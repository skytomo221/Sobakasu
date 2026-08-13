# ADR-0020: Built-in Arrays, Array Literals, Repeat Construction, and Indexing

## Status

Accepted (source-level `null` portions superseded by ADR-0026)

ADR-0026により、array literalの「最初のnon-null要素」規則とsource null elementは廃止された。通常の最初の型付き要素から推論し、nullable elementは`[Maybe<T>]`で表す。heap patch、CLR array、typed Udon slotが内部ABI値としてnullを保持できることは維持する。

## Context

Sobakasu は Udon-first の言語だが、これまで配列リテラルの初期構文だけがあり、配列型、生成、添字、状態初期値までを一貫して扱えなかった。Udon の配列は CLR の一次元配列を ABI 型として持ち、生成、getter、setter、長さ取得は型ごとの extern node で公開される。要素型が公開されていても、その配列型や各操作が必ず公開されるとは限らない。

配列の導入では、ADR-0003 の Parser／Binder／IR／backend 分離、ADR-0006 の post-assemble heap patch、ADR-0013 の共有可能な可変参照、ADR-0014 の public／sync 分離、ADR-0015 のゼロ引数呼び出し、ADR-0016 の Binder で確定する extern 境界を維持する必要がある。また ADR-0004 の初期配列仕様、ADR-0009 の compound assignment の左辺制限、ADR-0019 の配列除外を更新する必要がある。

## Decision

### 型と ABI

`[T]` を CLR／Udon ABI の `T[]` に対応する参照型とする。長さは型に含めず、同じ要素型から作る配列型 symbol は intern する。`[[T]]` は矩形多次元配列ではなく `T[][]` というジャグ配列である。

Binder は要素型の CLR ABI 型から配列 CLR 型を構築し、インストール済み SDK の Udon node catalog で次をすべて検証する。

* 配列 ABI 型自体
* `Array.__ctor(i32) -> Array`
* `Array.__Get(i32) -> T`
* `Array.__Set(i32, T) -> u0`
* `Array.__get_Length() -> i32`

SDK 3.10.4 で確認した標準添字型と長さ型は `System.Int32` であり、Sobakasu では `i32` とする。実際の署名は、例えば `[i32]` では次になる。

```text
SystemInt32Array.__ctor__SystemInt32__SystemInt32Array
SystemInt32Array.__Get__SystemInt32__SystemInt32
SystemInt32Array.__Set__SystemInt32_SystemInt32__SystemVoid
SystemInt32Array.__get_Length__SystemInt32
```

同 SDK の実カタログでは、少なくとも `System.Int32[]`、`System.String[]`、`System.Object[]`、`UnityEngine.GameObject[]` と必要な操作を確認した。一方、`System.Int32[][]` は公開されていない。このためジャグ配列の構文と一般化された型構築は実装するが、SDK 3.10.4 上の `[[i32]]` は明確な ABI 利用不可診断になる。

ジャグ配列を含め、構築した配列型または必要な操作が catalog にない場合は Binder 診断とする。要素型だけを根拠に利用可能とはみなさない。

### リテラルと生成

要素列は `[a, b, c]`、空配列は `[]` とする。非空配列は期待要素型があれば各要素を通常の代入規則で適合させ、期待型がなければ先頭の非 `null` 要素型を候補にする。異種要素を暗黙に `[object]` へ統一しない。`[]` は `[i32]` などの期待型がある場合だけ許可する。期待型が `[object]` の場合は ADR-0019 の通常の boxing を許可するが、`object` からの暗黙 unboxing は導入しない。

`[left; length]` は Parser では left を式候補として保持し、Binder が名前空間と値スコープを使って決定する。

* 型としてだけ解決できる `[T; length]` は、CLR 配列生成時の既定値で初期化された新しい配列を返す。追加の要素書き込みループは生成しない。
* 値としてだけ解決できる `[expression; length]` は、長さを一度評価し、添字の昇順に expression を要素ごとに一度ずつ再評価する。
* 型と値の両方なら曖昧診断、どちらでもなければ未解決診断とする。

式 repeat は Rust の「値を一度評価して複製」とは異なる Sobakasu 独自仕様である。長さが 0 のとき operand は評価しない。`[[i32; 2]; 3]` の内側生成は外側ループ本体で3回実行されるため、ABI が利用可能なら独立した3配列になる。単なる参照式を repeat した場合は式を毎回読むが、得た同じ参照を各要素で共有する。

定数の負の長さは診断する。動的な負の長さ、過大長、添字範囲外には独自の実行時検査や例外モデルを加えず、解決済み Udon extern の挙動を維持する。

### 添字、代入、長さ

`array[index]`、連鎖した `matrix[row][column]`、要素への `=` と compound assignment を組み込み配列について許可する。配列要素は正規の代入可能位置であり、配列を保持する binding が `let` でも要素変更を許可する。`mut` は配列参照そのものを別配列へ差し替えられるかだけを制御し、深い不変性を表さない。

compound assignment は getter、選択済み演算子、setter へ lower する。配列式、添字式、元要素、右辺をそれぞれ一度だけ、既存の左から右の式評価順序で評価するため、Lowerer は配列参照と添字を temporary に保存する。これは ADR-0009 の「mutable local だけ」という制限を、組み込み配列要素について部分的に置き換える。一般 property、field、任意 indexer には広げない。

`array.length` と `array.length()` は ADR-0015 に従う同じゼロ引数組み込みメンバーであり、戻り値は `i32` とする。

### Lowering と backend

Binder は配列型と constructor／getter／setter／length extern、添字型、compound operator を確定した Bound node を作る。Lowerer は要素列を生成と左から右の setter 列へ、式 repeat を明示的 temporary と CFG loop へ変換する。UASM Assembler は解決済み IR の slot、branch、extern call を出力するだけで、型推論、型／式判定、extern 探索、public／sync 判定を行わない。

配列の通常代入、引数、戻り値は参照をそのまま受け渡す。暗黙 clone、deep copy、move は行わない。表面構文に `new` keyword は追加しない。

### 状態、public、sync、heap patch

トップレベル配列 `state` 初期化子は ADR-0014 から ADR-0025 が継承した定数式制限を維持する。リテラル、定数要素の repeat、型 default repeat、ネスト配列を再帰評価できるが、関数呼び出しや実行時長は拒否する。initializer の要素から ADR-0025 の scalar `const` を参照できる。

非 `null` 配列初期値は ADR-0006 の post-assemble heap patch manifest に、配列 ABI runtime type 名と再帰的な型付き値として保存する。長さ、null、ネスト配列、`object[]` 内の boxing 元型を保持し、ProgramAsset refresh で CLR 配列を再構築する。ソースの定数配列で共有参照を表現できる構文は現時点にない。将来それが可能になった場合、manifest が identity を保持できるまで黙って複製しない。

public 可否と sync 可否は別検査とする。public は SDK が Inspector／Udon ABI 型として公開する配列だけを許可する。同期は ADR-0014 の表に従い `none` の一次元対応要素配列だけを許可し、`linear`／`smooth`、`object[]`、Unity object 参照配列、ジャグ配列を拒否する。SDK が将来ネスト配列を明示的に同期可能として公開する場合は、同期互換表の別更新を必要とする。

### 既存 ADR との関係

* ADR-0003、ADR-0006、ADR-0007、ADR-0013、ADR-0015、ADR-0016 の責務と基本方針を維持し、配列へ適用する。
* ADR-0004 の配列リテラル初期方針を、型、repeat、ジャグ配列、lowering まで具体化する。
* ADR-0009 の compound assignment 左辺制限を、組み込み配列要素に限って部分的に supersede する。
* ADR-0014 の同期互換表を実装し、配列は `none` の対応型だけとする。
* ADR-0019 の「配列と `object[]` は対象外」と「非 null boxed state は型情報を永続化できない」という制限を、配列 heap patch に必要な範囲で部分的に supersede する。単独の非 null `object` state 制限は維持する。

## Alternatives

1. 配列型を要素型ごとに手登録する。SDK更新に追従しにくく、ジャグ配列や外部型を一般化できないため却下した。
2. Parser が組み込み型名を見て repeat の型／式を決める。import、外部 binding、将来型を扱えず名前解決責務が漏れるため却下した。
3. repeat operand を一度評価して全要素へ複製する。副作用と独立したネスト配列に関する要求を満たさないため却下した。
4. backend で配列 extern を組み立て直す。意味解決が backend に漏れ ADR-0003 と矛盾するため却下した。
5. 配列代入時に暗黙 clone する。Udon の参照モデルと ADR-0013 に反し、コストも不可視になるため却下した。
6. 全配列へ自動境界検査を加える。Udon の既存失敗規則を変更し、v1 のコストと仕様を増やすため採用しない。

## Rationale

SDK node catalog を意味解析の根拠にすることで、Udon が実際に提供する ABI だけを利用できる。配列固有の意味を Bound tree と Lowerer に置き、評価回数を temporary／CFG で明示することで、複雑な repeat と compound assignment を backend の推測なしに実装できる。共有参照と `let`／`mut` の区別は既存言語モデルを保ち、再帰型付き manifest は Unity refresh 後も状態初期値を安全に復元できる。

## Consequences

### Positive

* 配列をローカル、引数、戻り値、状態、public、対応する sync で一貫して利用できる。
* リテラル、default、再評価 repeat、添字、compound assignment、長さを Udon ABI に直接対応させられる。
* 評価順序と評価回数が IR 上で明示される。
* `object[]` とネスト配列の状態値を型情報付きで永続化できる。

### Negative

* SDK が公開しない配列やジャグ配列は、CLR 上で構築可能でもコンパイルできない。
* 動的な不正長と範囲外添字は Udon extern の実行時挙動に依存する。
* 配列は共有可変参照なので、alias を介した変更は利用者が意識する必要がある。
* manifest の形式に再帰的型情報が加わり、配列値の保存サイズが増える。
