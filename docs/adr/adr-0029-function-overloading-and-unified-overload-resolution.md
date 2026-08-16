# ADR-0029: Function Overloading and Unified Overload Resolution

## Status

Accepted

## Context

SobakasuでUnity、VRChat、Udon APIのwrapperを標準ライブラリとして提供するには、ランタイム側と同じ自然な名前を保ったまま、引数型だけが異なる関数を公開できる必要がある。`log_string`、`log_object`のような別名を要求すると、wrapper APIが不自然になり、利用者とライブラリ作者の双方に外部APIのoverloadを手作業で符号化する負担が生じる。

ADR-0008はreflectionから得たextern候補について、exact match、既存primitive widening、同順位のambiguityをBinderで解決する仕組みを導入した。ADR-0016は同じ原則を`impl`内のinstance method、`static fn`、operatorへ広げ、method groupを候補集合として保持している。一方、ADR-0011のtop-level関数は`name -> FunctionSymbol`として単数で保持され、同名宣言を禁止していた。この差により、Sobakasu関数でexternを包む層だけが不自然な別名を必要としていた。

ADR-0017、ADR-0018、ADR-0027は、標準ライブラリmodule、`pub`、`use`、`pub use`、Prelude、grouped import、glob importを定めている。関数overloadを標準ライブラリで利用するには、これらの経路が単一symbolではなく候補集合を失わずに運ぶ必要がある。

ADR-0003が定める責務分離により、候補収集、型適用可能性、最良候補の選択はBinderで完了しなければならない。IR、inline lowering、UASM backendへ未解決の名前やruntime検索を持ち込むことはできない。

## Decision

### Overload identityと宣言

top-level `fn`はoverload可能とする。overload identityは、概念的に次で決まる。

```text
declaring module + function name + ordered parameter type identities
```

戻り値型はidentityに含めない。同じmodule内の同名関数について、parameter type列が異なれば共存できる。parameter type列が同一なら、戻り値型が同じ場合も異なる場合もduplicate function overloadとして宣言時に拒否する。

```sobakasu
fn parse(value: i32) -> i32 { value }
fn parse(value: string) -> i32 { 0 }
```

上記は有効である。次は無効である。

```sobakasu
fn parse(value: i32) -> i32 { value }
fn parse(value: i32) -> string { "value" }
```

この決定は、ADR-0011の「user-defined function overloadを禁止する」という部分を置き換える。関数構文、return規則、再帰禁止、inline loweringなど、ADR-0011のその他の決定は維持する。

### 統一された候補モデル

Binderはtop-level関数を`name -> overload set`として保持する。各候補は従来どおり独立した`FunctionSymbol`であり、Bound callは選択済みの1つの`FunctionSymbol`を参照する。

overload selectionの適用可能性とconversion distanceは、top-level関数、user method、extern methodで共通のcallable候補処理を使用する。top-level専用の別系統のconversion policyは作らない。対象は次である。

* top-level `fn`
* `impl`内のinstance method
* `impl`内の`static fn`
* extern methodおよびextern call

operator overloadの検索順序と組み込みoperator優先規則はADR-0016を維持する。

### Overload resolution

Binderはcall siteごとに次を行う。

1. 名前解決と可視性規則に従って候補集合を収集する。
2. arityが一致する候補だけを残す。
3. 各parameterに対して既存のcall conversionが適用可能か検査する。
4. 各argumentのconversion distanceの合計が最小の候補を選ぶ。
5. 最小値が複数候補で同じならambiguityとして失敗する。

exact matchのdistanceは0であり、既存primitive wideningは既存のrank差を使用する。既存の`object` catch-all applicabilityも維持し、exact matchおよびprimitive wideningより低い順位とする。external wrapper typeとruntime ABI typeの一致は、従来どおりextern候補の照合時だけ使用する。

このADRは新しいimplicit conversionを追加しない。特に、異なるnumeric category間の変換、narrowing、user-defined conversion、期待戻り値型による変換は導入しない。

候補集合が存在するが適用可能な候補がない場合は、未定義名とは区別し、argument typeと候補signatureを含むno matching overload診断を行う。同順位の候補が複数残る場合は、候補signatureを含むambiguous function overload診断を行う。一意な候補が選ばれた後のBound treeには、未解決のoverload setを残さない。

### Method、`static fn`、extern

ADR-0016のmethod groupを維持し、instance methodと`static fn`は従来の候補収集、可視性、receiver種別検査、duplicate signature検査を使用する。共通化するのはargument applicabilityとbest-candidate rankingであり、receiverやoperatorの固有規則は各既存経路に残す。

Sobakasu wrapper関数の呼び出しと、その本体にあるextern式は二段階で解決する。

```text
Sobakasu call
  -> Sobakasu function overload resolution
  -> selected FunctionSymbol
  -> bound function body
  -> existing extern overload resolution
  -> selected Udon extern signature
```

extern catalog、reflection candidate discovery、Udon exposed filtering、extern signature formattingはADR-0008とADR-0016の既存実装を再利用する。backendで候補を再検索しない。

### Module、visibility、import

各moduleは同名top-level関数を1つのfunction overload setとして宣言indexへ登録する。module内部の集合はprivate候補とpublic候補を含み、module exportはpublic候補だけを含む。

`use`、`pub use`、qualified module access、grouped import、glob import、re-export、Preludeはfunction overload set全体を運ぶ。同じ名前でparameter signatureが重複しないfunction setを複数経路からimportした場合は集合を併合し、単なる名前衝突とはしない。同じ宣言へ複数経路から到達した場合は、元の`FunctionSymbol` identityを重複させない。別宣言が同じparameter signatureを持つ場合、またはfunctionと異なるsymbol categoryが同名になる場合は、既存のimport/re-export ambiguity規則を維持する。

re-exportによる公開pathと宣言identityは分離したままとする。overload setを経由しても、各候補は宣言元moduleとcanonical public pathを保持する。

### Internal identity、IR、lowering、UASM

各overloadは別の`FunctionSymbol`であり、parameter signatureを含むcompiler-internal identityを持つ。具体的なmangled representationは実装詳細であり、Sobakasuのsource-level名、公開言語仕様、安定ABIにはしない。

現在のuser-defined functionとmethodはinline loweringされる。lowererは選択済み`FunctionSymbol`をkeyとして正しいbound bodyを取得し、呼び出しごとに一意なtemporaryとlabelを生成する。このため、同名overloadが複数あってもbody、inline return label、UASM storageが衝突しない。将来non-inline internal callを導入する場合も、この一意なcompiler-internal identityからlabelを生成しなければならない。

IRとUASMには、名前だけの未解決overload call、runtime signature search、reflection-based runtime invocationを導入しない。

### Return type、cast、genericの制約

期待戻り値型をoverload selectionへ使用しない。return-type-only overloadは宣言時のduplicateであり、call siteの文脈型によって選ばない。

このADRは`as`、explicit cast expression、新しいcast syntax、user-defined conversionを導入しない。既存の変換と構文だけを維持する。

ADR-0022のとおりgeneric top-level functionは存在しないため、このADRはgeneric function inferenceを追加しない。generic `impl`から具体化されたmethodは従来どおりconcrete `FunctionSymbol`としてmethod groupへ参加し、既存monomorphizationを維持する。method固有のgeneric parameterやgeneric extern methodは対象外とする。

## Alternatives

### オーバーロードを導入せず別名を要求する

`log_string`、`log_object`のようなAPIになり、Unity/Udon wrapperとして不自然で、利用者とライブラリ作者の負担が大きいため採用しない。

### externだけオーバーロード可能にする

extern側にはすでに候補解決があるが、Sobakasuで記述するwrapper関数の名前を分ける必要が残り、標準ライブラリの問題を解決できないため採用しない。

### methodだけオーバーロード可能にする

ADR-0016の状態を維持する案だが、free functionを中心とする標準ライブラリAPIを自然に設計できないため採用しない。

### 戻り値型もsignatureに含める

期待型を用いる複雑なcall-site resolutionが必要になり、代入先やreturn文脈によって呼び出し先が変化する。単純で予測可能なcompile-time resolutionを損なうため採用しない。

### C#のoverload resolutionを完全再現する

SobakasuはC#互換言語ではなくUdon-firstの独自言語である。optional parameter、generic method inference、user-defined conversionなどを含むC#規則は現在の型システムに対して過剰であり、既存method/externの挙動とも一致しないため採用しない。

### backendまたはruntimeでoverloadを解決する

Binderで確定すべき名前と型を後段へ漏らし、ADR-0003の責務分離と決定的なUASM生成を壊すため採用しない。

## Rationale

一般のSobakasu関数をoverload可能にすれば、extern専用hackを作らずに、Unity/Udon API wrapperを自然なSobakasu APIとして公開できる。top-level関数の候補集合だけを追加し、methodとexternがすでに使用するapplicability、conversion distance、best-candidate selectionを共通化することで、既存仕様と実装を活用できる。

return typeをidentityとresolutionから除外し、argument typeだけでcompile-timeに決定することで、呼び出し結果を予測可能に保てる。Binderが一意なtargetをBound treeへ記録するため、inline lowererとUASM backendは従来どおり選択済みのbodyとextern signatureだけを扱える。

moduleとimportが候補集合を保つことは、この機能を標準ライブラリの基盤として利用するために不可欠である。public候補だけをexportし、同じ宣言identityを再利用することで、既存のvisibility、re-export、canonical pathの意味を維持できる。

## Consequences

### Positive

* Unity、VRChat、Udon wrapperを自然な同名関数として公開できる。
* top-level関数、method、`static fn`、externが同じapplicabilityとrankingを共有する。
* no-match、ambiguity、duplicateで候補signatureを示せる。
* `pub`、`use`、`pub use`、group、glob、Preludeを越えてoverload setを保持できる。
* Binder以降は一意なcall targetだけを扱い、runtime表現やbackend検索を増やさない。
* overloadごとのcompiler-internal identityにより、inline bodyと生成labelの衝突を避けられる。

### Negative

* module declaration indexとimport tableがfunction overload setを扱う必要がある。
* 同名候補の追加によりno-matchとambiguityの診断経路が増える。
* conversion distanceの合計が同じ候補は意図的にambiguityとなり、C#と同じ結果になるとは限らない。
* generic top-level function、generic method inference、explicit castによる候補選択は引き続き利用できない。
* 将来non-inline internal callを導入する場合、実装詳細のmanglingをUASM label生成へ接続する必要がある。
