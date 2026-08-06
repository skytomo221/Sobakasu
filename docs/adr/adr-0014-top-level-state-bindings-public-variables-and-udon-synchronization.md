# ADR-0014: Top-level State Bindings, Public Variables, and Udon Synchronization

## Status

Accepted

## Context

Sobakasu の `let` はこれまでブロック内のローカル変数だけを表していた。一方、UdonBehaviour はイベント呼び出しをまたいで heap 上の状態を保持し、Udon Assembly は変数の外部公開とネットワーク同期を `.export` と `.sync` で別々に表現する。

状態を関数のローカルへ落とすとイベント間で値が失われる。トップレベル immutable 値を定数としてインライン化すると、Udon の外部 API から公開変数が変更された後も古い値を読むことになる。また、公開と同期を同じ修飾子へまとめると、非公開の同期変数や公開するだけの変数を表現できない。

VRChat Worlds SDK 3.10.4 の `UdonNetworkTypes` と UdonSharp の UASM 出力を確認すると、非公開の同期変数も有効であり、同期のために `.export` は必須ではない。同期可能な型と補間モードの組み合わせには SDK 側の制約がある。

## Decision

### 宣言構文

トップレベルに次の状態宣言を追加する。

```text
StateDeclaration
  ::= "pub"? SyncModifier? "let" "mut"? Identifier TypeClause? "=" Expression ";"

SyncModifier
  ::= "sync"
    | "sync" "(" SyncMode ")"

SyncMode
  ::= "none" | "linear" | "smooth"
```

正規順序は `pub? sync(mode)? let mut? name : type? = initializer;` とする。`pub` と `sync` はトップレベル状態にのみ使用でき、重複、順序違反、未知の同期モード、引数なしまたは複数引数の `sync(...)` は診断する。`none`、`linear`、`smooth` は `sync(...)` 内だけで同期モードとして扱う contextual identifier とする。

```sobakasu
let mut count = 0;
pub let enabled = true;
sync let mut global_status = 0;
pub sync(linear) let mut synchronized_value: f32 = 0.0;
```

裸の `sync` は `sync(none)` と同じ意味を持つ。`sync` はネットワークから値が書き換わるため `mut` を必須とする。`pub` は Sobakasu 内の再代入可否を変更しないため、`pub let value = 0;` は Sobakasu 内では immutable のままである。

ブロック内の `let` は従来どおり、呼び出しごとに使われるローカル変数である。トップレベルの `let` は UdonBehaviour インスタンスごとに一つの永続 heap slot を持つ状態変数であり、プロセス全体で共有する static/global 変数ではない。同じ Program Asset を複数の UdonBehaviour へ割り当てても、各インスタンスが独立した heap 状態を持つ。immutable でも定数へ置換しない。

### 名前解決と意味解析

Binder にローカル変数とは別の `StateVariableSymbol` を導入し、型、可変性、公開性、同期の有無とモード、初期値、宣言位置を保持する。状態宣言は関数とイベントの本体より先に収集し、宣言順に依存しない参照を許可する。同じスコープのローカル変数と引数は状態変数を shadow する。

状態変数への単純代入と複合代入は `mut` を検証する。関数とイベントからの読み書きは、常に同じ状態 symbol を参照する。

トップレベル初期化子は v1 ではコンパイル時に評価できる定数式に限定する。プリミティブリテラル、文字列、`bool`、単項および二項の定数演算、型注釈付き参照型の `null` を扱う。`null` だけから型を推論することはできない。関数呼び出し、状態変数参照、実行時 API 呼び出しなどは拒否する。

```sobakasu
let count = 0;
let name = "example";
let value: f64 = 1.0f64;
let negative = -1;
let target: GameObject = null;
```

### 公開と同期

`pub` は対象 data slot に `.export` を出力する。ソース名を Udon の公開 symbol 名として維持する。`pub` のない状態は内部名を使用し、ソースレベルの公開 API に現れない。

`sync` は対象 data slot に `.sync <symbol>, <mode>` を出力する。`pub` を暗黙に追加しない。したがって、公開性と同期は独立した二軸である。

SDK 3.10.4 に合わせ、同期モードと型を次のように検証する。

| モード | 対応型 |
| --- | --- |
| `none` | `bool`、`char`、整数型、浮動小数点型、`string`、`Color`、`Color32`、`Vector2`、`Vector3`、`Vector4`、`Quaternion`、`VRCUrl`、およびそれらの配列 |
| `linear` | 整数型、浮動小数点型、`Color`、`Color32`、`Vector2`、`Vector3`、`Quaternion` |
| `smooth` | 整数型、浮動小数点型、`Vector2`、`Vector3`、`Quaternion` |

配列の宣言・初期化は ADR-0020 で導入する。`none` で同期できるのは表に含まれる要素型の一次元配列だけであり、ジャグ配列、`object[]`、Unity object参照配列は含まれない。

### IR、UASM、初期値

IR にはローカル storage と別の `IrStateStorage` を持たせる。状態の load/store は関数の inline lowering 後も同じ storage identity を維持する。UASM assembler は状態ごとに data slot を一度だけ割り当て、すべてのイベントと関数由来のコードからその slot を参照する。

UASM assemble 後に実値を設定する必要がある非 null 初期値は、通常の実行時定数 patch と区別した `HeapPatchKind.GlobalInitializer` として manifest に保存する。既存の assemble、heap patch、commit、refresh の流れを再利用し、refresh 後も初期値と同期 metadata を復元する。型付き `null` は UASM の data 初期値 `null` で直接表現する。

`none` は同期を無効にする指定ではなく、補間なしの同期である。Behaviour 全体の Manual／Continuous 同期方式は変数の `none`／`linear`／`smooth` とは別概念とし、この機能は同期 metadata を生成するだけである。ownership の取得、`RequestSerialization` の呼び出し、同期タイミングの制御、同期イベントの追加は行わない。

## Alternatives

### `field` や C# 互換の属性構文を導入する

却下した。トップレベルとブロック内で `let` の storage class を分ければ既存の宣言規則を再利用でき、C# 互換を目的としない Udon-first の設計にも合う。

### 公開変数をすべて自動同期する

却下した。Udon の `.export` と `.sync` は独立しており、公開だけ、同期だけ、両方、どちらでもない、の四通りが必要である。

### immutable なトップレベル値をコンパイル時定数へ置き換える

却下した。公開された Udon symbol は外部 API から変更され得るため、Sobakasu からの読み取りも実際の heap slot を参照しなければならない。

### 実行時初期化コードを生成する

却下した。イベントより前に一度だけ実行される初期化の仕組みを新設すると Udon の実行順序と再初期化に追加仕様が必要になる。v1 は定数式と既存の post-assemble heap patch に限定する。

## Rationale

Parser、Binder、IR、UASM backend の責務を分離したまま、Udon の実行モデルをそのまま表現できる。公開と同期を直交させることで SDK の UASM モデルと一致し、`sync` のモード・型検証を意味解析に置ける。初期値は既存の heap patch manifest に載せるため、Unity Editor の refresh 経路も一つに保てる。

## Consequences

### Positive

* イベントと関数をまたぐ UdonBehaviour ごとの永続状態を直接記述できる。
* Udon の public variable と network synchronization を独立して表現できる。
* 不正な同期型と同期モードを UASM 生成前に診断できる。
* 既存のローカル `let`、shadowing、heap patch、ProgramAsset refresh の仕組みを維持できる。

### Negative

* `let` の storage class は宣言位置によって変わるため、利用者はトップレベルとブロック内の寿命の違いを理解する必要がある。
* SDK の同期可能型が変更された場合は互換表を追従する必要がある。
* v1 の初期化子では関数呼び出しや他の状態を参照できない。
* 同期の ownership と送信タイミングは利用者が Udon の規則に従って別途扱う必要がある。
