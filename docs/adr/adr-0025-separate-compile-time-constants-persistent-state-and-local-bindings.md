# ADR-0025: Separate Compile-Time Constants, Persistent State, and Local Bindings

## Status

Accepted (typed source `null` initializer portions superseded by ADR-0026)

ADR-0026により、scalar `const`または`state`の型付きsource `null`は廃止された。optional persistent stateは`state value: Maybe<T> = Maybe.Nothing;`で表し、aggregate flatteningとheap patchを使用する。ABI placeholderとしてのnullは維持する。

## Context

Sobakasu はこれまで、ブロック内の local binding とトップレベルの UdonBehaviour state の両方を `let` / `let mut` で宣言していた。しかし両者は lifetime と storage semantics が大きく異なる。local `let` は一回の関数・イベント呼び出しと block scope に属する一方、トップレベル `let` は UdonBehaviour インスタンスごとの heap slot としてイベント呼び出しをまたいで存続していた。

トップレベルの immutable `let` は見た目が定数に近いにもかかわらず、実際には runtime heap state であった。Udon の `.export`、`.sync`、Inspector／ProgramVariable interoperability を持つ persistent state と、コンパイル時に値が確定して runtime state slot を必要としない constant は異なる概念である。

宣言位置から暗黙に storage class が切り替わる構文をやめ、source keyword から lifetime と storage class が分かるようにする必要がある。

## Decision

Sobakasu の値宣言を次の三種類に分離する。

```text
const = compile-time lifetime
state = UdonBehaviour instance lifetime
let   = local invocation/block lifetime
```

`let` / `let mut` は ADR-0007 の local binding 専用とする。immutable default、`mut` による再代入、shadowing、block scope、既存 lowering は変更しない。トップレベル `let` / `let mut` は breaking change として拒否し、`const` または `state` への移行を案内する。

### Compile-time constants

次の構文を採用する。

```text
ConstDeclaration
  ::= "pub"? "const" Identifier TypeClause? "=" ConstExpression ";"
```

`const` は Binder が型と値を確定する独立した `ConstantSymbol` である。型注釈がない場合は initializer から推論し、型注釈がある場合は既存の assignment／conversion 規則を適用する。定数宣言は runtime `StateVariableSymbol` や `IrStateStorage` を生成せず、参照箇所で既存の IR constant へ lower する。backend が値のために内部 constant data slot を共有することは認めるが、その slot は source の state、Udon public variable、Inspector variable ではない。

同じ module、`use`、qualified module access、`pub use`、implicit Prelude から constant を解決できる。`pub const` の `pub` は Sobakasu module visibility だけを意味し、Udon `.export` を生成しない。local variable と parameter は既存の優先順位で constant を shadow する。

constant は宣言順に依存せず別の constant を参照できる。Binder は依存関係を再帰的に評価し、cycle とその依存経路を診断する。constant は state、runtime function call、extern call に依存できない。runtime expression と state initializer から constant を参照することはできる。

現在の実装は、既存の state initializer constant evaluator が安全に値化でき、単一の Udon constant value として表現できる scalar を対象とする。数値、`bool`、`char`、`string`、対応する外部 scalar／enum、型付き `null` を扱う。array constant と user-defined aggregate constant は明示的に拒否し、runtime state へ fallback しない。array／aggregate state initializer の各要素または leaf から scalar constant を参照することはできる。

### Persistent state

次の構文を採用する。

```text
StateDeclaration
  ::= "pub"? SyncModifier? "state" Identifier TypeClause? "=" Expression ";"

SyncModifier
  ::= "sync"
    | "sync" "(" ("none" | "linear" | "smooth") ")"
```

`state` は per-UdonBehaviour persistent state であり、常に mutable とする。`state mut` は存在せず診断する。immutable なトップレベル値には `const` を使用する。

`state` は既存の `StateVariableSymbol`、`IrStateStorage`、physical Udon heap storage へ lower する。`pub state` は ADR-0014 の Udon `.export`、public symbol name、Inspector／ProgramVariable interoperability を維持する。`sync state` は既存の `.sync`、`none` / `linear` / `smooth`、SDK 型互換性検査を維持する。`pub` と `sync` は引き続き独立する。

state initializer は既存どおり compile-time evaluation 可能な式に限定し、`GlobalInitializer` heap patch を使用する。named constant の評価済み値を initializer から利用できる。primitive、string、array、object、aggregate、generic aggregate の既存 state storage、flattening、SoA、sync、heap patch semantics は変更しない。

### ADR-0014 との関係

この ADR は ADR-0014 のトップレベル宣言構文、`let` による storage class の切り替え、state mutability model を supersede する。一方、次の Udon semantics は ADR-0014 から継承する。

* UdonBehaviour インスタンスごとの永続 heap storage
* `.export` と `.sync`
* public と synchronization の独立性
* `none` / `linear` / `smooth` と同期可能型の検査
* Behaviour の Manual／Continuous sync mode との区別
* post-assemble `GlobalInitializer` heap patch

## Alternatives

1. 既存のトップレベル `let` / `let mut` を維持する案は、宣言位置による lifetime と storage class の暗黙変更が残るため採用しない。
2. Rust の `static` を採用する案は、Sobakasu の値がプロセス全体の static ではなく UdonBehaviour インスタンス state であることを表しにくいため採用しない。
3. `status` keyword を採用する案は、特定のステータス値を連想させる。`state` はプログラミング一般で persistent state を指す自然な語であり、用途を狭めないため `state` を採用する。
4. `state mut` で可変性を明示する案は、persistent state を immutable と mutable に再分割し、compile-time constant との境界を再び曖昧にするため採用しない。
5. `state` を常に mutable とする案は、変化し得る UdonBehaviour state という意味と一致するため採用する。
6. immutable top-level state を維持する案は、compile-time constant と外部から変化し得る runtime slot のどちらを求めているかを構文から判別できないため採用しない。
7. `pub const` を禁止する案は、standard library の named constants を通常の module visibility と re-export 規則で提供できなくなるため採用しない。
8. compile-time constant を runtime state として保持する案は、不要な heap slot、`.export` との混同、heap patch、外部変更可能性を生むため採用しない。

## Rationale

三つの keyword が三つの lifetime と一致することで、同じ `let` が宣言位置によって local storage と Udon heap storageへ変わることがなくなる。`state` と `StateVariableSymbol` / `IrStateStorage` の用語も source から backend まで一致する。

Binder が constant の名前解決、型付け、依存評価、cycle、module visibility を確定し、Lowerer が評価済み値を IR constant へ変換するため、ADR-0003 の責務分離を維持できる。UASM backend は source declaration、module path、constant dependency を解決しない。

## Consequences

### Positive

* storage class と lifetime が source syntax から分かる。
* compile-time constant と persistent runtime state が明確に分離される。
* `state` と compiler internal terminology が一致する。
* `sync state` の意味が読みやすくなる。
* standard library は `pub const PI` のような API を通常の module system で提供できる。
* local `let` semantics が単純になる。
* constant 宣言自体は Udon state slot、`.export`、`GlobalInitializer` patch を生成しない。

### Negative

* 既存 Sobakasu source のトップレベル `let` は移行が必要な breaking change になる。
* Parser、Binder、IR、tests、docs、sample の更新が必要になる。
* constant の依存評価と cycle diagnostics が Binder に加わる。
* 同じ `pub` でも `pub const` は module visibility、`pub state` は Udon export semantics を持つため、declaration kind ごとの処理が必要になる。
* array／aggregate constant は未対応であり、必要なら value representation と evaluator を別の設計判断で拡張する必要がある。
