# ADR-0021: User-Defined Structs, Payload Enums, and Flattened Aggregate Storage

## Status

Accepted

## Context

Sobakasu には複数の値を一つの型として扱うユーザー定義型がなく、関連する state や配列要素を個別の変数として管理する必要があった。一方、対象である Udon VM には、コンパイラが任意に追加できる struct / union ABI 型がない。そのため source 上の値のまとまりと Udon heap 上の表現を分離する必要がある。

この決定は ADR-0003 の Parser、Binder、IR Lowerer、UASM backend の責務分離、ADR-0007 の `let` / `mut`、ADR-0013 の共有可能な参照と shallow-copy、ADR-0014 の top-level state / `pub` / `sync`、ADR-0016 の `impl` と extern 境界、ADR-0018 の module / visibility、ADR-0019 の `object`、ADR-0020 の typed array と評価順序を維持しなければならない。初期値は ADR-0006 の post-assemble heap patch を引き続き使用する。

## Decision

### Compile-time aggregate type

非 generic のユーザー定義 `struct` と Rust 風 payload enum を nominal type として導入する。同じ field 構成でも宣言 identity が異なる型は互換ではなく、aggregate 間の structural implicit conversion は行わない。

aggregate は Sobakasu の Parser / Binder / Bound tree では一つの型と値である。Binder は宣言、field、variant、payload、tag、visibility、型を解決する。Parser や Binder で複数の独立変数へ早期分解せず、UASM backend に field / variant の探索や型検査を行わせない。

型宣言 shell を field 型の binding より先に収集して、同一 module 内および既存の `use` / `pub use` / qualified module access による前方参照を可能にする。`pub struct` と `pub enum` は ADR-0018 の通常の型 export と同じ規則を使う。v1 では field ごとの visibility は導入しない。Named Type Symbol を対象とする既存の通常 `impl` を aggregate にも再利用し、aggregate 専用 dispatch は作らない。

### Struct syntax and semantics

宣言、構築、field access は次の形とする。

```sobakasu
struct Foo {
  score: i32,
  finished: bool,
}

let mut foo = Foo {
  finished: false,
  score: 0,
};
foo.score = 100;
```

末尾 comma を許可する。v1 に field default はなく、構築時は全 field が必要である。記述順は宣言順と異なってよい。Binder は unknown / duplicate / missing field と型不一致、struct でない対象への initializer を診断する。layout は initializer 順でなく、宣言側の決定的な field 順に従う。

aggregate 全体および field の再代入には root binding の `mut` が必要である。ただし `mut` は deep immutability ではない。immutable `foo` でも `foo.values[0] = value` のような参照先配列の変更は ADR-0013 / ADR-0020 に従って許可し、`foo.values = replacement` は拒否する。

struct の代入、引数、戻り値は field-wise shallow copy へ lower する。primitive leaf は値を、配列、string、Unity / VRChat object 等の参照 leaf は既存の参照をコピーする。deep clone、ownership、move は導入しない。

### Payload enum syntax and semantics

一つの enum で次の三形式を混在可能とする。

```sobakasu
enum WebEvent {
  PageLoad,
  KeyPress(char),
  Ip(u8, u8, u8, u8),
  Click { x: i64, y: i64, },
}

let load = WebEvent.PageLoad;
let key = WebEvent.KeyPress('A');
let ip = WebEvent.Ip(127u8, 0u8, 0u8, 1u8);
let click = WebEvent.Click { x: 10i64, y: 20i64, };
```

Unit variant は payload を持たない。Tuple variant は 0 個に特殊化せず、複数の位置付き payload を持てる。Struct variant は名前付き payload を持つ。Binder は解決済み variant symbol と payload field symbol を Bound node に保存し、unknown / duplicate variant、tuple arity / type、named payload の unknown / duplicate / missing / type mismatch を診断する。

variant の tag は宣言順に 0 から割り当てる `i32` である。明示的 discriminant、C / C# 風整数 enum、flags enum は v1 では導入しない。

### Recursive leaf layout

ユーザー定義 aggregate は独立した Udon ABI 型を持たない。意味解析後、logical aggregate storage を Udon で表現可能な leaf storage へ宣言順で再帰的に flatten する。

```text
Player { score: i32, position: Position { x: f32, y: f32 } }

player__score: i32
player__position__x: f32
player__position__y: f32
```

実際の symbol 名は決定的で、source field path を追跡でき、既存 symbol と衝突しない名前にする。logical storage identity と physical leaf storage identity は区別する。Lowerer は aggregate value / storage と field path を解決済み leaf operation へ変換し、Assembler が受け取る命令は既存の scalar `COPY`、typed array extern、branch 等だけにする。

有限に flatten できない直接・間接・struct / enum 横断の dependency cycle は Binder で拒否し、cycle path を診断する。配列 field を介する cycle も例外にしない。

enum storage は tag leaf と全 variant の全 payload leaf を持つ。

```text
event__tag: i32
event__KeyPress__0: char
event__Click__x: i64
event__Click__y: i64
```

現在有効な payload は tag だけで決まる。variant を変更しても inactive payload は clear せず、stale storage を保持してよい。source から inactive payload を直接読む機能は提供しない。既存 location へ enum を書くときは、各 source expression を左から右へ一度だけ評価し、新しい payload leaf を先に書き、tag を最後に書く。

### Aggregate arrays use Structure of Arrays

通常の `[T]` は ADR-0020 のとおり単一の Udon ABI `T[]` である。ユーザー定義 aggregate `Foo` に限り `[Foo]` は `Foo[]` へ変換せず、各 leaf の typed array からなる Structure of Arrays (SoA) とする。

```text
[Foo { score: i32, finished: bool }]

foos__score: i32[]
foos__finished: bool[]
```

全 leaf array は常に同じ length を持つ。array literal は各要素を source 順で一度だけ評価して各 leaf setter へ展開する。repeat construction は length を一度だけ評価し、同じ値で全 leaf array を構築する。値 repeat の operand、長さ 0、左から右の評価順序は ADR-0020 を維持する。

`foos[i]` の read / write は index を一度だけ評価して各 leaf getter / setter へ展開する。`foos[i].score` は対応する `foos__score[i]` に直接 lower でき、aggregate 全体の不要な materialize / write-back は行わない。compound assignment でも array、index、右辺を既存規則どおり一度だけ評価する。enum array は tag array と payload arrays を使い、element assignment でも payload setter を tag setter より先に emit する。

aggregate の中に aggregate がある場合および enum payload 内の aggregate にも同じ recursive layout を使う。aggregate 内の通常配列 leaf はその参照を一つの leaf とし、aggregate array に含めるには対応する typed array-of-array ABI が SDK catalog に存在しなければならない。

### State, public variables, synchronization, and heap patches

top-level aggregate state は同じ UdonBehaviour 内の複数 leaf heap slots に展開し、どの関数・イベントから参照しても同じ slots を使用する。`pub` aggregate はすべての leaf を deterministic / collision-free な public symbols として export する。既存の単純 state の名前と ABI は変更しない。

`sync` aggregate はすべての leaf に同じ sync mode を付け、一つの logical synchronization group とみなす。同一 Behaviour の通常 serialization を使用し、version counter、二重 buffer、複数 Behaviour への分割は導入しない。`pub` と `sync` は ADR-0014 の独立した性質を保つ。

Binder は各 physical leaf に ADR-0014 / ADR-0020 の storage、public typed-array、sync mode 互換性を適用する。一つでも非対応 leaf があれば logical field path を含む診断で aggregate 全体を拒否する。

定数 top-level initializer は logical valueを宣言順の leaf constant に評価する。各 physical state を既存 ADR-0006 の `GlobalInitializer` heap patch entry として保存するため、primitive、nested aggregate、payload enum、SoA aggregate array の runtime 型情報と初期値は ProgramAsset refresh 後も復元される。aggregate 専用の第二の保存形式は作らない。

### Functions and extern boundary

通常の Sobakasu 関数の aggregate 引数 / 戻り値 / local は logical aggregate のまま型検査し、既存 inline lowering の parameter / result storage を leaf bindings へ展開する。Udon ABI に aggregate parameter type は生成しない。

Sobakasu aggregate 自体を Udon extern の引数、戻り値、operator receiver 等として渡すことは禁止する。`object` への暗黙 boxing で境界を迂回しない。必要な leaf は source で明示的に渡す。これは ADR-0016 / ADR-0019 の解決済み extern 境界を維持する。

### Out of scope

generic struct / enum、generic instantiation、monomorphization、`match`、pattern matching、`if let`、destructuring、明示的 discriminant、recursive aggregate、inheritance / trait / interface、aggregate runtime reflection、dynamic dispatch、packed / user-specified layout、C ABI、inactive payload access はこの決定に含めない。

将来 optimizer が内部 layout を最適化する余地は残すが、source semantics、評価順序、logical storage identity を変更してはならない。

## Alternatives

1. user-defined aggregate を Udon ABI 型として生成する。Udon に任意の struct / union ABI がないため却下した。
2. `object[]` に AoS として詰め込む。primitive の boxing / unboxing、static type 情報の消失、operator / assignment / sync の複雑化、既存 typed array との不整合が生じるため却下した。
3. 固定長 aggregate array の全要素を scalar slots に展開する。runtime length に対応できず一般の `[Foo]` を表現できないため却下した。
4. 同型 leaf を `[x0, y0, x1, y1, ...]` のように interleave する。型構成によって stride と layout が変わり、field access、sync、patch が複雑になるため v1 では却下した。
5. backend が struct / enum を再解釈して flatten する。型解決、variant 検査、layout 判断、`pub` / `sync` 意味判定が backend に漏れ、ADR-0003 の責務分離に反するため却下した。

採用案は recursive leaf flattening for aggregate values and Structure of Arrays for aggregate arrays である。

## Rationale

一つの layout traversal を struct、enum、nested aggregate、local、function、state、array に共有することで、Sobakasu の nominal value semantics と Udon の既存 typed storage を両立できる。Binder が意味と ABI 適合性を確定し、Lowerer が leaf operation を生成すれば、UASM Assembler は source-level aggregate を知らずに既存命令と heap patch を処理できる。

## Consequences

### Positive

* Udon に独自 runtime 型を要求せず、typed Udon storage を維持できる。
* struct、payload enum、nested aggregate、aggregate array が一つの recursive layout 原則を共有する。
* `pub` / `sync` / heap patch を既存の leaf 規則へ自然に展開できる。
* tag + payload representation を将来の `match` 実装で再利用できる。
* source-level 型解決や aggregate layout 判断を backend に持ち込まない。

### Negative

* aggregate field 数に比例して Udon variable と aggregate array extern operation が増える。
* enum には inactive payload storage が残る。
* SoA の物理 layout は source 上の AoS イメージと異なる。
* recursive aggregate は表現できない。
* aggregate ABI を要求する extern へ直接渡せない。
* public aggregate は複数の Udon public symbols として見える。
* deterministic naming、aggregate layout、heap patch manifest の連携が複雑になる。
