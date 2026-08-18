# ADR-0032: Add `maybe out` for Nullable Extern Output Parameters

## Status

Accepted

## Context

ADR-0026はsource-levelの不存在をcompiler専用型ではなく通常のgeneric enum `Maybe<T>`で表し、外部referenceのvalidity判定には`VRC.SDKBase.Utilities.IsValid`を用いる方針を定めた。ADR-0030はこの方針を宣言的bindingのnormal returnへ適用する`maybe extern`を導入した。ADR-0031はCLR／Udonの`ref`／`out` parameterをphysical ABI metadataとして保持し、通常returnとparameter宣言順の`ref`／`out` outputをSobakasuのlogical returnへ投影する。

しかし、`bool TryGet(out GameObject value)`のように、normal returnとは独立して`out` valueがnullまたはinvalid referenceになり得るAPIを、logical return `(bool, Maybe<GameObject>)`として宣言する方法がない。raw `out`をすべて自動的に`Maybe<T>`へ変換すると、ADR-0026が維持するlow-level escape hatchを失い、physical overload selectionとlibrary policyも混同する。

## Decision

### Extern ABI parameterに`maybe out`を追加する

ADR-0031の宣言的extern binding右辺に限り、次のparameter specificationを許可する。

```sobakasu
fn try_get() -> (bool, Maybe<GameObject>)
  = extern External.TryGet(
      maybe out GameObject value,
    )
```

`maybe`はこの位置だけで意味を持つcontextual keywordである。通常のfunction parameter、normal ABI parameter、`ref`には導入しない。したがって`maybe ref`、`maybe T name`、通常function parameterの`maybe out`は不正とする。

### Physical ABIとlogical output projectionを分離する

`out T`と`maybe out T`は、どちらもphysical parameter mode／typeとしては`out T`である。`maybe`はoverload resolutionへ参加せず、同じCLR／Udon overloadを選択できる。Binderはphysical overloadを確定した後にlogical projectionを適用し、metadataで次を区別する。

```text
PhysicalPassingMode: Normal | Ref | Out | In
LogicalOutputProjection: Raw | Maybe
```

logical outputは次のとおりである。

```text
out T       -> T
maybe out T -> Maybe<T>
```

normal returnを先頭に置き、その後へparameter宣言順の`ref`／`out` outputを並べるADR-0031の規則は変更しない。`ref`と`out`を種類別に並べ替えない。output数の正規化も維持する。

```text
0 outputs -> ()
1 output  -> T
2+ outputs -> (T0, T1, ...)
```

このため`void Foo(maybe out GameObject value)`のlogical returnは`Maybe<GameObject>`であり、`(Maybe<GameObject>,)`ではない。

### 既存のMaybe validity projectionを再利用する

`maybe out T`は、既存の`maybe extern`と同じ規則でvalidity check可能なreference-like external valueに限る。value type等へ適用した場合はBinder diagnosticとする。新しいnullable type分類は導入しない。

loweringはphysical `out T` temporaryを確保してextern callを一度だけ実行し、そのtemporaryを`Utilities.IsValid`と`Maybe` payloadの双方で再利用する。validならsingle-payload variant、invalidならunit variantを構築する。variantはvisibleなgeneric enum `Maybe<T>`の形状から、validity methodはextern catalogと既存resolverからBinderが解決する。

`Maybe<T>`はADR-0026どおり通常のgeneric payload enumであり、既存aggregate flatteningを用いる。Maybe専用runtime object、opcode、UASM signature文字列、variant名のbackend hard-code、backend-only nullability inferenceは追加しない。normal returnが`bool`であっても、その値とout valueのvalidityを結合する規則は設けない。

## Alternatives

1. すべてのreference `out`を自動的に`Maybe<T>`へ変換する。raw ABI accessを失い、将来のgenerator policyまでcompiler semanticsへ混在させるため採用しない。
2. `out Maybe<T>`としてphysical ABI型も変更する。実在するCLR／Udon overloadと一致しなくなるため採用しない。
3. `maybe ref`も同時に導入する。入力時と出力時のpresence semanticsを別途設計する必要があり、今回のnullable output projectionを越えるため採用しない。
4. normal returnの`bool`を`Maybe` tagとして使用する。API固有の成功条件とUnity／VRChat reference validityを誤って結合するため採用しない。

## Rationale

physical mode／typeとlogical projectionを分離すれば、既存のreflection catalogとoverload resolverを唯一のphysical target選択経路として維持できる。BinderでMaybe enumとvalidity methodまで解決し、IR Lowererは解決済みprojectionを通常のcall、branch、aggregate constructionへ展開できるため、ADR-0003の責務分離も保たれる。

ADR-0026の通常の`Maybe<T>` policyとraw extern escape hatchを変更せず、ADR-0030の`maybe extern`を置き換えず、ADR-0031の`ref`／`out` logical adaptationだけをparameter単位へ拡張する。

## Consequences

### Positive

* nullableなextern `out`をlogical `Maybe<T>`として明示できる。
* `out`と`maybe out`は同じphysical overloadを使用する。
* extern callは一度だけ実行され、out temporaryをvalidity判定とpayloadで共有する。
* 複数outputの宣言順とtuple flatteningを維持できる。
* backendへsource syntax、Maybe解決、nullable policyを持ち込まない。

### Negative

* ABI parameter metadataとloweringにparameter単位のprojection情報が増える。
* `maybe out`にはvisibleな適切な`Maybe<T>`とcatalog-backed `Utilities.IsValid`が必要になる。
* generatorがどのAPIを`maybe out`にするかというpolicyは別途設計が必要であり、当面のdefault生成はraw `out`のままとなる。

## Non-goals

* `maybe ref`、`T?`、一般的なnullable reference type system
* ownership、borrow、lifetime、通常functionの`ref`／`out`
* reference outputの自動nullable inferenceまたはpolicy configuration
* static classのtop-level function化、predicate命名変換、documentation URL生成

