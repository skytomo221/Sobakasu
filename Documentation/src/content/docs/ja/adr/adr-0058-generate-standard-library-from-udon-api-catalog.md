---
title: 'ADR-0058: Generate Standard Library from the Udon API Catalog'
---

## Status

Proposed

## Context

ADR-0051 では、Udon API discovery と Standard Library Generator を分離し、`udon-api-catalog.json` を Compiler が Udon API を解決するための生成済み metadata として導入した。

その時点では、Udon API discovery によって構築された共通の physical API model を、Udon API Catalog Generator と Standard Library Generator の双方が利用する構成を採用していた。

概念的には以下である。

```text
Unity / VRChat SDK
        ↓
   UdonApiDiscovery
       / \
      /   \
     ↓     ↓
Udon API   Standard Library
Catalog    Generator
Generator
     ↓
udon-api-catalog.json
```

この構成では Compiler の通常処理から Unity / VRChat SDK dependency を除去できる一方、Standard Library Generator は引き続き Unity Editor 内で CLR Reflection、VRChat Udon exposure 情報、および `UdonApiDiscovery` を使用する必要がある。

そのため、Standard Library の再生成には Unity Editor の起動が必要であり、現在の `Scripts/run-standard-library-generator.ps1` も Unity を batch mode で起動して Generator を実行している。

一方、ADR-0051 以降の実装によって、`udon-api-catalog.json` にはすでに次のような physical Udon API metadata が保存されている。

```text
runtime type
type shape
supertype
enum metadata
member name
member kind
static / instance
CLR signature
Udon extern signature
generic parameter
ABI parameter
ABI return type
array capability
synchronization capability
unmatched Udon signature
```

また ADR-0052 によって Compiler Core は Unity Editor assembly から分離され、同一 Compiler source を通常の .NET project から build / test できるようになっている。

Standard Library の生成に必要なのは、インストールされた Unity / VRChat SDK を毎回 discovery することではない。

必要なのは、

```text
physical Udon API metadata
Standard Library generation policy
manual additions
```

である。

physical Udon API metadata が `udon-api-catalog.json` として既に永続化されている以上、Standard Library Generator が同じ SDK に対して再度 Reflection discovery を行うことは、metadata source が二重化した状態になる。

また Catalog 更新と Standard Library 更新には異なる意味がある。

Catalog 更新は、

```text
Unity / VRChat SDK の現在の Udon API surface を取得する操作
```

である。

Standard Library 更新は、

```text
既存の physical Udon API metadata に
Sobakasu の naming / placement / Maybe / prelude 等の policy を適用する操作
```

である。

この2つを同じ実行経路に含める必要はない。

特に Standard Library の生成スクリプトを実行するたびに Catalog まで更新すると、SDK metadata の変更と Standard Library policy の変更が同じ差分に混在し、Catalog 更新を意図せず発生させる。

さらに、Standard Library Generator を AI coding agent を含む通常の .NET 環境から実行・テストするには、生成処理そのものを Unity / VRChat SDK から分離する必要がある。

## Decision

Standard Library Generator の physical Udon API input を `udon-api-catalog.json` に変更する。

今後の構造を以下とする。

```text
                 手動更新
Unity / VRChat SDK
        ↓
   UdonApiDiscovery
        ↓
UdonApiCatalogGenerator
        ↓
udon-api-catalog.json
        │
        ├──────────────→ Sobakasu Compiler
        │
        └──────────────→ Standard Library Generator
                              ↑
                 generation configuration
                 StandardLibraryAdditions~
                              │
                              ↓
                       StandardLibrary~
```

### `udon-api-catalog.json` を physical API metadata の正本とする

`udon-api-catalog.json` は Compiler だけでなく Standard Library Generator にとっても、Udon target の physical API metadata の正本とする。

Standard Library Generator は通常の生成処理で以下を行わない。

```text
CLR assembly scanning
System.Type を起点とした API discovery
MethodInfo / FieldInfo / PropertyInfo による member discovery
UdonExposedNodeCache の問い合わせ
InstalledUdonApiExposure の問い合わせ
UdonApiDiscovery の実行
Unity / VRChat SDK からの metadata 補完
```

Catalog に metadata が存在しない場合、Reflection に fallback して補完してはならない。

不足または非対応の Catalog は明示的な generation error とする。

### Catalog の更新は独立した手動操作とする

Udon API Catalog の生成は引き続き Unity Editor と VRChat SDK が利用可能な環境で行う。

既存の専用操作、

```text
Window/Sobakasu/Build Udon API Catalog
```

を Catalog 更新の経路とする。

Standard Library の生成操作から Catalog を暗黙に生成または更新しない。

これは Editor Window から生成する場合と command line から生成する場合の双方に適用する。

特に、

```text
Scripts/run-standard-library-generator.ps1
```

には Catalog generation を組み込まない。

Catalog の内容を更新する必要がある場合は、開発者が明示的に Catalog Generator を実行する。

### Standard Library Generator core を Unity 非依存にする

Standard Library の生成ロジックは通常の .NET 環境から実行可能にする。

生成 core は以下へ依存してよい。

```text
Sobakasu.Compiler
.NET Base Class Library
Newtonsoft.Json
udon-api-catalog.json
standard-library-generation-config.json
StandardLibraryAdditions~
```

生成 core は以下へ依存しない。

```text
UnityEngine
UnityEditor
VRC.*
VRC.Udon.Editor
```

Editor Window は Unity integration として残してよい。

ただし Editor Window 自体が Udon API discovery を行うのではなく、Unity 非依存の Standard Library Generator core を呼び出す adapter とする。

### standalone .NET executable を用意する

Standard Library Generator 用の SDK-style .NET executable project を用意する。

Compiler と同様に、Unity 用実装とは別の Generator implementation を作らない。

概念的には以下とする。

```text
                                      ┌─ Sobakasu.Editor
Standard Library Generator sources ───┤
                                      └─ standalone .NET project
```

Unity と .NET の双方が同じ Generator source を compile する。

prebuilt DLL を正本としたり、standalone 用 source copy を持ったりしない。

### PowerShell script は `dotnet` の launcher とする

`Scripts/run-standard-library-generator.ps1` は Unity Editor launcher ではなく、standalone Standard Library Generator の launcher とする。

概念的には以下とする。

```text
run-standard-library-generator.ps1
        ↓
      dotnet
        ↓
Standard Library Generator
        ↓
udon-api-catalog.json
generation configuration
manual additions
        ↓
StandardLibrary~
```

スクリプトから Unity Editor を起動しない。

スクリプトから Udon API Catalog Generator を実行しない。

Catalog が存在しない、古い、または必要な metadata を持たない場合は、Catalog を自動生成せず、手動で Catalog を再生成する必要があることをエラーとして報告する。

### Catalog に Standard Library generation に必要な metadata を保持する

現在の Catalog schema は Compiler の extern resolution を主目的としているため、Standard Library Generator が Reflection から取得している情報の一部を直接保持していない。

特に Standard Library generation では次の区別が必要である。

```text
CLR namespace
static API container
method / property / field の source-level kind
generation report 用 display signature
```

runtime type name から CLR namespace と nested type boundary を推測してはならない。

例えば、

```text
UnityEngine.UI.Scrollbar.Direction
```

という runtime identity だけから、

```text
namespace = UnityEngine.UI
declaring type = Scrollbar
nested type = Direction
```

という境界を文字列 heuristic で決定しない。

Catalog generation 時に Reflection から得られる情報を永続化する。

少なくとも type metadata に次を追加する。

```text
clrNamespace
isStaticApiContainer
```

member metadata には、Compiler 用に正規化された `kind` とは別に、Standard Library generation が property と field 等を区別できる source-level kind を保持する。

概念的には以下を表現できるものとする。

```text
StaticMethod
InstanceMethod
Constructor
PropertyGetter
PropertySetter
FieldGetter
FieldSetter
Event
```

generation diagnostics / report に必要な display signature も Catalog に保持する。

これらは Standard Library の naming や placement policy ではない。

Unity / CLR discovery によって決まる physical API metadata であるため Catalog の責務とする。

一方、以下は Catalog に保存しない。

```text
Sobakasu namespace rename
Sobakasu type rename
Sobakasu member rename
language item
Maybe projection
prelude selection
exclude policy
generated module path
generated source
```

これらは引き続き Standard Library generation configuration の責務とする。

### Catalog schema version を更新する

Standard Library Generator が必要とする physical metadata を Catalog persisted contract に追加するため、Catalog format version を更新する。

新しい Catalog format は version 2 とする。

Compiler は、Compiler が必要とする metadata を満たす既存 version 1 Catalog を引き続きロード可能としてよい。

一方 Standard Library Generator は version 2 metadata を必要とするため、version 1 Catalog から不足情報を推測しない。

version 1 Catalog が渡された場合は generation を停止し、Unity Editor の Catalog Generator から手動で Catalog を再生成するよう要求する。

### Catalog DTO と Compiler symbol construction を分離する

Catalog JSON の deserialize と、Compiler の `ExternCatalog` 構築を同一責務として扱わない。

概念的に次の境界を設ける。

```text
JSON
 ↓
UdonApiCatalogData
 ├─→ Compiler ExternCatalog
 └─→ Standard Library Generator source model
```

JSON parsing を Compiler と Standard Library Generator で二重実装しない。

Catalog persisted model は Unity / VRChat type を含まない pure .NET data model とする。

### Standard Library Generator は Catalog-native model を使用する

Standard Library Generator 内部では、Catalog record を `System.Type`、`MethodInfo`、`FieldInfo`、`PropertyInfo` 等に復元しない。

Standard Library generation に必要な型表現、member classification、operator projection、`ref` / `out`、generic、enum、struct rendering は Catalog metadata から行う。

特に次は禁止する。

```text
Type.GetType(runtimeName)
Assembly.Load(...)
runtimeName から Reflection member を再探索する
Catalog の不足項目だけ Reflection で補う
```

Catalog から standalone generator 用の immutable / indexed source model を構築し、その model に generation policy と renderer を適用する。

### Generated declaration validation も Catalog-backed Compiler を使用する

Standard Library Generator が生成した Sobakasu declaration を Compiler で validation する場合、Unity compilation environment を使用しない。

同じ `udon-api-catalog.json` から構築した `SobakasuCompilationEnvironment` を使用する。

これにより、

```text
Catalog
  ↓
Standard Library generation
  ↓
generated declaration validation
```

までを Unity Editor なしで完結させる。

## Alternatives

### Standard Library Generator が引き続き `UdonApiDiscovery` を直接使用する

採用しない。

Compiler と Standard Library Generator で physical API metadata source が異なり、同じ Udon target に対して Catalog と live discovery の二つの状態を持つことになる。

Standard Library の再生成にも Unity Editor が必要なままとなる。

### Standard Library の生成時に Catalog を自動更新する

採用しない。

Catalog generation は SDK metadata の snapshot 更新であり、Standard Library policy の適用とは別の操作である。

自動更新すると、Standard Library の設定変更だけを行いたい場合にも巨大な Catalog 差分が発生し得る。

また Catalog 更新が必要なタイミングを明示的に管理できなくなる。

### Catalog に不足する情報を CLR Reflection で補完する

採用しない。

standalone Generator が Unity / VRChat SDK に依存し続ける。

さらに Catalog と実行環境の CLR metadata が一致する保証がなく、同一 generation に二つの physical metadata source が混在する。

必要な physical metadata は Catalog generation 時に保存する。

### runtime type name から namespace や nested type を推測する

採用しない。

CLR runtime name の `.` は namespace と nested type の双方に現れるため、一般には境界を一意に復元できない。

discovery 時に得られる構造的情報を Catalog に保存する。

### standalone Generator から Unity assembly を参照する

採用しない。

`dotnet` から起動できても semantic dependency が Unity に残るため、今回の目的を満たさない。

### Unity 用と standalone 用に Generator を二重実装する

採用しない。

同じ configuration に対する生成結果が実装ごとに分岐する可能性がある。

Unity と .NET の双方から同じ source set を compile する。

### StandardLibrary~ 自体を physical API metadata として使用する

採用しない。

`StandardLibrary~` は naming、placement、Maybe、prelude、manual additions 等が適用された Sobakasu public API projection であり、physical Udon API metadata ではない。

## Rationale

Udon API discovery と Standard Library projection は異なる責務である。

Udon API discovery は、

```text
「この Unity / VRChat SDK では、どの physical API が利用可能か」
```

を決定する。

Standard Library generation は、

```text
「その physical API を Sobakasu からどのような API として公開するか」
```

を決定する。

`udon-api-catalog.json` を両者の境界に置くことで、この責務分離が永続化形式としても明確になる。

また Compiler と Standard Library Generator が同じ Catalog を入力にするため、

```text
Compiler が認識している target API
```

と

```text
Standard Library が projection している target API
```

の physical metadata source が一致する。

Catalog の更新は SDK 更新時等に明示的に実行し、通常の Standard Library policy 開発では固定された Catalog を使える。

これにより生成結果の再現性が高まり、Standard Library configuration や renderer の変更を SDK discovery の変化から分離してレビューできる。

さらに Generator core を pure .NET 化することで、Unity Editor を起動できない環境でも、

```text
implementation
    ↓
dotnet build
    ↓
dotnet test
    ↓
standard-library generation
    ↓
failure analysis
    ↓
fix
```

という反復が可能になる。

これは通常の開発だけでなく、AI coding agent が Standard Library Generator を自己完結して変更・検証する上でも重要である。

## Consequences

### Positive

- Compiler と Standard Library Generator が同じ physical Udon API metadata を使用する。
- Standard Library の通常生成で Unity Editor を起動する必要がなくなる。
- `run-standard-library-generator.ps1` を軽量な `dotnet` launcher にできる。
- Standard Library Generator core を通常の `dotnet build` / `dotnet test` で検証できる。
- AI coding agent が Unity Editor なしで Generator の実装とテストを反復できる。
- SDK discovery と Standard Library policy の変更を別々の Git diff として扱える。
- Catalog の更新タイミングが明示的になる。
- Standard Library generation の再現性が高まる。
- Reflection metadata と Catalog metadata が同一 generation 中に混在しなくなる。
- Unity Editor Window を UI adapter として維持できる。
- Unity 用と standalone 用で Generator implementation を複製せずに済む。
- Catalog-backed Compiler を generated declaration validation にそのまま使用できる。

### Negative

- Catalog schema に Standard Library generation 用の physical metadata を追加する必要がある。
- Catalog format version を更新する必要がある。
- Standard Library Generator 内に残っている `System.Type` / Reflection ベースの model、formatter、policy、renderer を Catalog-native representation へ移行する必要がある。
- Generator tests を Reflection fixture 中心から Catalog fixture 中心へ整理する必要がある。
- Catalog を更新せず SDK だけを更新した場合、Standard Library は古い Catalog を基準に生成される。
- SDK 更新後は開発者が Catalog の手動再生成を忘れない運用が必要になる。
- version 2 Catalog が生成されるまでは、実際の repository Catalog を使った standalone end-to-end generation を実行できない場合がある。
- Unity integration を変更した場合は、standalone tests に加えて引き続き Unity Test Framework による検証も必要になる。
