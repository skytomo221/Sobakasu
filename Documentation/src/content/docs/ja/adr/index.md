---
title: ADR一覧
description: Sobakasu の Architecture Decision Records 一覧。
---

Sobakasu の Architecture Decision Records（ADR）の正本です。各 ADR は決定時点の記録であり、後続の決定によって変更された内容も当時の意味を保ったまま保存します。

| ADR番号 | タイトル | Status |
| --- | --- | --- |
| [ADR-0001](./adr-0001-record-architecture-decisions/) | Sobakasu用カスタム軽量ADRテンプレートの採用 | Accepted |
| [ADR-0002](./adr-0002-why-sobakasu/) | Sobakasuを開発する理由（Udonファースト高級言語・ツールチェーン） | Accepted |
| [ADR-0003](./adr-0003-compiler-pipeline-architecture/) | Sobakasu Compiler Pipeline Architecture | Accepted |
| [ADR-0004](./adr-0004-literal-syntax-and-types/) | Literal Syntax and Built-in Literal Types | Superseded by ADR-0005 (the `null` literal portion is additionally superseded by ADR-0026) |
| [ADR-0005](./adr-0005-64bit-numeric-types-and-literals/) | Primitive Type Expansion with Rust-Style Built-in Type Names | Accepted |
| [ADR-0006](./adr-0006-post-assemble-heap-patching/) | Post-Assemble Heap Patching for Typed Initial Values | Accepted |
| [ADR-0007](./adr-0007-local-variable-declarations-and-mutability/) | Local Variable Declarations and Mutability with Rust-Style `let` | Accepted (the `null` assignment exception is superseded by ADR-0026) |
| [ADR-0008](./adr-0008-use-imports-and-reflection-based-extern-resolution/) | use導入構文とreflectionベースextern解決の採用 | Accepted (partially superseded by ADR-0017) |
| [ADR-0009](./adr-0009-core-unary-and-binary-operators/) | 基礎 unary / binary operator と explicit short-circuit IR の採用 | Accepted (partially superseded by ADR-0016 and ADR-0020) |
| [ADR-0010](./adr-0010-udonsharp-compatible-event-handlers/) | UdonSharp互換イベントハンドラカタログの採用 | Proposed |
| [ADR-0011](./adr-0011-function-declarations-with-rust-style-fn/) | Rust-style `fn` Function Declarations | Accepted |
| [ADR-0012](./adr-0012-expression-oriented-control-flow/) | Expression-oriented `if`, `while`, and `loop` Control Flow | Accepted |
| [ADR-0013](./adr-0013-do-not-introduce-a-rust-style-ownership-system/) | Do Not Introduce a Rust-Style Ownership System | Accepted (null-safety direction extended by ADR-0026) |
| [ADR-0014](./adr-0014-top-level-state-bindings-public-variables-and-udon-synchronization/) | Top-level State Bindings, Public Variables, and Udon Synchronization | Superseded by ADR-0025 |
| [ADR-0015](./adr-0015-ruby-style-callable-names-and-optional-zero-argument-parentheses/) | Ruby-Style Callable Names and Optional Zero-Argument Parentheses | Accepted |
| [ADR-0016](./adr-0016-impl-external-type-bindings-extern-expressions-and-operator-overloading/) | Impl Blocks, External Type Bindings, Extern Expressions, and Operator Overloading | Accepted |
| [ADR-0017](./adr-0017-standard-library-modules-and-extern-boundary/) | Standard Library Modules and Extern Boundary | Accepted |
| [ADR-0018](./adr-0018-hierarchical-modules-implicit-prelude-re-exports-and-qualified-module-access/) | Hierarchical Modules, Implicit Prelude, Re-exports, and Qualified Module Access | Accepted |
| [ADR-0019](./adr-0019-built-in-object-type-for-udon-values/) | Built-in `object` Type for Udon Values | Accepted (partially superseded by ADR-0020 and ADR-0026) |
| [ADR-0020](./adr-0020-built-in-arrays-array-literals-repeat-construction-and-indexing/) | Built-in Arrays, Array Literals, Repeat Construction, and Indexing | Accepted (source-level `null` portions superseded by ADR-0026) |
| [ADR-0021](./adr-0021-user-defined-structs-payload-enums-and-flattened-aggregate-storage/) | User-Defined Structs, Payload Enums, and Flattened Aggregate Storage | Accepted |
| [ADR-0022](./adr-0022-generic-types-and-monomorphization/) | Generic Types and Monomorphization | Accepted |
| [ADR-0023](./adr-0023-match-expressions-and-enum-pattern-matching/) | Match Expressions and Enum Pattern Matching | Accepted (the source `null` pattern exclusion is superseded by ADR-0026) |
| [ADR-0024](./adr-0024-custom-network-event-receivers-and-send-syntax/) | Custom Network Event の受信宣言と送信構文 | Partially superseded by ADR-0057 (the bare `send` syntax) |
| [ADR-0025](./adr-0025-separate-compile-time-constants-persistent-state-and-local-bindings/) | Separate Compile-Time Constants, Persistent State, and Local Bindings | Accepted (typed source `null` initializer portions superseded by ADR-0026) |
| [ADR-0026](./adr-0026-eliminate-null-in-favor-of-maybe/) | Eliminate `null` in Favor of `Maybe<T>` | Accepted |
| [ADR-0027](./adr-0027-rust-style-use-trees-grouped-imports-and-glob-imports/) | Rust-style Use Trees, Grouped Imports, and Glob Imports | Accepted |
| [ADR-0028](./adr-0028-c-style-line-and-block-comments/) | C-style line and block comments | Accepted |
| [ADR-0029](./adr-0029-function-overloading-and-unified-overload-resolution/) | Function Overloading and Unified Overload Resolution | Accepted |
| [ADR-0030](./adr-0030-declarative-extern-bindings/) | Declarative Extern Bindings | Accepted |
| [ADR-0031](./adr-0031-introduce-tuples-and-adapt-extern-ref-out-parameters-to-value-returns/) | Introduce Tuples and Adapt Extern Ref/Out Parameters to Value Returns | Accepted |
| [ADR-0032](./adr-0032-add-maybe-out-for-nullable-extern-output-parameters/) | Add `maybe out` for Nullable Extern Output Parameters | Accepted |
| [ADR-0033](./adr-0033-apply-extern-parameter-projection-rules-uniformly-to-constructors/) | Apply Extern Parameter Projection Rules Uniformly to Constructors | Accepted |
| [ADR-0034](./adr-0034-make-udon-api-stub-generation-policy-driven/) | Make Udon API Stub Generation Policy-Driven | Superseded by ADR-0035 |
| [ADR-0035](./adr-0035-separate-udon-binding-generation-policy-v2/) | Separate Udon Binding Generation Policy v2 | Accepted |
| [ADR-0036](./adr-0036-treat-sobakasu-files-as-udon-program-sources/) | Treat .sobakasu Files as Udon Program Sources | Accepted |
| [ADR-0037](./adr-0037-decompose-sobakasu-binder/) | Decompose SobakasuBinder into composable binding components | Accepted |
| [ADR-0038](./adr-0038-disallow-source-initializers-for-public-state/) | Disallow Source Initializers for Public State | Accepted |
| [ADR-0039](./adr-0039-introduce-type-language-items-and-generation-config-v3/) | Introduce type language items and generation config v3 | Accepted |
| [ADR-0040](./adr-0040-external-struct-and-enum-bindings/) | External Struct and Enum Bindings | Accepted |
| [ADR-0041](./adr-0041-canonical-primitive-external-bindings/) | Canonical Primitive External Type Bindings | Accepted |
| [ADR-0042](./adr-0042-generic-udon-extern-methods-and-clr-type-patterns/) | Generic Udon Extern Methods and CLR Type Patterns | Accepted |
| [ADR-0043](./adr-0043-lazy-standard-library-module-and-re-export-resolution/) | Lazy Standard-Library Module and Re-export Resolution | Accepted |
| [ADR-0044](./adr-0044-declarative-operators-and-first-operand-hosting/) | Declarative Operators and First-Operand Hosting | Accepted |
| [ADR-0045](./adr-0045-external-nominal-type-declarations-and-udon-exposure-catalog/) | External Nominal Type Declarations and Udon Exposure Catalog | Accepted |
| [ADR-0046](./adr-0046-unicode-identifiers-and-uasm-symbol-boundary/) | Unicode Identifiers and the UASM Symbol Boundary | Accepted |
| [ADR-0047](./adr-0047-path-resolution-and-method-model/) | Path Resolution and Method Model | Accepted |
| [ADR-0048](./adr-0048-migrate-documentation-to-astro-starlight/) | Migration to a Multilingual Documentation Site with Astro/Starlight | Proposed |
| [ADR-0049](./adr-0049-integrate-adrs-into-official-documentation/) | ADRを公式Documentationへ統合する | Proposed |
| [ADR-0057](./adr-0057-behavior-callable-qualification-and-optional-state-capability/) | Behavior Callable Qualification and Optional State Capability | Accepted (ADR-0024 の send 構文と ADR-0054 の旧記述を supersede) |
