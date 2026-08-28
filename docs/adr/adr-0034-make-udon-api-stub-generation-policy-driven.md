# ADR-0034: Make Udon API Stub Generation Policy-Driven

## Status

Superseded by ADR-0035

## Context

Udon API Stub Generatorはinstalled Udon node catalogとCLR reflectionから、外部型、member kind、physical parameter type、`ref`／`out` passing mode、external signatureを発見できる。しかし、reference resultをrawにするか`Maybe<T>`へprojectするか、reference `out`をrawにするか`maybe out`へprojectするか、Sobakasu上の名前、公開位置、除外対象はreflectionだけでは決められない。

特にnullabilityについて、「CLR reference typeをすべてMaybeにする」固定heuristicと、reflection metadataからnon-nullを推測する固定heuristicのどちらもgeneratorの責務にはしない。ADR-0026はraw externをlow-level escape hatchとして維持し、ADR-0030はnormal returnの`maybe extern`、ADR-0032はparameter単位の`maybe out`、ADR-0033はconstructorへの同じprojection規則を定めている。generatorはこれらの既存構文を選択して出力すべきであり、独自のnullable semanticsやloweringを持つべきではない。

従来のgeneratorは各CLR typeをCLR namespace由来のpathへ個別の`pub impl ... = extern ...`として出力し、member名を一律snake_caseへ変換していた。生成後のsourceを人間が移動または編集するとSDK更新時の再生成で変更が失われ、同じ入力から同じ公開APIを再現できない。

また、static classをtop-level functionとして公開する場合、CLR namespaceとSobakasu moduleを同一視すると、外部API側の物理配置がStandardLibraryの公開構造を拘束する。生成先moduleと、そのmodule内での`impl`／`top_level` placementは別々のpolicyでなければならない。

## Decision

### Discovery、policy、renderingを分離する

generatorを次のpipelineにする。

```text
Udon exposure/catalog
        ↓
physical CLR/Udon API model
        ↓
version-controlled generation configuration
        ↓
logical Sobakasu generation model
        ↓
renderer and module path planner
        ↓
GeneratedUdonApiStubs~
```

Discoveryはphysical APIの事実だけを保持する。Policy applicationはgenerated namespace、placement、name、exclusion、return projection、out projectionを解決する。Rendererは解決済みlogical modelから既存のSobakasu構文を出力する。

生成結果自体の手編集は調整手段にしない。人間による調整はversion-controlledなJSON configurationに保存する。同じcatalog、SDK、configurationからは同じsource、path、declaration order、reportが得られなければならない。

### Version 1 configuration

Configuration version 1は次を設定できる。

* defaults: generated namespace、reference return projection、reference out projection、static class placement、predicate naming
* CLR namespace rule: CLR namespace prefixからSobakasu namespaceへのmappingとsubnamespace保存
* exact type rule: generated namespace、placement、将来も利用できるwrapper type name
* exact member rule: return projection、out parameter projection、generated callable name、exclude

`out` projectionはUnityの`JsonUtility`でdictionaryを使わず決定的に読めるよう、parameter名とprojectionを持つarrayとして表す。

Configを指定しない場合も同じdefault policy objectを使用する。Version 1のdefaultは次とする。

```text
generated namespace       = external
reference return          = raw
reference out             = raw
static class placement    = top_level
predicate naming          = true
normal class placement    = impl
preserve_subnamespaces    = false
```

`extern`はSobakasuのkeywordでmodule segmentに使用できないため、例示上の`extern`ではなく`external`をdefault module名とする。

Parameter単位のout projectionは次の形で記述する。

```json
{
  "declaring_type": "Some.Namespace.Store",
  "member_kind": "static_method",
  "member": "TryGet",
  "parameter_types": ["Some.Namespace.GameObject&"],
  "out": [
    {
      "parameter": "value",
      "projection": "maybe"
    }
  ]
}
```

raw projectionは既存のlow-level wrapperとの互換性を維持する。Normal class／structは`impl`を基本とする。CLR static classは`type.IsAbstract && type.IsSealed`で認識し、選択されたSobakasu module内のtop-level functionとして公開する。Static classをexact type ruleで`impl`へ戻すことはできる。Exact type ruleでnormal typeを`top_level`へ置く場合は表現可能なstatic memberだけを生成し、instance memberをreport付きでskipする。Instance member自体をtop-level functionへ変換する規則は導入しない。

### Exact member identityとprecedence

Member ruleは少なくとも次のidentityをすべて持つ。

```text
declaring CLR type
member kind
CLR member name
ordered CLR parameter type identities
```

CLR by-ref typeは`System.Int32&`のようにelement typeと区別する。Constructor、method、property getter／setter、field getter／setterはmember kindで区別する。利用者にencoded Udon extern signatureを記述させない。

Policy precedenceは次とする。

```text
exact member rule
    > exact type rule
    > longest matching CLR namespace rule
    > defaults
```

Generated namespaceだけは、exact type override、longest-prefix CLR namespace mapping、global defaultの順で解決する。Namespace ruleで`preserve_subnamespaces`がtrueなら、matched prefixより後ろのCLR namespace segmentをsnake_case化してtarget namespaceへ追加する。Falseならtarget namespaceへflattenする。

### Projection

Reference normal returnのraw policyは`T`と`= extern`を生成し、maybe policyは`Maybe<T>`と`= maybe extern`を生成する。Reference `out`のraw policyは`out T`とlogical `T`を、maybe policyはphysical typeを変えず`maybe out T`とlogical `Maybe<T>`を生成する。`out Maybe<T>`は生成しない。

Constructorはconstructed `Self`をlogical outputの先頭に維持し、その後へparameter declaration orderの`ref`／`out` outputを並べる。Constructor自体を`maybe extern`にはしない。`maybe ref`も導入しない。

Current compilerでは`maybe extern`がphysical callのcomplete logical resultへ適用されるため、reference normal returnと`ref`／`out` outputを同時に持つcallableへのnormal-return-only projectionはunsupported configurationとして拒否する。Generator独自の架空構文は追加しない。

### Naming

Explicit member nameはautomatic namingより優先する。それ以外はsnake_caseを使用する。`IsXxx`または`isXxx`で始まり、logical callable returnが単一の`bool`であるgetter／methodだけは`xxx?`へ変換する。Non-bool returnはpredicate化しない。Bool property setterは`set_xxx`とし、`?`を付けない。

### Sobakasu moduleとoutput layout

External CLR declaring typeとgenerated Sobakasu namespaceを別々のmodel fieldとして保持する。External binding RHSは常に元のCLR typeを使い、generated namespaceへ書き換えない。

Sobakasuの既存module規約に従い、resolved namespace `foo.bar`は`foo/bar.sobakasu`へ出力する。同じgenerated namespaceへ配置された複数のexternal typeは同じmodule fileへ集約する。Normal typesは複数の`impl` declarationとして、static classesはtop-level declarationの集合として同居できる。`top_level`はrepository globalではなく、resolved module内のtop levelを意味する。

### Collision、validation、report

Duplicate declarationはpolicy適用後のgenerated namespace、placement／impl scope、final name、ordered logical input type identityで判定する。Return typeはidentityに含めない。正当なoverloadは保持する。Collisionを自動suffixで回避せず、該当memberをskipしreportへ記録する。

次はgeneration前のconfiguration errorとする。

* unknown JSON propertyまたはinvalid enum value
* duplicate／conflicting namespace、type、member rule
* exact ruleの0 matchまたは複数match
* invalid Sobakasu namespace、type name、callable name
* value type returnへのmaybe projection
* `ref`、存在しないparameter、value type `out`へのmaybe out projection
* current compilerで表現できないplacementまたはprojection combination

Reportは既存のtype/member completeness invariantを維持し、configuration identity、configured／matched rules、exclusion、collision、projection count、placement count、resolved namespace、generated fileを追加する。

Output writerは引き続きnew／empty directoryだけへUTF-8 BOMなしで書き、`StandardLibrary~`を拒否し、`generation_report.json`と`skipped_members.txt`を生成する。

## Alternatives

1. 全referenceをreflectionだけでMaybe化する。API policyとphysical typeを混同し、raw escape hatchを失うため採用しない。
2. Generated sourceの手編集を維持する。SDK更新後の再現性がなく、policy変更をreview可能なdataとして保存できないため採用しない。
3. CLR namespaceをgenerated moduleとして固定する。StandardLibraryの公開hierarchyを外部APIの都合から分離できないため採用しない。
4. Static classごとに個別moduleを強制する。同一moduleへ`Math`／`MathF`等のlogical overload setを集約できないため採用しない。
5. Collisionへ自動suffixを追加する。公開APIがcatalog順序に依存し、意図しないrenameを隠すため採用しない。
6. Generator内でMaybe wrapper bodyまたはnullable loweringを生成する。ADR-0030〜0033のBinder／IR責務を重複させるため採用しない。

## Consequences

### Positive

* Generated sourceを編集せず、version-controlled configだけで公開APIを再構成できる。
* Physical API discoveryとSobakasu API policyが分離される。
* Raw／Maybe、raw out／maybe out、rename、exclude、placement、namespaceを一つの拡張可能なpolicyとして扱える。
* Static class overloadを自然なtop-level functionとして維持できる。
* CLR namespaceと無関係なSobakasu module hierarchyを構成できる。
* Stale／typo configとpost-policy collisionを明示的に検出できる。
* Existing compiler semanticsとoutput safetyを再利用できる。

### Negative

* Configuration schema、validation、matching、reportの保守が必要になる。
* Installed SDK／catalogから対象memberが消えるとstale ruleとしてgenerationが失敗する。
* 同一moduleへの集約により、1ファイルの差分が複数external typeの変更を含むことがある。
* Current compilerで表現できないinstance-to-top-level変換とnormal-return-only Maybe projectionは設定できない。

## Non-goals

* Compilerの新しいnullable type system、`T?`、`maybe ref`、`out Maybe<T>`
* CLR nullable annotationの完全推論
* Ownership、borrow、lifetime、runtime Maybe representationの変更
* Generated sourceの手編集merge
* StandardLibrary~への直接自動書き込み
* Documentation site generation
* New property syntax、default arguments、new overload resolution rules
