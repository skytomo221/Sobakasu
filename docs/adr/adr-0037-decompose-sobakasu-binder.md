# ADR-0037: Decompose SobakasuBinder into composable binding components

## Status

Accepted

## Context

`SobakasuBinder` は Binder フェーズの公開入口である一方、binding pipeline の制御、module・import・prelude・visibility の解決、symbol table、型・関数・定数・state・event・network receive の宣言と binding、statement と expression の走査、名前・型・member・overload・operator・conversion の解決、generic inference・substitution・instantiation、extern 解決、定数評価と依存関係、aggregate・constructed type・再帰関数の検証、scope・loop と current module/type/function 等の一時状態を単一クラスへ集約していた。

この構造では、新機能の変更範囲が広がり、寿命も目的も異なる mutable state が共有され、個別アルゴリズムの理解とテストが難しくなる。特定機能の調査にも約一万行のクラス全体を参照する必要があり、言語機能の増加とともに責務集中が悪化する。

目的はファイルサイズだけを減らすことではなく、Binder を明確な責務境界を持つ composable なサブシステムへ再構成することである。

本決定は ADR-0003 のコンパイラパイプラインを変更しない。

```text
Lexer -> Parser -> Binder -> Desugar -> IrLowerer -> Optimizer -> UasmAssembler
```

Binder は引き続き名前解決、型付け、暗黙変換の明示化、symbol 解決を担う単一の compiler phase である。変更対象は Binder 内部の実装アーキテクチャに限る。また、extern、module/use/prelude、aggregate、generics、overload、tuple、ref/out、constant/state、network event を定める既存 ADR の意味論は変更しない。

## Decision

### SobakasuBinder is a facade

`SobakasuBinder` は環境を保持し、`BindProgram` ごとに `BindingSession` を生成し、binding pipeline を開始して `BoundProgram` を返す facade とする。公開 entry point、`Diagnostics`、最後に bind した module symbols は維持する。個別の意味解析アルゴリズム、大量の symbol dictionary、expression switch、overload・generic・extern・module lookup、program-wide validation は置かない。

`partial SobakasuBinder` は使用しない。責務は別の具象クラスへ移す。

### Session and state ownership

1 回の `BindProgram` に 1 個の `BindingSession` を生成する。前回の bind で使った dictionary、list、set を `Clear()` して再利用しない。

`BindingSession` は per-bind の composition root であり、意味解析アルゴリズム自体は持たない。状態は次の専用 owner に分ける。

```text
BindingSession
├─ ModuleBindingState       module tables, imports, aliases, prelude, current module
├─ DeclarationBindingState aggregate/type/member declaration tables
├─ CallableBindingState    function, method, event/receive-related symbols
├─ ConstantBindingState    declaration order, dependency/binding state, bound constants
├─ GenericBindingState     generic scopes, templates, pending instantiations
├─ BodyBindingContext      scope, loop stack, current type/function/return/event
└─ DiagnosticBag
```

`BodyBindingContext` は function、event、receive、extern binding expression を bind する入口で新しく生成し、終了時に以前の context を復元する。compiler-wide state と body-local state の寿命を分け、`BindingSession` 自体を dictionary とアルゴリズムの新しい God Object にしない。

### Binding phases and declaration binders

pipeline の順序は phase component から読み取れる形にする。module table 初期化、aggregate type collection/binding、callable signature collection、constant/state binding、body binding、program-wide validation をそれぞれ専用 component が実行する。

宣言処理は aggregate、callable、extern declaration、constant、state、event/receive の責務へ分ける。即時診断は syntax を bind する component に残し、aggregate dependency、constructed aggregate、recursive function 等の program-wide 検証は validation component が担当する。

### Syntax binding and semantic resolution

statement/expression binder は syntax traversal と Bound Node 構築を担当する。名前、型、member、call overload、operator、conversion、extern target の候補探索・選択は resolution component が担当する。

概念上の依存は次のとおりである。

```text
Expression/Call binders
├─ NameResolver / TypeResolver / MemberResolver
├─ OverloadResolver / OperatorResolver / ConversionClassifier
├─ GenericInference / GenericSubstitution / GenericInstantiation
└─ ExternResolver
```

call binder や binary-expression binder にこれらの semantic algorithm を再実装しない。

### Generics, externs, modules, constants, and validation

generic inference、type substitution、constructed type/method instantiation は別 component とする。generic semantics と monomorphization は ADR-0022 のまま維持する。

extern type/receiver/method/operator/constructor と ABI projection の解決は extern component が `ExternCatalog` を利用して行う。`ExternCatalog` 自体は再設計しない。declarative extern、Maybe、ref/out、constructor projection の意味論は維持する。

module graph、alias/direct/glob import、prelude、visibility、visible symbol resolution は module/resolution component を介する。expression binder が import table の組み立てを担当しない。

constant の宣言収集・binding state・cycle detection と compile-time evaluation は分け、state binder は確定した constant evaluator を利用する。program-wide validation は syntax binding の後に専用 component で実施する。

### Class design

具象実装が一つで差し替え要件もない責務へ機械的な interface を作らない。DI framework と global service locator は導入しない。component は per-session の型付き composition とし、依存する state と collaborator をコード上で明示する。主要型は原則 1 ファイルとし、空 wrapper は作らない。

## Alternatives

### 1. 現在の単一 SobakasuBinder を維持する

今後の機能追加でさらに巨大化し、unrelated concern が mutable state を共有し続ける。変更影響範囲が広く、局所的な理解とテストも難しいため却下する。

### 2. partial class で SobakasuBinder を複数ファイルに分割する

ファイルサイズが減り、エディタやコード検索から対象箇所へ到達しやすくなる利点はある。しかし class としての責務集中、mutable state、internal coupling は変わらず、God Object を複数ファイルへ広げるだけになるため却下する。

### 3. Binder を compiler pipeline 上の複数 stage へ分割する

`Resolver`、`TypeChecker`、`Binder` 等を独立 phase にする案である。ADR-0003 が定める Binder という意味解析境界自体には問題がなく、必要なのは pipeline の変更ではなく内部 architecture の改善なので今回は却下する。

## Rationale

Sobakasu は今後も言語機能が増えるため、単一 Binder では責務集中が継続する。syntax binding と semantic resolution を分ければ、構文追加と解決規則の変更を局所化できる。generic、extern、overload resolution を独立させれば、それぞれの複雑化を Binder 全体へ波及させずに済む。

compiler-wide state と body-local state を分けることで mutable state の owner と寿命が明確になる。Binder という既存 compiler phase は維持するため ADR-0003 と整合する。機能ごとの実装位置が明確になり、人間、IDE、コード検索、AI coding agent のいずれも必要なコードだけを調査しやすくなる。AI の context 削減は副次的利点であり、主目的は責務分離、保守性、将来の拡張性である。

## Consequences

### Positive

* 責務境界と mutable state の owner が明確になる。
* 新機能を局所的に実装し、対象を絞ってテストしやすくなる。
* overload、generics、extern 等を独立して発展させやすくなる。
* 巨大な `SobakasuBinder` 全体を読む必要が減る。
* 人間と AI coding agent のコード探索時に必要な context を減らしやすい。
* Binder の既存 externally observable behavior と pipeline 境界を維持できる。

### Negative

* class と file、Unity `.meta` の数が大幅に増える。
* component 間依存を継続して設計・監査する必要がある。
* 過剰分割すると処理の追跡が難しくなる。
* 単純な処理でも複数ファイルを辿る場合がある。

これらを許容する。Sobakasu がさらに複雑になることを前提に、短期的なファイル数の少なさより長期的な責務分離を優先する。
