# ADR-0052: Separate Compiler Core from Unity Editor Assembly

## Status

Accepted

## Context

ADR-0051 により、Sobakasu Compiler が Udon API を解決するための metadata は `udon-api-catalog.json` を正本とし、通常の compilation path では Unity / VRChat SDK に対する Reflection discovery を行わない構造を採用した。

その後の実装により、Compiler Core の通常処理は catalog-backed な `ExternCatalog` と `SobakasuCompilationEnvironment` を使用するようになり、Udon API の型解決、member 解決、operator 解決、generic constraint、enum constant、array capability、synchronization capability などを Unity / VRChat SDK から直接取得する必要がなくなった。

また、runtime type は Compiler Core 内では `RuntimeTypeIdentity` などの symbolic representation として扱い、実際の CLR `System.Type` や runtime value への materialization は Unity integration 側の boundary で行う構造になっている。

しかし、Compiler Core は現在も物理的には Unity Editor 用 assembly `Sobakasu.Editor` の一部としてコンパイルされている。

概念的には以下の状態である。

```text
Sobakasu.Editor
 ├─ Compiler Core
 │  ├─ Lexer
 │  ├─ Parser
 │  ├─ Binder
 │  ├─ Desugar
 │  ├─ IR
 │  ├─ Optimizer
 │  ├─ UASM Assembler
 │  └─ Udon API catalog loader
 │
 ├─ Unity importer
 ├─ ProgramAsset
 ├─ runtime materialization
 ├─ Udon API discovery
 ├─ Udon API Catalog Generator
 └─ Standard Library Generator
```

この状態では、Compiler Core の実装自体が Unity / VRChat API を使用していなくても、assembly および test runner の単位では Unity Editor project に依存している。

そのため、Compiler Core の変更を検証するために Unity Test Framework を使用する必要があり、通常の .NET tooling だけで Compiler の build / test を完結できない。

特に AI coding agent に Compiler の変更を実装させる場合、Unity を起動せずに

```text
implementation
    ↓
dotnet build
    ↓
dotnet test
    ↓
failure analysis
    ↓
fix
```

という反復を行えることには大きな利点がある。

Compiler Core の semantic dependency はすでに Unity / VRChat から分離されているため、次に assembly、project、test boundary を物理的にも分離する。

## Decision

Compiler Core を独立した `Sobakasu.Compiler` assembly として分離する。

Unity Editor / VRChat SDK integration は `Sobakasu.Editor` assembly に残す。

依存方向は以下の一方向とする。

```text
Sobakasu.Editor
        ↓
Sobakasu.Compiler
```

`Sobakasu.Compiler` から `Sobakasu.Editor` への依存は許可しない。

### Compiler assembly

以下を Compiler Core とする。

```text
Lexer
Parser
Binder
Desugar
IR
IR Lowerer
Optimizer
UASM Assembler
Diagnostics
module resolution
semantic model
ExternCatalog
SobakasuCompilationEnvironment
RuntimeTypeIdentity
Udon API catalog persisted model / loader
```

Compiler Core は `Sobakasu.Compiler` assembly でコンパイルする。

Unity 用 asmdef は概念的に以下の制約を持つ。

```text
name = Sobakasu.Compiler
Unity / VRChat assembly references = none
noEngineReferences = true
```

`Sobakasu.Compiler` から以下を参照してはならない。

```text
UnityEngine
UnityEditor
VRC.*
VRC.Udon
VRC.Udon.Editor
```

Unity / VRChat API を Compiler assembly に追加して build error を解消することは認めない。

Compiler Core が target-specific な情報を必要とする場合は、ADR-0051 で定義した catalog metadata や既存の explicit compilation environment boundary を使用する。

### Unity integration assembly

Unity Editor integration は `Sobakasu.Editor` assembly とする。

以下は `Sobakasu.Editor` 側の責務とする。

```text
ScriptedImporter
ProgramAsset
ProgramCompiler integration
runtime value materialization
runtime value serialization
Unity compilation environment provider
Udon API discovery
Udon API Catalog Generator
Standard Library Generator
Editor Window
VRChat SDK integration
```

`Sobakasu.Editor` は `Sobakasu.Compiler` を参照できる。

既存の Editor asmdef の assembly name は `Sobakasu.Editor` を維持する。

asmdef のファイル名も assembly name と一致させることを推奨する。

### Compiler source location は変更しない

Compiler Core の source files は P3 では移動しない。

引き続き以下を Compiler Core の source location とする。

```text
Packages/com.skytomo221.sobakasu/Editor/Compiler/
```

`src/Sobakasu.Compiler/` など Unity package 外の directory へ source tree を移動しない。

P3 の目的は source directory の再編ではなく、assembly / project / dependency / test boundary の分離である。

大規模な source relocation は `.meta`、Git history、Unity package layout、build configuration など本決定とは無関係な差分を増加させるため行わない。

### `.meta` files を維持する

Compiler source は引き続き Unity package 内に存在するため、Unity package asset として必要な `.meta` files は維持する。

既存 `.meta` files を「Compiler Core が Unity 非依存になった」という理由で削除しない。

Unity package 内で file、directory、asmdef を追加、移動、rename する場合は、対応する `.meta` も通常どおり管理する。

既存 asset を移動または rename する場合は、可能な限り既存 `.meta` を同時に移動し GUID を維持する。

`.meta` の存在は、

```text
Sobakasu.Compiler → Unity
```

という code dependency を意味しない。

`.meta` は Unity Asset Database / package asset management の metadata であり、C# assembly reference や Compiler runtime dependency とは独立した概念である。

Compiler Core の Unity 非依存性は `.meta` の有無ではなく、assembly references と独立 .NET build によって保証する。

### 同一 source を .NET project でも build する

Compiler Core には SDK-style .NET project を用意する。

.NET project は Unity 用 Compiler implementation のコピーを持たず、Unity asmdef が使用するものと同じ

```text
Packages/com.skytomo221.sobakasu/Editor/Compiler/
```

配下の C# source files をコンパイルする。

概念的には以下とする。

```text
                         ┌─ Sobakasu.Compiler.asmdef
Compiler source files ──┤
                         └─ Sobakasu.Compiler.csproj
```

Unity 用 implementation と .NET 用 implementation を別々に維持してはならない。

Compiler の third-party .NET dependency が必要な場合は、SDK-style project 側で明示的に依存関係を宣言する。

UnityEngine、UnityEditor、VRChat SDK assembly を `.csproj` から参照してはならない。

Compiler project は Unity project、Unity installation、VRChat SDK が存在しない通常の .NET environment で build 可能でなければならない。

### Prebuilt Compiler DLL を Unity package の正本としない

Compiler source を Unity package 外へ移動し、事前に build した `Sobakasu.Compiler.dll` のみを Unity package から参照する方式は採用しない。

Unity と .NET の双方が同じ Compiler source を直接 compile する。

これにより、source と prebuilt DLL の同期、DLL generation、packageへのbinary同梱などの追加 build pipeline を必要としない構造を維持する。

### Compiler test と Unity integration test を分離する

Compiler Core の unit / behavior tests は `Sobakasu.Compiler.Tests` として通常の .NET test project から実行可能にする。

Compiler Core に属する代表的な test scope は以下とする。

```text
Lexer
Parser
Binder
type system
module resolution
Desugar
IR
IR Lowerer
Optimizer
UASM Assembler
ExternCatalog
UdonApiCatalogLoader
Compiler pipeline
```

これらの通常の test runner は `dotnet test` とする。

一方、以下のように Unity Editor または VRChat SDK を実際に必要とする test は `Sobakasu.Editor.Tests` に残し、Unity Test Framework で実行する。

```text
ScriptedImporter integration
ProgramAsset integration
runtime materialization
Unity asset behavior
Udon API discovery
Udon API Catalog Generator
Standard Library Generator
VRChat SDK integration
```

最終的な test boundary は概念的に以下とする。

```text
Sobakasu.Compiler.Tests
        ↓
Sobakasu.Compiler
        ↑
Sobakasu.Editor
        ↑
Sobakasu.Editor.Tests
```

Compiler test を .NET test project へ移行したという理由だけで、必要な Unity integration tests を削除してはならない。

### AI coding agent から Compiler tests を実行可能にする

Compiler Core の変更は、Unity Editor を使用せず通常の .NET CLI だけで build / test できることを設計上の要件とする。

Compiler Core に対する通常の verification command は以下を基本とする。

```text
dotnet build
dotnet test
```

これにより、AI coding agent を含む Unity Editor を直接操作できない開発環境でも、Compiler の実装、test、failure analysis、修正を一連の作業として完結できるようにする。

Unity / VRChat integration を変更した場合は、これとは別に既存の Unity Test Framework による integration test を実行する。

### Assembly internal boundary

Assembly separation によって Unity integration code から Compiler の `internal` implementation detail へアクセスできなくなった場合、その解決策として `InternalsVisibleTo("Sobakasu.Editor")` を安易に導入しない。

原則は以下とする。

```text
不要になった旧integration
    → 削除する

Unity integrationから本当に必要なCompiler operation
    → 明示的なCompiler APIとして公開する

Compiler implementation detail
    → internalのまま維持する
```

Test assembly に対する `InternalsVisibleTo` は許容する。

たとえば Compiler tests が Compiler internals を検証する必要がある場合、

```text
InternalsVisibleTo("Sobakasu.Compiler.Tests")
```

を使用できる。

移行途中で既存 Compiler tests がまだ `Sobakasu.Editor.Tests` に存在する場合、その test assembly に対する一時的な visibility は許容するが、最終的な test separation 後は不要な visibility を残さない。

### Deprecated Reflection-based compiler integration を延命しない

P2以前の compatibility のために残っている Reflection-based extern catalog construction や、それを支える Compiler compatibility shim が assembly separation の障害になる場合、通常の compilation path ですでに不要であれば削除する。

特に、廃止済みの Reflection-based implementation を維持する目的で Compiler internals を `Sobakasu.Editor` へ公開してはならない。

Udon API discovery に必要な Reflection logic は Unity / Tools 側の責務とし、Compiler Core の semantic model を Reflection-backed construction のために再び公開しない。

### Completion conditions

本決定の実装は、少なくとも以下をすべて満たした時点で完了とする。

```text
Sobakasu.Compiler が独立 assembly になっている。

Sobakasu.Editor → Sobakasu.Compiler の一方向依存になっている。

Sobakasu.Compiler が UnityEngine / UnityEditor / VRC.* assembly を参照していない。

Sobakasu.Compiler が noEngineReferences = true で Unity project 内でもcompileできる。

同じ Compiler source を SDK-style .NET project から buildできる。

Unity / VRChat SDKなしで dotnet build が成功する。

Compiler Core testsを dotnet test で実行できる。

dotnet test が成功する。

Unity / VRChat integration testsは Unity Test Framework に残っている。

既存の Unity test suite が成功する。
```

## Alternatives

### Compiler Core を引き続き `Sobakasu.Editor` assembly に置く

採用しない。

P2によって semantic dependency を Unity / VRChat から除去しても、assembly と test runner が Unity Editor に固定されたままでは Compiler Core を通常の .NET project として独立して build / test できない。

特に Compiler の変更を AI coding agent が自己完結して検証できない状態が続く。

### Compiler source を `src/Sobakasu.Compiler/` へ移動する

採用しない。

Compiler Core の Unity 非依存性に source tree の移動は必要ない。

P3で必要なのは assembly / project dependency の分離であり、source location の変更ではない。

source relocation は大量の `.meta` 移動、Unity package layout の変更、Git diff の増大など、本決定に不要な作業を導入する。

将来 Compiler を別package、NuGet package、別repositoryなどとして独立配布する要件が生じた場合は、別の architecture decision として検討する。

### Compiler source を Unity package 外へ移し、prebuilt DLL を Unity から参照する

採用しない。

Compiler DLL の generation、binary distribution、sourceとの同期、package更新など追加の build / release pipeline が必要になる。

現時点では、同じ Compiler source を Unity asmdef と SDK-style .NET project の双方から直接 compile する方が単純である。

### `.meta` files を Compiler source から削除する

採用しない。

Compiler source は Unity package 内に存在し続けるため、`.meta` は Unity asset management 上必要な metadata である。

`.meta` の削除は Compiler の Unity code dependency を除去することにはならず、Unity上のasset identityを不必要に失うだけである。

### `InternalsVisibleTo("Sobakasu.Editor")` で assembly separation を通す

原則として採用しない。

これを一般的な integration mechanism とすると、assembly は分かれていても `Sobakasu.Editor` が Compiler implementation detail に広範囲に依存でき、physical boundary の意味が弱くなる。

必要な interaction は明示的な Compiler API として設計する。

### Unity Test Framework を Compiler Core の主要test runnerとして維持する

採用しない。

Compiler Core 自体は Unity / VRChat に依存しないため、その通常testまで Unity Editor の起動に依存させる理由がない。

Compiler Core tests は通常の `dotnet test` で実行可能にし、Unity Test Framework は Unity / VRChat integration の検証に限定する。

### .NET 用に Compiler implementation を複製する

採用しない。

Unity と .NET の implementation が分岐し、同じ Compiler を検証している保証が失われる。

同一 source set を両方の build system から compile する。

## Rationale

P2によって Compiler Core の semantic dependency は Unity / VRChat から切り離された。

その状態で physical assembly と build/test project まで分離することで、「Unity APIを使用していない」という実装上の慣習ではなく、assembly dependency と独立 .NET build によって Compiler Core の独立性を機械的に保証できる。

`Sobakasu.Compiler` に Unity / VRChat references を持たせず `noEngineReferences` を有効にすることで、将来 Compiler Core に Unity dependency が誤って再導入された場合も build failure として検出できる。

一方、Compiler source 自体を Unity package 外へ移す必要はない。

source location、Unity asset metadata、C# code dependency は別の概念である。

Compiler source を Unity package 内に維持することで、既存package layoutを大きく変更せず、Unityと.NETの双方から同一sourceをcompileできる。

また Compiler Core tests を `dotnet test` へ移行することで、Unity Editorを起動できない環境でも高速かつ反復的に Compiler の実装を検証できる。

これは通常の開発だけでなく、AI coding agent が implementation → test → failure analysis → fix を自己完結して行える開発フローを実現する上でも重要である。

Unity Test Framework は廃止せず、Unity importer、asset、runtime materialization、catalog generation、VRChat SDK integration など、本当に Unity environment を必要とする integration tests に責務を限定する。

## Consequences

### Positive

- Compiler Core の Unity / VRChat 非依存性を assembly boundary で強制できる。
- Unity / VRChat dependency の Compiler Core への再混入を build error として検出できる。
- Compiler Core を Unity なしで `dotnet build` できる。
- Compiler Core tests を `dotnet test` で実行できる。
- AI coding agent が Compiler の実装から test・修正まで自己完結しやすくなる。
- Unity Test Framework の実行を必要とする変更範囲を Unity integration に限定できる。
- Unity と .NET で Compiler implementation を二重管理せずに済む。
- 既存の Unity package source layout を維持できる。
- 大量の source / `.meta` relocation を避けられる。
- Compiler と Unity integration の責務境界が assembly dependency graph と一致する。
- 将来 Compiler を独立package等へ発展させる場合の基盤になる。

### Negative

- Unity asmdef と SDK-style `.csproj` の2つの build definition を維持する必要がある。
- Compiler source が Unity package 配下に存在するため、Unity非依存であっても `.meta` files は引き続き管理する必要がある。
- 既存 Compiler tests を Unity Test Framework から .NET test project へ整理する作業が必要になる。
- assembly separation により、これまで同一 assembly で許されていた `internal` access が compile error として表面化する。
- 必要な integration API と implementation detail の境界を整理する必要がある。
- Compiler と Unity integration の両方に関係する変更では、`dotnet test` と Unity Test Framework の双方を実行する必要がある。
