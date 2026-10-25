---
title: 'ADR-0061: Mutable Self Receiver Semantics'
---

## Status

Accepted

## Context

ADR-0047 により、Sobakasu の `implementation` 内では、先頭に専用 receiver parameter `self` を持つ `function` を instance method とするモデルを採用した。

```sobakasu
implementation Counter {
    function get(self) -> i32 {
        self.value
    }
}
```

しかし現在の `self` には可変性の区別がない。

そのため、次の2種類のメソッドを言語上区別できない。

```sobakasu
// receiver 自身の値を変更しない
function length(self) -> f32

// receiver 自身の値を変更する
function normalize(self)
```

Sobakasu では local binding を既定で immutable とし、値を書き換える場合は `mutable` を明示する。

```sobakasu
let value = 1;
let mutable counter = 0;
```

instance method についても同様に、呼び出しによって receiver 自身の値が変更される可能性がある場合、その性質を source 上で明示できる必要がある。

特に value semantics を持つ型では、

```sobakasu
let mutable vector = ...;
vector.normalize();
```

の呼び出しによって `vector` 自身の値が変更される。

一方、参照型に対して外部オブジェクトの状態を変更する操作、

```sobakasu
game_object.set_active(false);
```

は、`game_object` に保持されている参照値そのものを書き換えることとは異なる。

receiver の可変性は一般的な副作用の有無ではなく、**Sobakasu 上の receiver 自身の値が呼び出し前後で変更され得るか**を表す必要がある。

また、Sobakasu は Rust から一部の構文・考え方の影響を受けるが、ADR-0013 の方針どおり ownership、borrowing、lifetime system を導入することは目的としない。

## Decision

Sobakasu の instance method receiver に、`self` と `mutable self` の2種類を導入する。

```sobakasu
implementation Counter {
    function get(self) -> i32 {
        self.value
    }

    function increment(mutable self) {
        self.value += 1;
    }
}
```

### `self`

`self` は、メソッド呼び出しによって receiver 自身の Sobakasu 上の値を変更しない receiver を表す。

```sobakasu
function get(self) -> i32 {
    self.value
}
```

`self` を持つメソッドは immutable な binding からも呼び出せる。

```sobakasu
let counter = Counter::new();
let value = counter.get();
```

plain `self` を持つメソッドの本体から、`self` 自身または `self` に属する value field を書き換えてはならない。

```sobakasu
implementation Counter {
    function increment(self) {
        self.value += 1; // error
    }
}
```

### `mutable self`

`mutable self` は、メソッド呼び出しによって receiver 自身の Sobakasu 上の値が変更され得ることを表す。

```sobakasu
implementation Counter {
    function increment(mutable self) {
        self.value += 1;
    }
}
```

`mutable self` を持つメソッドは、変更後の値を書き戻せる receiver に対してのみ呼び出せる。

```sobakasu
let counter = Counter::new();
counter.increment(); // error

let mutable counter = Counter::new();
counter.increment(); // OK
```

メソッド内部で変更された `self` は、正常にメソッド呼び出しが完了したとき、呼び出し元の receiver へ書き戻される。

概念的には次の動作となる。

```text
receiver の現在値を読む
        ↓
self としてメソッドを実行
        ↓
変更後の self
        ↓
元の receiver へ書き戻す
```

これは source language 上で receiver を戻り値へ変換することを意味しない。

例えば、

```sobakasu
function increment(mutable self)
```

を内部実装の都合で、

```sobakasu
function increment(self) -> Self
```

のような source-level API に変換してはならない。

`mutable self` は instance method の receiver semantics として扱う。

### Writable receiver

`mutable self` の呼び出し対象は、変更後の値を同じ場所へ書き戻せる必要がある。

例えば次のような receiver は対象になり得る。

```sobakasu
let mutable value = ...;
value.mutate();

outer.inner.mutate();

values[index].mutate();
```

nested field や array element の場合も、メソッド呼び出し後の値を元の場所へ書き戻す。

一方、一時値など書き戻し先を持たない式から `mutable self` method を呼び出すことは許可しない。

```sobakasu
make_value().mutate(); // error
```

receiver location の評価は1回だけ行う。

例えば、

```sobakasu
values[index()].mutate();
```

で `index()` を読み出し時と書き戻し時の2回評価してはならない。

### Nested mutable calls

`mutable self` method から別の `mutable self` method を呼び出せる。

```sobakasu
implementation Foo {
    function inner(mutable self) {
        // ...
    }

    function outer(mutable self) {
        self.inner();
    }
}
```

plain `self` method から `self` を mutable receiver として渡すことはできない。

```sobakasu
implementation Foo {
    function inner(mutable self) {
        // ...
    }

    function outer(self) {
        self.inner(); // error
    }
}
```

### Reference object mutation

`mutable self` は一般的な副作用 annotation ではない。

参照型の receiver を通して、その参照先オブジェクトの内部状態を変更するだけであれば、receiver に保持されている参照値そのものは変更されない。

そのため、例えば次のような API は `self` のままでよい。

```sobakasu
function set_active(self, active: bool)
    = extern self.SetActive(active)
```

ここで変更されるのは参照先の GameObject の状態であり、Sobakasu の `self` に保持されている参照値そのものではない。

`mutable self` が必要なのは、メソッドの実行によって receiver 自身の Sobakasu 上の値が変更され得る場合である。

### Receiver syntax

receiver parameter の構文は次の2種類とする。

```text
self
mutable self
```

どちらも dedicated receiver parameter であり、通常の parameter ではない。

既存の receiver 規則を維持する。

- receiver は `implementation` method でのみ使用できる。
- receiver は parameter list の先頭でなければならない。
- receiver は1つだけ指定できる。
- receiver に型注釈は付けない。
- `self: T` および `mutable self: T` は不正とする。
- `mutable value: T` のような一般 parameter の可変性はこの決定では導入しない。

### Method identity

receiver mutability は overload identity に含めない。

したがって、次の2つを mutability の違いだけで overload することはできない。

```sobakasu
function foo(self) {
}

function foo(mutable self) {
}
```

`self` と `mutable self` は同じ instance method signature に対する異なる receiver contract であり、別 overload ではない。

### Ownership and borrowing

この決定によって Rust-style ownership system は導入しない。

導入しないものには少なくとも以下を含む。

- ownership tracking
- borrow checker
- shared / exclusive borrow analysis
- lifetime
- `&self`
- `&mut self`
- alias analysis による排他性保証

`mutable self` が保証するのは、receiver の変更可能性と呼び出し元への write-back だけである。

## Alternatives

### 1. すべての `self` を常に mutable とする

却下する。

immutable binding から呼び出したメソッドによって値が暗黙に変更されることになり、Sobakasu の immutable-by-default な binding model と一致しない。

また、メソッド declaration を見ても receiver を変更するか判断できなくなる。

### 2. `self` を常に immutable とし、変更後の値を戻り値として返す

例えば、

```sobakasu
function normalize(self) -> Self
```

として caller 側で、

```sobakasu
vector = vector.normalize();
```

とする案である。

却下する。

値を書き換える instance method を表現するたびに API shape を変更する必要があり、外部 API との binding も不自然になる。

また、receiver mutability と通常の return value を同じ仕組みに押し込むことになる。

### 3. `mutable` ではなく `ref self` や `out self` を導入する

却下する。

`ref` / `out` は extern ABI parameter projection と receiver mutability では異なる概念である。

Sobakasu source 上の可変性は既に `mutable` という語彙を使用しており、receiver についても同じ語を使う方が一貫している。

### 4. Rust と同様に `&self` / `&mut self` を導入する

却下する。

Sobakasu は ownership、borrowing、lifetime system を導入しない。

receiver mutability の表現だけを目的として参照・借用モデル全体を導入する必要はない。

### 5. receiver mutability を compiler が自動推論する

source language の user-defined method については却下する。

メソッド declaration に receiver mutation contract が現れなくなり、呼び出し側の可変性判定も実装詳細に依存する。

user-defined Sobakasu method では `self` / `mutable self` を明示する。

外部 API から生成する standard library における自動推定については別の generation policy として扱い、この ADR では決定しない。

## Rationale

Sobakasu は local binding において immutable を既定とし、変更可能な binding にのみ `mutable` を明示する。

instance method でも同じ原則を適用すると、

```sobakasu
function length(self)
function normalize(mutable self)
```

という declaration だけで、そのメソッドが receiver 自身の値を変更し得るかを判断できる。

これにより、

```sobakasu
let value = ...;
```

と

```sobakasu
let mutable value = ...;
```

の違いを method invocation にも一貫して適用できる。

また、receiver の値の変更と、参照先オブジェクトに対する副作用を分離することで、C# / CLR の value type / reference type の詳細を Sobakasu の source semantics へ直接持ち込まずに済む。

Sobakasu のユーザーが意識するのは、

> このメソッドは `self` 自身の値を変更するか

という言語上の意味だけである。

CLR/Udon API の method がどちらに対応するかを standard library generator が推定する仕組みは、この source semantics の上に別途構築できる。

## Consequences

### Positive

- instance method が receiver 自身を変更するか source 上で明示できる。
- immutable binding から意図せず値が変更されることを防げる。
- local binding の `mutable` と method receiver の mutability が一貫する。
- value type を変更する外部 API を自然な instance method として表現できる。
- reference object の副作用と receiver 自身の変更を区別できる。
- CLR / C# の value/reference semantics を Sobakasu 利用者から隠すための明確な言語境界になる。
- ownership / borrowing systemを導入せずに receiver mutability を表現できる。
- 将来の Standard Library Generator が C# / Udon API を `self` / `mutable self` に投影するための基準が明確になる。

### Negative

- Binder は mutable receiver call に対して receiver が書き戻し可能か検査する必要がある。
- Lowering は mutable receiver の変更後の値を元の location へ書き戻す必要がある。
- field、nested field、array element 等に対して、読み出しと書き戻しを一貫して扱う writable-location の仕組みが必要になる。
- receiver location を1回だけ評価するため、lowering の評価順管理が複雑になる。
- 既存の plain `self` method 内で `self` の field を変更していたコードは `mutable self` へ移行する必要がある。
- ADR-0047 や ADR-0054 にある plain `self` から receiver field を変更する例は、この ADR の receiver mutability 規則によって supersede される。

この ADR は Sobakasu source language の receiver semantics のみを決定する。

Standard Library Generator における、

- CLR value type method を `mutable self` と推定する規則
- readonly receiver の例外設定
- generation config schema の変更
- generated standard library の更新

は後続の決定で扱う。
