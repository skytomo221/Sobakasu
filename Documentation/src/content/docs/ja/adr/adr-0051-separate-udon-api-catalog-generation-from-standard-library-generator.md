# ADR-0051: Separate Udon API Catalog Generation from Standard Library Generator

## Status

Proposed

## Context

Sobakasu Compiler の extern 解決は現在、`ReflectionExternCatalogBuilder` と `UdonExposedNodeCache` を通じて CLR Reflection および `UdonEditorManager` に依存している。

このため、Binder を含む通常のコンパイル処理でも Unity Editor と VRChat SDK が必要になり、Lexer / Parser / Binder / Desugar / IR Lowerer / Optimizer / UASM Assembler といった Compiler Core を Unity 外で実行・テストすることが困難になっている。

一方、Udon で利用可能な型・member・extern signature は、通常のコンパイルごとに discovery する必要はない。インストールされた Unity / VRChat SDK に対して一度 discovery を実行し、その結果を Compiler が利用可能な生成済み metadata として保持できる。

この metadata には、Sobakasu Compiler が Udon API をコンパイルするために必要な情報を保持する。

主な情報は以下である。

- Udon で利用可能な runtime type
- reference type / value type / enum などの型分類
- generic type および generic constraint の検証に必要な型情報
- extern member の名前、kind、static / instance
- parameter type、return type、`in` / `ref` / `out`
- generic method metadata
- 解決済み Udon extern signature
- external enum constant
- array construction / getter / setter / length に必要な Udon ABI capability

これは Standard Library の source API とは異なる。

`StandardLibrary~` は Sobakasu ユーザーに公開する API surface であり、generator policy、命名、wrapper、除外設定などを含む。一方 Udon API catalog は、Compiler が Udon の physical API を解決するために使用する target metadata である。

現在の `StandardLibraryGeneratorWindow` は `Window/Sobakasu/Build Standard Library` から開き、`StandardLibrary~`、additions、diagnostics、generation report の生成を担当している。

Udon API catalog は Standard Library の生成物ではなく Compiler の入力データであるため、この Window に catalog 生成機能を追加すると責務が混在する。

## Decision

Udon API discovery の結果を、生成済みの `udon-api-catalog.json` として保存する。

`udon-api-catalog.json` は、Sobakasu Compiler が Udon API をコンパイルするために必要な metadata の正本とする。

### Catalog の生成

Catalog は Unity Editor と VRChat SDK が利用可能な環境で生成する。

生成処理では CLR Reflection、Udon node definitions、および Udon type exposure 情報を使用して Udon API を discovery し、Compiler が必要とする metadata に変換する。

通常の Sobakasu compilation ではこの discovery を実行しない。

### Standard Library Generator とは別の Editor Window を使用する

Catalog の生成には専用の Editor Window を追加する。

Menu item は以下とする。

```text
Window/Sobakasu/Build Udon API Catalog
```

Window title は以下とする。

```text
Udon API Catalog
```

この Window は少なくとも以下を提供する。

- catalog output file の表示
- output file の選択
- `udon-api-catalog.json` の生成
- 生成結果の表示
- output file または output directory を開く操作

既存の

```text
Window/Sobakasu/Build Standard Library
```

および `StandardLibraryGeneratorWindow` は Standard Library の生成のみを担当する。

Standard Library の生成操作から Udon API catalog を暗黙に生成・更新しない。

Udon API catalog の生成操作から Standard Library を暗黙に生成・更新しない。

両者は独立したユーザー操作とする。

### Discovery implementation は共有する

Editor Window と生成物は分離するが、Udon API discovery のロジックを二重実装してはならない。

概念的な構造を以下とする。

```text
Unity / VRChat SDK
        ↓
   UdonApiDiscovery
        ↓
 canonical Udon API model
       / \
      /   \
     ↓     ↓
Udon API   Standard Library
Catalog    Generator
Generator
     ↓
udon-api-catalog.json
```

`UdonApiDiscovery` またはそれに代わる共通 discovery component が、Udon exposure、CLR member、type、generic、operator、property、field などの physical API discovery を一元的に担当する。

`UdonApiCatalogGenerator` と `StandardLibraryGenerator` が独自に Udon exposure の判定規則を持ってはならない。

Standard Library Generator は、この共通 discovery model に対して generator policy、renaming、placement、manual additions などを適用して `StandardLibrary~` を生成する。

Catalog Generator は、同じ discovery model から Compiler 用 metadata を生成する。

### Catalog schema

Catalog のトップレベルは以下の情報を持つ。

```text
UdonApiCatalog
{
    formatVersion
    target
    types
    unexposedClrTypeNames
    members
    unexposedMembers
    capabilities
    unmatchedUdonSignatures
}
```

`formatVersion` は Sobakasu が定義する catalog schema version とする。今回の schema 整理は未リリースの version 1 schema を確定させる変更であり、migration として扱わない。そのため `formatVersion` は `1` のままとする。

`target` には catalog を生成した Unity / VRChat SDK 環境を識別する情報を保持できる。

`types` は Udon-exposed type の metadata を保持し、extern type resolution と validation に使用する。

`unexposedClrTypeNames` は CLR discovery scope 上には存在するが Udon-exposed ではない型を識別する軽量な名前一覧とする。非exposed型の member metadata は保存しない。

`members` は Udon-exposed member のみを保持し、extern member resolution、overload resolution、および ABI lowering に必要な rich metadata を保持する。`udonExposed` boolean は持たず、`members` に所属すること自体が exposed であることを表す。

`unexposedMembers` は exposed host type 上で discovery された非exposed member を識別する軽量 metadata とする。これにより Compiler は Unity / Reflection なしでも「CLR側ではmemberとして認識されたが Udon target では利用できない」という情報を保持できる。

各 `unexposedMembers` record は次の4項目だけを持つ。

```text
hostType
name
kind
externSignature
```

`hostType` は structured type reference ではなく runtime type name 文字列とし、CLR declaring type ではなく member の surface type を表す。`kind` は `Constructor`、`StaticMethod`、`InstanceMethod`、`PropertyGetter`、`PropertySetter`、`FieldGetter`、`FieldSetter`、`Event` を区別し、static / instance と getter / setter を単独で復元できるものとする。`externSignature` は discovery 時に算出された physical signature をそのまま保持し、signature が算出されない member に対して架空の signature を生成しない。

`unexposedMembers` には ABI parameter、return type、generic constraint などの rich metadata を保存しない。

`capabilities` は通常の CLR member model だけでは表現しにくい Udon 固有 capability を保持する。初期用途として array construction / getter / setter / length の解決済み ABI を保持する。

`unmatchedUdonSignatures` は Udon node definitions 側には存在したが、discovery された CLR member と対応付けられなかった signature を保持する。

以下を catalog schema の不変条件とする。

```text
非exposed typeのmemberはcatalogへ保存しない。

membersにはUdon-exposed memberだけを保存する。

unexposedMembersにはrich ABI metadataを保存しない。

exposure状態をmembers[].udonExposedのbooleanで表現せず、
members / unexposedMembers のpartitionで表現する。
```

型参照は `System.Type` を保存せず、概念的に以下を表現できる structured type reference とする。

```text
Named(runtimeName)
Array(element)
GenericParameter(scope, ordinal)
ConstructedGeneric(definition, arguments)
```

member parameter の `Normal` / `In` / `Ref` / `Out` / `GenericTypeArgument` は type reference とは別の ABI parameter metadata として保持する。

generic constraint は Compiler が Reflection を使用せず検証できる情報として保持する。

external enum は member name から constant value を解決できる metadata を保持する。

### Catalog に保存しない情報

以下は catalog の永続化形式には保存しない。

- `System.Type`
- `MethodInfo`
- `MethodBase`
- `MemberInfo`
- `FieldInfo`
- `TypeSymbol`
- `MethodGroupSymbol`
- Compiler 内部の operator index
- Compiler 内部の lookup dictionary
- Sobakasu の logical parameter list
- Sobakasu の logical return type
- Standard Library の placement や public API naming policy

これらのうち Compiler 内部で必要な index や symbol は catalog load 時に構築する。

logical extern signature は physical ABI metadata から構築する。

### Raw signature/type list は別ファイルにしない

以下の独立した snapshot file は正本として導入しない。

```text
extern-signatures.txt
type-names.txt
```

exposed type information と resolved extern signature は `udon-api-catalog.json` に含める。

必要な signature set や type index は catalog load 時に生成する。

### JSON を正本とする

永続化形式には JSON を使用する。

ファイル名は以下とする。

```text
udon-api-catalog.json
```

生成結果は deterministic とする。

少なくとも以下を固定する。

- UTF-8
- LF newline
- 2-space indentation
- property order
- type order
- member order
- capability order
- enum constant order

同一の SDK、同一の Sobakasu version、同一の generation condition からは同一内容を生成できなければならない。

これにより SDK 更新時の catalog 差分を Git diff でレビュー可能にする。

binary format や generated C# は catalog の正本としない。

### Compiler から Unity discovery を除去する

Compiler は生成済み `udon-api-catalog.json` をロードし、そこから immutable な `ExternCatalog` を構築する。

概念的な依存関係は以下とする。

```text
Unity / VRChat SDK
        ↓
   UdonApiDiscovery
        ↓
udon-api-catalog.json
        ↓
 UdonApiCatalogLoader
        ↓
 immutable ExternCatalog
        ↓
       Binder
        ↓
        IR
        ↓
       UASM
```

通常の compilation path では以下を要求しない。

```text
UnityEngine
UnityEditor
VRC.Udon.Editor
UdonEditorManager
CLR assembly scanning
Udon API Reflection discovery
```

Catalog は一度ロードして index 化し、複数 compilation で再利用できるものとする。

既存の `SobakasuCompilationEnvironment` / `ExternCatalog` injection boundary を利用し、Binder 自体が catalog file や Unity discovery の所在を探索しない。

`TypeSymbol.RuntimeClrType`、generic method constraint validation、generic `System.Type` operand、enum constant materialization など、現在 Compiler Core に残っている Reflection / runtime type identity 依存についても、最終的には catalog metadata と target-specific materialization boundary へ移行する。

本 ADR はそのための Udon API metadata source と生成境界を定義する。

## Alternatives

### Standard Library Generator Window から catalog も生成する

採用しない。

Standard Library はユーザー向け Sobakasu API surface であり、Udon API catalog は Compiler の target metadata である。

同じ Window に置くと、Standard Library の再生成と Compiler metadata の更新が同じ操作に見え、責務と更新条件が曖昧になる。

### StandardLibrary~ を catalog として使用する

採用しない。

Standard Library は generator policy、renaming、manual additions、wrapper、公開範囲などを含む source-level projection であり、Udon の physical API catalog そのものではない。

direct extern resolution、generic constraint、enum constant、array ABI などに必要な情報を完全には保持しない。

### Compiler 実行時に Reflection と UdonEditorManager から catalog を構築する

採用しない。

通常の compilation が Unity Editor / VRChat SDK に依存し続け、Compiler Core を独立して実行・テストできない。

同じ SDK metadata を compilation ごとに discovery する必要もない。

### `extern-signatures.txt` と `type-names.txt` を個別に生成する

採用しない。

extern overload resolution、generic、`ref` / `out`、enum、type classification などに必要な構造化 metadata を表現できず、複数ファイル間の整合性管理も必要になる。

### generated C# を catalog とする

採用しない。

catalog 更新のたびに Compiler assembly の再コンパイルが必要になり、target metadata と Compiler implementation の境界も弱くなる。

生成差分も JSON よりレビューしにくい。

### binary catalog を正本とする

採用しない。

Git diff による SDK 更新レビューが困難になる。

将来 JSON load が性能上の問題になった場合は、JSON を正本として派生 binary cache を追加することは妨げない。

## Rationale

Udon API catalog は Standard Library ではなく Compiler の target metadata である。

このため、Standard Library Generator と Catalog Generator の UI と出力責務を分離しつつ、physical Udon API discovery だけを共有する構造が最も責務境界を明確にできる。

生成済み catalog を Compiler の入力とすることで、Unity / VRChat SDK に依存する処理を generation boundary の外側へ移動できる。

また、catalog を CLR Reflection metadata の単純な dump とせず、Sobakasu Compiler が Udon API の型解決、overload resolution、constraint validation、ABI lowering に必要な事実だけを保存することで、Compiler Core に `System.Type` や `MethodInfo` を再導入せずに済む。

JSON を deterministic に生成することで、VRChat SDK や Unity の更新によって Udon API が変化した際も、その差分を source code と同様にレビューできる。

## Consequences

### Positive

- Standard Library generation と Compiler target metadata generation の責務が明確に分離される。
- `StandardLibraryGeneratorWindow` が catalog 管理まで担わずに済む。
- Udon API discovery implementation は両 generator で共有できる。
- 通常の Sobakasu compilation から Unity Editor / VRChat SDK discovery 依存を除去できる。
- Binder を含む Compiler Core を `dotnet test` など Unity 外の環境で実行可能にする基盤になる。
- Udon API metadata の SDK 間差分を Git 上でレビューできる。
- Compiler が利用する physical Udon API と Standard Library の public surface を独立して扱える。
- `extern-signatures.txt` や `type-names.txt` のような二重の正本を持たずに済む。
- 将来の Udon API追加にも catalog regeneration で対応できる。

### Negative

- `udon-api-catalog.json` の schema と compatibility を維持する必要がある。
- VRChat SDK または Unity の更新時には catalog の再生成が必要になる。
- Catalog Generator 用の Editor Window、serializer、loader、validation、tests が新たに必要になる。
- Standard Library Generator と Catalog Generator が共有する discovery layer を整理する必要がある。
- 現在 `System.Type`、`MethodInfo`、`UdonEditorManager` に依存している generic constraint、enum constant、array ABI、generic type operand などを段階的に generated metadata ベースへ移行する必要がある。
- catalog と installed SDK が異なる状態を検出・診断する仕組みが必要になる可能性がある。
