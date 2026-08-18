# ADR-0033: Apply Extern Parameter Projection Rules Uniformly to Constructors

## Status

Accepted

## Context

ADR-0031はextern methodのnormal returnと`ref`／`out` parameterをlogical returnへ適応する規則を定めた。Reflection extern catalogはconstructorもconstructed valueをresult slotへ受け取るcallableとして扱えるが、宣言構文とstub rendererがconstructor parameterを通常argument listとして出力すると、`ref`／`out`のphysical ABI modeを表現できない。これによりcatalogが持つconstructor ABIと生成sourceが非対称になる。

ADR-0032は`out T`のphysical ABIを保ったままlogical outputだけを`Maybe<T>`へ投影する`maybe out`を定める。このparameter単位の規則もconstructorだけ例外にすべきではない。

## Decision

### Constructorをimplicit primary outputを持つextern callableとして扱う

constructorは特別なlogical parameter規則を持たず、`Self`をimplicit primary logical outputとして持つextern callableとする。

```text
method outputs      = non-void normal return + ref/out outputs
constructor outputs = constructed Self       + ref/out outputs
```

parameter projectionはADR-0031と同一である。

```text
normal parameter -> input
ref parameter    -> input + output
out parameter    -> output only
```

constructor logical outputは`Self`を先頭に置き、その後へparameter宣言順の`ref`／`out` outputを追加する。`ref`と`out`を種類別に並べ替えない。

```csharp
Foo(ref int value, out string name, ref float weight)
```

```sobakasu
pub static fn new(value: i32, weight: f32)
    -> (Self, i32, string, f32)
  = extern new Self(
      ref i32 value,
      out string name,
      ref f32 weight,
    )
```

by-ref parameterがなければ従来どおり単一の`Self`を返し、`(Self,)`へはしない。`out`だけならlogical inputは空で、returnは`(Self, T)`となる。

### Constructorでもparameter projectionを適用する

ADR-0032の`maybe out`をconstructor ABI parameterにも許可する。

```sobakasu
pub static fn new() -> (Self, Maybe<GameObject>)
  = extern new Self(
      maybe out GameObject owner,
    )
```

physical ABIはconstructed result slotと`out GameObject` slotのままであり、logical outputだけを`Maybe<GameObject>`へ投影する。overload selection、validity resolution、aggregate constructionはmethodと同じ仕組みを使う。

### Catalogと実際のUdon ABIをsource of truthとする

Binderはconstructor targetとexplicit ABI parameter mode／typeをreflection extern catalogのconcrete constructor candidateへ照合する。IR Lowererは選択済み`ExternMethodSymbol`のphysical argument順とresult slotを使用し、ADR例からPUSH順を再推測しない。

logical result assemblyのみを`Self + projected ref/out outputs`として共通化する。UASM backendは解決済みphysical callとflatten済みlogical resultをemitし、constructor semanticsやsource modifierを再解釈しない。

### Stub rendererはby-ref constructorのABI signatureを出力する

constructorにby-ref parameterがある場合、stub generatorはmethodと同じく右辺へ型、名前、`ref`／`out` modeを含むphysical ABI signatureを生成する。左辺はlogical inputとlogical returnを生成する。

generatorはreference `out`を自動的に`maybe out`へ変更しない。nullable inference、policy file、manual overrideは本決定の範囲外であり、defaultはraw `out`を維持する。

## Alternatives

1. Constructorだけ`ref`／`out`を生成対象外にする。catalog completenessとmethodとの対称性を失うため採用しない。
2. Constructor専用のparameter adaptationとloweringを追加する。同じphysical／logical規則が二重化し、順序差分を生みやすいため採用しない。
3. Constructor resultを`ref`／`out`の後ろへ置く。ADR-0031のprimary outputモデルと通常のconstructor APIに反するため採用しない。
4. Generatorへnullable policyを同時導入する。APIごとのpolicy設計は独立した判断であり、本ADRのconstructor対称性を越えるため採用しない。

## Rationale

constructorとmethodの差をprimary outputだけに限定すれば、physical parameter matching、logical input selection、output ordering、Maybe projection、aggregate flatteningを共有できる。catalogをABIのsource of truthとして維持しながら、生成sourceも同じmetadataを損失なく表現できる。

本ADRはADR-0031のoutput orderingを変更せずconstructorへ適用し、ADR-0032のparameter projectionも同じ位置へ合成する。

## Consequences

### Positive

* normal、`ref`、`out`、mixed constructorを同じlogical API規則で公開できる。
* constructed `Self`とparameter outputsの順序が一意になる。
* methodとconstructorが共通のBinder／IR adapterを利用する。
* constructor stubがphysical by-ref ABI modeを保持し、Parserへ再入力できる。
* constructorの`maybe out`も通常のMaybe aggregate loweringを利用できる。

### Negative

* constructorのgenerated declarationはby-ref parameterを右辺に重ねて記述する必要がある。
* `Self`を含むmulti-output constructorでは利用側のdestructuringが必要になる。
* nullable constructor outputを自動生成するpolicyは引き続き未定である。

## Non-goals

* constructorまたは通常functionへの一般的なreference semantics
* `maybe ref`、nullable reference type system、ownership、borrow、lifetime
* generatorのpolicy-driven redesign、nullable API inference、configuration file
* static class変換、命名変換、documentation URL生成

