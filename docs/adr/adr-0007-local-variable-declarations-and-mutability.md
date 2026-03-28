# ADR-0007: Local Variable Declarations and Mutability with Rust-Style `let`

## Status

Accepted

## Context

Sobakasu は Udon-first の言語であり、UdonSharp 利用者が違和感なく使える命令型フローを重視している。
ADR-0003 により frontend / IR / backend の責務分離がすでに採用されており、構文追加は lexer / parser / binder / lowering / backend の各層を通して一貫して扱う必要がある。

ADR-0005 と ADR-0006 によって primitive literal と typed value の取り扱いは前進したが、block 内で値に名前を付けて再利用する local binding は未整備のままだった。
これまでの statement は実質的に expression statement 中心であり、中間結果の保持、再利用、段階的な書き換えを支える基礎として local variable declaration が不足していた。

## Decision

Sobakasu は block scope の local variable declaration として Rust 風の `let` を採用する。

採用する構文は次の 4 形に限定する。

```sobakasu
let x = expr;
let x: T = expr;
let mut x = expr;
let mut x: T = expr;
```

初期化子は必須とする。
型注釈は省略可能とし、省略時は initializer から型推論する。
型注釈がある場合は binder が initializer の型適合性を検証する。

local binding の既定は immutable とする。
再代入を許可したい場合にのみ `mut` を付ける。
`mut` のない束縛への再代入は診断とする。

shadowing は許可する。
同名の `let` を後続位置に書いた場合は既存束縛への再代入ではなく、新しい束縛として扱う。

この ADR の対象は local variable declaration のみとする。
top-level field 宣言、member 宣言、pattern binding、destructuring、multi-declaration は対象外とする。

v1 の lowering では local を concrete typed slot に下ろす。
local store 規則は exact-type store を基本とし、reference type への `null` 代入のみ例外として許可する。

## Alternatives

### 1. C# 風の `int x = 1;` / `var x = 1;` を採用する

却下した。
Sobakasu は C# 互換を目的とする言語ではなく、型名も `i32`, `f32`, `f64` など Rust 風の命名を採用している。
このため declaration syntax だけ C# 風に寄せると、言語全体の一貫性が崩れる。

### 2. `let` は導入するが `mut` を持たず常に再代入可能にする

却下した。
immutable default を外すと accidental reassignment を減らすという利点が失われる。
また binder が mutability を前提に診断できなくなり、意図しない代入を静的に拾いにくくなる。

### 3. shadowing を禁止する

却下した。
block 単位の書き換えや一時値導入では、同名の値を段階的に再束縛できるほうが自然である。
Rust 風 `let` を採用する以上、shadowing を許可するほうが構文と意味論の整合性が高い。

### 4. local variable より先に field 宣言を導入する

却下した。
field は lifetime、serialization、initial value、backend layout などの設計面積が大きい。
一方 local binding は block scope 内で閉じており、制御構文や式評価の基礎を先に固める題材として適している。

## Rationale

Sobakasu は C# 互換を目的としないため、Rust 風の `let` は言語方針と整合する。
型名が `i32`, `f32`, `f64` など Rust 風であることからも、`let` を採用したほうが全体の読み味が自然になる。

immutable default により accidental reassignment を減らせる。
再代入が必要な箇所だけ `mut` を明示させることで、binder の診断品質も上げやすい。

shadowing を許可すると block 単位の書き換え、一時値導入、ネストした scope での自然な再束縛がしやすくなる。
一方で top-level field や pattern binding まで同時に広げると v1 としては範囲が大きすぎるため、今回は local variable のみに限定する。

## Consequences

### Positive

block 内で中間結果を名前付きで保持できる。
binder に scope / symbol / mutability という基礎が入り、今後の機能追加に再利用できる。
制御構文やユーザー定義型を導入する際の土台になる。
frontend から backend まで local binding を一貫して扱う経路ができる。

### Negative

scope 管理と shadowing により binder が複雑になる。
assignment を含めることで式 / 文の設計面積が増える。
field 宣言や destructuring など未対応領域との境界を今後整理する必要がある。
v1 は concrete typed local slots と exact-type local store を採るため、より広い implicit conversion は将来の課題として残る。
