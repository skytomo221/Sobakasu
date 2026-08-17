# ADR-0030: Declarative Extern Bindings

## Status

Accepted

## Context

SobakasuのStandardLibraryは、.NET、Unity、VRChat、UdonのAPIをSobakasuらしい名前と型で公開する。従来、単純な一対一のwrapperにも通常のblock bodyが必要だった。

```sobakasu
pub impl Random = extern System.Random {
  pub fn next_double() -> f64 {
    extern self.NextDouble()
  }
}
```

この形式は実行上は十分だが、大量のwrapperを生成・保守するには冗長であり、Sobakasu symbolとexternal memberの対応を得るために任意のfunction bodyを再解析する必要がある。将来のreference／documentation生成では、選択済みoverload、引数型、戻り値型、static／instance、member kindを意味解析結果から直接取得できることが望ましい。

ADR-0008とADR-0016は、installed SDKとUdon node catalogに基づくextern候補収集、overload resolution、Udon extern signatureの確定をBinderの責務としている。ADR-0029も、未解決の候補をIRやbackendへ渡さず、Binderで具体的なcall targetを一意にする。新構文はこの共通resolverを再利用し、別のoverload規則を持ってはならない。

ADR-0026はsource-levelの不存在を通常のgeneric enum `Maybe<T>`で表し、reference-returning StandardLibrary wrapperでは`VRC.SDKBase.Utilities.IsValid`を用いる方針を定めた。同時に、raw externを低レベルescape hatchとして維持し、自動的な`Maybe<T>`化を禁止している。

ADR-0003の責務分離、ADR-0011のfunction declaration、ADR-0015のzero-argument parentheses省略、ADR-0017のStandardLibrary boundaryも維持する必要がある。

## Decision

### 専用の宣言構文を追加する

function declarationのbodyとして、次の二形式だけを追加する。

```text
fn NAME [parameters] [-> return_type] = extern EXTERNAL_ACCESS
fn NAME [parameters] [-> return_type] = maybe extern EXTERNAL_ACCESS
```

例えば次を許可する。

```sobakasu
pub fn sqrt(value: f64) -> f64
  = extern System.Math.Sqrt(value)

pub impl GameObject = extern UnityEngine.GameObject {
  pub static fn find(name: string)
    = maybe extern UnityEngine.GameObject.Find(name)

  pub fn set_active(active: bool)
    = extern self.SetActive(active)
}
```

top-level function、`impl`内のinstance methodと`static fn`に同じ規則を適用する。引数がない場合の`()`省略はADR-0015の既存規則をそのまま使う。

`maybe`はこの構文の`=`直後だけで意味を持つcontextual keywordとして解析する。これは既存の`maybe` module名と`use maybe.Maybe`を壊さずに新構文を導入するためである。

一般的な`fn ... = expression`は導入しない。`=`の後に許可するのは`extern`または`maybe extern`だけであり、既存のblock bodyも引き続き有効とする。

### 宣言をdirect semantic bindingとして保持する

Parserは通常のexpression bodyではなく専用のextern binding syntax nodeを生成する。Binderは既存のextern expression resolverを一度だけ呼び、選択された具体的な`ExternMethodSymbol`をfunction symbolへ関連付ける。

意味解析結果は少なくとも次を保持する。

```text
SobakasuSymbol
ExternalDeclaringType
ExternalMemberName
ResolvedExternalSignature
InvocationKind: Static | Instance
MemberKind: Method | Getter | Setter | Constructor | Operator
ReturnBindingMode: Raw | Maybe
```

さらに、既存resolverが確定したUdon extern名、external parameter types、external return typeを同じresolved symbolから利用できる。名前だけのmember groupやsource textをmetadataとして保存しない。overloadされたAPIでは、Binderが引数型から選んだ一つの具体的なsignatureを保存する。

compilerの公開結果には、documentation generator等がParserを再実行せず利用できるread-onlyなexternal binding metadataを含める。URL生成規則やdocumentation site自体はこのADRでは定めない。

### 戻り値型を解決済みexternから決める

戻り値型を省略したraw bindingでは、選択済みextern signatureの戻り値を既存のexternal-to-Sobakasu type mappingで変換し、functionの戻り値型として推論する。

明示した場合は、解決済みextern戻り値との既存の代入互換性規則を検証する。不一致は宣言位置のdiagnosticとする。voidを返すexternには、戻り値を省略した従来どおりの`u0` functionを構成できる。

functionのsignatureはcall siteをbindする前に確定する。extern解決失敗、overloadのno-match／ambiguity、unsupported targetは既存のextern diagnosticsを再利用し、宣言的binding固有の形状またはreturn policyの失敗だけに専用diagnosticを用いる。

### `maybe extern`は通常の`Maybe<T>`を構築する

`maybe extern`のexternal resultを`T`とすると、functionの戻り値型は`Maybe<T>`になる。明示する場合は`Maybe<T>`と整合しなければならない。

`T`は`VRC.SDKBase.Utilities.IsValid`で検査可能なnon-void reference resultでなければならない。Binderはvisibleな通常のgeneric enum `Maybe<T>`とそのunit variant／single-payload variantを解決し、installed catalogから`Utilities.IsValid`の具体的なextern signatureを選択する。variant名、Udon signature文字列、backend専用Maybe表現はハードコードしない。

loweringは概念的に次を生成する。

```sobakasu
let value = extern External.Find(...);
if extern VRC.SDKBase.Utilities.IsValid(value) {
  Maybe.Just(value)
} else {
  Maybe.Nothing
}
```

external callは必ず一度だけ評価し、その一時値をvalidity checkとpayloadの双方に使う。結果はADR-0021／ADR-0022の通常のflattened generic enumとしてIRへlowerする。UASM backendは解決済みextern call、branch、aggregate leafをemitするだけで、extern resolutionやMaybe判定を行わない。

通常の`= extern`はraw resultを返し、reference resultを自動的に`Maybe<T>`へ変換しない。これによりADR-0026のraw extern escape hatchを維持する。

### 従来のwrapperを維持する

次のblock-body wrapperは引き続き有効であり、宣言的bindingへ強制移行しない。

```sobakasu
pub fn foo(value: i32) -> i32 {
  extern System.Math.Abs(value)
}
```

同じexternal memberと型を使用するraw declarative bindingは、temporary名やinternal labelを除いて、同じ解決済みUdon extern signatureと意味的に同等のcodeを生成する。

## Alternatives

### 現状のblock bodyだけを維持する

単純なbindingにもboilerplateが残り、direct bindingか任意のwrapper logicかをsyntaxから区別できない。documentation toolingがbody構造を再解析しなければならないため採用しない。

### 一般的なexpression-bodied functionを追加する

`fn foo = expression`は独立した言語機能として検討すべきであり、extern symbolとの直接対応を保証しない。今回必要なsemantic metadataを曖昧にするため採用しない。

### `optional extern`を使う

Sobakasuの既存のpresence型は`Maybe<T>`であり、新しいoptional用語を導入すると型名と構文が乖離するため採用しない。

### `option extern`を使う

同様に、既存型名`Maybe<T>`と一致せず、二つの語彙を作るため採用しない。

### `extern?`を使う

`?`はmethod名末尾に使用可能であり、punctuationの意味が衝突しやすい。raw／Maybeの意図も検索しにくいため採用しない。

### `try extern`を使う

exception処理または`Result`型を連想させるが、この構文が表すのはreference validityによるpresenceだけであるため採用しない。

### `safe extern`を使う

`Maybe<T>`は将来のlifetimeやあらゆる外部動作の安全性を保証せず、何をsafeとするか曖昧になるため採用しない。

## Rationale

1. StandardLibraryの大量生成と保守を容易にする。
2. 単純なextern wrapperのboilerplateを削減する。
3. Sobakasu APIとexternal APIの対応をcompilerが明示的なsemantic dataとして保持できる。
4. 将来の.NET／Unity／VRChat公式documentationへの自動リンク生成を可能にする。
5. 選択済みoverloadを再利用し、documentation側でCLR／Udon APIを再解決する必要をなくす。
6. `Maybe<T>`が必要なextern boundaryを構文上で明示する。
7. raw externとMaybe化されたexternをlibrary authorが明示的に選択できる。
8. static APIとinstance APIを同じbinding modelで扱う。

専用syntax、Binder上のdirect metadata、解決済みbound callを組み合わせることで、sourceの簡潔さとtooling向けの機械可読性を同時に得られる。既存resolverを唯一のoverload選択経路とし、Maybeを通常のenumへlowerするため、Parser、Binder、IR、UASM backendの責務も維持される。

## Consequences

### Positive

* 単純なStandardLibrary wrapperを一つの宣言として読める。
* return typeを解決済みexternal signatureから安全に推論できる。
* concrete overloadとUdon extern signatureを後続toolingが直接参照できる。
* top-level static APIと`impl`内のstatic／instance APIで同じmodelを使える。
* `maybe extern`はexternal callを一度だけ評価し、既存の`Maybe<T>` layoutを再利用する。
* block-body wrapperとraw externの互換性を維持する。
* backendへ型解決やreflectionを追加しない。

### Negative

* function signature collection中にextern targetとreturn typeを解決する経路が増える。
* `maybe`をcontextual keywordとしてtoolingが認識する必要がある。
* `maybe extern`はinstalled catalogに適切な`Utilities.IsValid` overloadがなければ宣言できない。
* public compiler resultに新しいmetadata surfaceが増え、将来のtoolingとの互換性を考慮する必要がある。

## Non-goals

* 一般的なexpression-bodied function
* external documentation URLのハードコード
* 完全なdocumentation site generator
* unrelatedなextern catalogの大規模拡張
* `Maybe<T>`またはADR-0026の再設計
* static field／property向けの新しいtop-level declaration syntax

