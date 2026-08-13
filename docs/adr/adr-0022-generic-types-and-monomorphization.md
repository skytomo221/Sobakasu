# ADR-0022: Generic Types and Monomorphization

## Status

Accepted

ADR-0026は、このADRのgeneric enum／monomorphizationを維持したまま、標準の不存在型を`Maybe<T>`と定める。本ADR中の`Option<T>`はgeneric機構の歴史的な例であり、`Maybe<T>`も同一の通常のconstructed enumとして処理される。

## Context

Sobakasu の user-defined aggregate は ADR-0021 により nominal type、recursive leaf flattening、payload enum の tag + payload storage、aggregate array の Structure of Arrays (SoA) として実装されている。一方、ADR-0021 は generic struct / enum、generic instantiation、monomorphization を out of scope としていたため、`Option<T>`、`Pair<T, U>`、`Container<T>` のような再利用可能な aggregate を宣言できなかった。

Udon VM には Sobakasu が利用できる runtime generic type representation がない。また ADR-0003 は Parser が構文を保持し、Binder が名前・型・変換を解決し、IR と UASM backend は解決済みの意味だけを扱う責務分離を定めている。したがって source-level generic の解決、型引数推論、展開を UASM backend に持ち込むことはできない。

ADR-0016 は `impl`、`Self`、`self`、method / associated function、extern boundary を定めたが generic `impl` を除外している。generic aggregate を実用的にするには、receiver の型引数を method signature と body に適用できる必要がある。

数値リテラルは ADR-0005 の既定型を維持する。すなわち文脈のない整数は `i32`、浮動小数点数は `f32` であり、generic 導入のために新しい literal inference variable は作らない。

## Decision

### Syntax and definitions

次の aggregate declaration と generic `impl` を導入する。

```sobakasu
struct Pair<T, U> {
  first: T,
  second: U,
}

enum Option<T> {
  None,
  Some(T),
}

impl<T> Option<T> {
}
```

型パラメータは struct field、enum の unit / tuple / struct variant payload、generic impl 内の method parameter / return type / body で使用できる。generic top-level function と method 固有の追加型パラメータは導入しない。同一 parameter list 内の重複名は禁止する。

Parser は generic parameter list、type argument list、expression 上の explicit type application を syntax として保持するだけで、型名を解決しない。type argument list の解析中に限り `>>` token を二つの `>` として文脈的に消費する。通常の expression では `a >> b` を引き続き right shift として扱う。

### Type symbols and identity

generic parameter は独立した `TypeSymbol` とする。その identity は generic declaration identity と parameter ordinal で決まり、同名でも別宣言の parameter は別 symbol である。

generic definition と constructed type を区別する。constructed type の identity は次で決まる。

```text
generic definition identity
+ ordered concrete type arguments
```

各 generic definition は compilation 内で constructed type cache を所有する。同じ definition と同じ順序付き型引数からは同じ symbol instance を返し、`Pair<i32, string>` と `Pair<string, i32>` は別 symbol とする。runtime hash や生成順に依存する名前は identity に使わない。

型引数の個数は parameter 数と完全一致しなければならない。default type argument と partial generic application は導入しない。runtime storage が必要な位置では、すべての型引数が concrete でなければならない。

### Substitution and monomorphization

Sobakasu の generics は compile-time static generics とし、constructed type ごとに monomorphize する。Binder が generic definition の field、enum payload、array element、nested constructed type、method signature に現れる parameter を再帰的に置換する。

```text
Foo<T> { value: T, values: [T], option: Option<T> }
Foo<i32> -> { value: i32, values: [i32], option: Option<i32> }
```

置換後の concrete aggregate は ADR-0021 の既存 aggregate symbol と layout traversal へ渡す。generic 専用 runtime layout は作らない。したがって concrete struct は既存の recursive leaf flattening、concrete payload enum は既存の tag + payload storage、`[ConcreteGenericAggregate]` は既存の SoA lowering を使用する。aggregate 内の array も置換後の element type に対して ADR-0020 の array system を使用する。

generic parameter、open constructed type、generic metadata は Bound tree から lowering へ渡さない。UASM backend は generic definition、parameter、type argument inference、monomorphizationを認識しない。

### Type argument inference

Binder は explicit type arguments に加え、次の情報から型引数を推論する。

1. concrete expected type
2. struct initializer の宣言 field と対応付けた initializer expression type
3. enum tuple / struct variant の宣言 payload と対応付けた argument type

expected type は最初の制約として扱う。続いて declaration 側の template type と実引数型を照合し、generic parameter、array、nested constructed type を再帰的に統一する。initializer の source order ではなく field name と declaration field の対応を用い、既存の unknown / duplicate / missing field diagnostics を維持する。

同一 parameter に異なる concrete identity が要求された場合は inference conflict とする。通常の implicit conversion は型引数が決定した後の payload / field assignment にだけ適用し、型引数 identity の競合を `object` erasure や widening で解消しない。未決定 parameter には default type を補わず診断する。

literal は ADR-0005 の既存型をそのまま inference へ入力する。したがって `Option.Some(42)` は `Option<i32>`、`Option.Some(3.14)` は `Option<f32>` となる。

### Generic impl

`impl<T> Box<T>` は generic aggregate receiver に対する compile-time template とする。Binder は concrete receiver が使用されたときに method parameter、return type、`self`、`Self`、body 内の型参照を receiver の concrete arguments で置換し、既存の user method binding と inline lowering に登録する。

method 自体は新しい runtime generic callable にならない。使用された concrete receiver ごとに concrete `FunctionSymbol` と bound body を作る。追加の method type parameter、specialization、partial specialization、constraint-based impl selection は導入しない。`impl Foo<i32>` のような specialization を必要とする target は拒否する。

ADR-0016 の method / associated function、optional zero-argument call、`self` / `Self`、extern boundary、inline lowering は維持する。concrete generic aggregate であっても Sobakasu aggregate 自体を Udon extern argument / return value として渡さず、`object` erasure で迂回しない。

### Recursion, state, visibility, and arrays

generic declaration と constructed aggregate の dependency を Binder で検査する。`Node<T> { next: Node<T> }` のような無限 layout と間接 cycle は拒否する。一方、`Wrapper<Wrapper<i32>>` のように最終的な concrete layout が有限な型は許可する。

concrete generic aggregate の top-level `state`、`pub`、`sync`、heap patch、function argument / return、array は ADR-0025 が ADR-0014 から継承した規則と ADR-0020、ADR-0021 の既存規則を leaf ごとに適用する。`state` は常に mutable であり、generic であることを理由に新しい state、sync、array representation を作らない。

### Existing ADRs and scope

この ADR は ADR-0021 の out-of-scope 項目のうち generic struct / enum、generic instantiation、monomorphization のみを拡張する。ADR-0021 の nominal identity、flattening、enum storage、SoA、state / `pub` / `sync`、heap patch、function lowering、extern boundary は維持する。

この ADR は ADR-0016 の generic `impl` 除外を、aggregate receiver の parameter を利用する method に必要な範囲で置き換える。ADR-0016 のその他の決定は維持する。ADR-0003、ADR-0005、ADR-0018、ADR-0020 の責務、literal、module visibility、array の決定も維持する。

次は out of scope とする。

* generic top-level functions
* method 固有の追加 generic parameters
* traits、interfaces、constraints、`where`
* variance、higher-kinded types、associated types
* default generic arguments、partial application
* specialization、partial specialization
* runtime reflection、runtime generic metadata、type erasure
* generic CLR / Udon extern binding、generic extern methods
* lifetime、ownership、`match`、pattern matching

## Alternatives

### Runtime generics

Udon 側に利用可能な generic runtime representation がなく、Sobakasu の compile-time 型解決方針とも合わないため採用しない。

### `object` type erasure

static type information が失われ、boxing、runtime check、operator resolution、extern boundary が複雑になる。aggregate extern prohibitionも迂回してしまうため採用しない。

### Expand generic definitions in the backend

UASM backend が source-level type resolution、inference、aggregate layout selection を行うことになり、ADR-0003 の責務分離に反するため採用しない。

### Compile-time monomorphization

concrete type を Binder で確定でき、既存 aggregate、array、state、Udon ABI infrastructure を再利用できるため採用する。

## Rationale

Sobakasu は Udon-first の static language であり、runtime に存在しない型機構をエミュレートするより、compile-time に concrete storage へ変換するほうが既存設計と整合する。型引数推論と substitution を Binder に集中させれば、Parser は構文、Lowerer は concrete aggregate operation、backend は scalar / typed-array / resolved extern emission という境界を維持できる。

constructed type cache と一つの再帰 substitution により、nested generic、array、method receiver を同じ identity 規則で扱える。具体化後に ADR-0021 へ接続することで、generic と non-generic aggregate が同一の layout、state validation、SoA、heap patch を共有する。

## Consequences

### Positive

* `Option<T>`、`Pair<T, U>` など再利用可能な型を記述できる。
* Udon に runtime generic support を要求しない。
* static type safety と nominal identity を維持できる。
* ADR-0021 の aggregate layout、SoA、state、method lowering を再利用できる。
* `Option.Some(42)` のような簡潔な inference を利用できる。
* generic aggregate の array、state、`pub`、`sync`、method を既存 system と統合できる。

### Negative

* constructed type 数に比例して compiler memory と生成 code size が増える。
* constructed type / method monomorphization cache が必要になる。
* inference と conflict / unresolved diagnostics が複雑になる。
* nested generic parsing に `>>` の contextual handling が必要になる。
* specialization、constraints、generic function は別の設計を必要とする。
* recursive generic instantiation と finite layout の検査が必要になる。
