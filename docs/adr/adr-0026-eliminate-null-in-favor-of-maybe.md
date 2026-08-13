# ADR-0026: Eliminate `null` in Favor of `Maybe<T>`

## Status

Accepted

## Context

SobakasuはUdon-firstの静的言語である。従来のsource languageは`null` literalと内部`Null`型を持ち、`string`、array、`object`、外部reference bindingへの代入、state constant、array element inference、boxingを特例で処理していた。このモデルでは、値が存在しない可能性が型から分からず、通常のSobakasu APIと、nullまたはinvalid referenceを返し得るUnity／VRChat／Udon境界の区別も曖昧になる。

一方、Udon ABIとCLR reference storageから物理的なnullを排除することはできない。Udon heap slotの既定値、event parameter slot、raw extern result、payload enumのinactive leafなどでは、具体的なreference型のnull storageが必要である。source-level valueの不存在とABIのplaceholderは分離しなければならない。

ADR-0021はpayload enumをtagと全payload leafへflattenし、inactive payloadはtagによって無効となるstale／default storageを保持できると定めた。ADR-0022はgeneric aggregateをBinderでconcrete typeへmonomorphizeし、ADR-0023はconcrete generic enumの`match`を通常CFGへlowerする。このため、値の不存在は専用runtime機構を追加せず、通常のgeneric payload enumとして表現できる。

ADR-0016の`extern`は、Unity／VRChat／Udon APIへ直接アクセスする明示的な低レベル境界である。StandardLibraryはその上に通常の安全側APIを構築できるが、raw externの表現力を失わせてはならない。またADR-0013どおり、SobakasuはRust風ownership、borrow checker、lifetime、typestateを導入しない。

## Decision

### Source languageから`null` literalを削除する

通常のSobakasu sourceでは`null`を値として記述できない。Lexerは`null`という旧literal spellingへ専用診断`SBK0007`を報告し、Parserの回復用にはidentifier tokenとして渡す。`NullKeyword`、null literal syntax node、Binderの`Null`型／constant、null assignment conversion、null boxing、nullによる型推論は存在しない。

既存のserialized heap-patch metadataとの互換性のため、公開`TypeKind` enumでは旧`Null`が使用していた数値16を再利用しない。これはnull型を残すものではなく、後続kindの数値を変えないためのserialization tombstoneである。

したがって、次はすべてコンパイルエラーである。

```sobakasu
let text: string = null;
state target: GameObject = null;
let value: object = null;
let values: [GameObject] = [null];
```

`null`は通常の利用可能なidentifierとして復活しない。専用診断は、旧コードの移行先として`Maybe<T>`を示す。

### `Maybe<T>`を標準の不存在表現とする

StandardLibraryの`maybe` moduleに次の通常のgeneric payload enumを定義し、Preludeから再exportする。

```sobakasu
pub enum Maybe<T> {
  Nothing,
  Just(T),
}
```

利用例は次のとおりである。

```sobakasu
let empty: Maybe<i32> = Maybe.Nothing;
let value: Maybe<i32> = Maybe.Just(42);

let result = match value {
  Maybe.Just(inner) => inner,
  Maybe.Nothing => 0,
};
```

`Maybe<T>`はcompiler built-in、lang item、well-known runtime typeではない。現在の実装ではPreludeの通常のtype export、generic type identity、expected type inferenceだけで必要な意味を表現できるため、名前文字列またはファイルパスによるcompiler special-caseを追加しない。

ADR-0023はbare variant shorthandを導入しないため、`Nothing`単独ではなく`Maybe.Nothing`を使用する。このADRだけのためにvariant name resolutionを変更しない。

`T`から`Maybe<T>`への一般的な暗黙変換は追加しない。値の存在は`Maybe.Just(value)`という明示的なenum constructionで表す。

### 既存のgeneric enum、flattening、matchを再利用する

`Maybe<T>`はADR-0022によりBinderでconcrete typeへmonomorphizeされた後、ADR-0021の通常のenum layoutを使用する。

```text
Maybe<GameObject>
  tag: i32
  Just payload: UnityEngine.GameObject
```

copy、state、function parameter／return、array、matchは既存のaggregate shallow-copy、recursive leaf layout、SoA、heap patch、CFG loweringを使用する。Maybe専用UASM型、nullable pointer、runtime generic metadata、runtime helper、opcode、backendでのnullability inferenceは追加しない。UasmAssemblerは引き続き解決済みIRのemissionだけを担う。

### Source-level不存在とABI nullを分離する

source literalとBinder null constantは削除するが、具体型が決定済みの内部storageはABI nullを保持できる。例えば次のstateは、

```sobakasu
state target: Maybe<GameObject> = Maybe.Nothing;
```

概念的に次のstorageへflattenされる。

```text
target__tag: i32 = 0
target__Just__0: UnityEngine.GameObject = null
```

後者のnullはinactive payload用のUdon heap placeholderであり、Sobakasuのsource valueではない。nullのinactive leafにはpost-assemble heap patchを作らず、型付きUASM data slotの既定値`null`を使用できる。event parameter／return slot、array／reference placeholder、raw extern resultにも同じ区別を適用する。

### Raw `extern`を低レベルescape hatchとして維持する

ADR-0016のraw extern resultを自動的に`Maybe<T>`へ変換しない。利用者は引き続き次を記述できる。

```sobakasu
let raw = extern UnityEngine.GameObject.Find("Target");
```

この値は静的には選択済みのconcrete reference型だが、実行時にはnull、destroy済みUnity object、退出済みplayer等のinvalid referenceになり得る。raw externはこの危険を利用者が扱う低レベル境界である。

### StandardLibraryのreference returnは原則`Maybe<T>`にする

StandardLibraryでreference-returning external APIをラップする場合は、APIの意味がnon-nullを保証すると明示的に判断した場合を除き、原則として`Maybe<T>`を返す。最初の代表APIとして`unity.GameObject.find`を提供する。

```sobakasu
pub static fn find(name: string) -> Maybe<Self> {
  let value = extern UnityEngine.GameObject.Find(name);
  if extern VRC.SDKBase.Utilities.IsValid(value) {
    Maybe.Just(value)
  } else {
    Maybe.Nothing
  }
}
```

実装はinstalled SDK／Udon node catalogで解決される`VRC.SDKBase.Utilities.IsValid(System.Object) -> bool`を使用する。現在のcatalogで選択されるUdon signatureは`VRCSDKBaseUtilities.__IsValid__SystemObject__SystemBoolean`である。ソースライブラリはこのUASM signature文字列をハードコードせず、通常のextern resolutionを通す。

wrapperはexternal resultを一度だけ評価し、`Utilities.IsValid`がfalseなら`Maybe.Nothing`、trueなら`Maybe.Just(value)`を構築する。単純なCLR `== null`だけに依存しない。これによりUdonがinvalidと判定できる参照も同じAPI境界で扱えるが、すべてのCLR reference typeや将来の全時点について完全なlifetime保証を与えるものではない。

StandardLibraryの全reference APIをこのADRで一括追加・変換はしない。新規または更新するwrapperはこの原則に従い、raw externは維持する。

### `Maybe<T>`はownership／lifetime保証ではない

`Maybe.Just(value)`は、その`Maybe`を構築または検査した時点で値を有効として扱ったことを表す。その後にUnity objectがDestroyされたり、`VRCPlayerApi`が退出したりしてinvalidになることは静的に防がない。

ADR-0013の決定を維持し、ownership、borrow checker、lifetime annotation、typestate、イベント間の参照有効性解析は導入しない。必要なAPI境界または利用時点で再度`Utilities.IsValid`を使うことができる。

### `string`、array、`object`、stateの規則

* `string`の不存在は`Maybe<string>`で表す。
* `[T]`にsource null elementはない。nullable elementは`[Maybe<T>]`で表す。array literal inferenceは通常の最初の型付き要素から始め、旧「最初のnon-null要素」規則を持たない。
* `object`へのsource null boxingはない。nullable objectは`Maybe<object>`で表す。
* 現行heap-patch formatでdirect `object` stateのsource initializerを安全に復元できない制限は維持する。optional stateには`state value: Maybe<object> = Maybe.Nothing;`を使用できる。
* aggregate enumのinactive payloadはADR-0021どおりtagだけで意味が決まり、default／null physical storageを保持できる。

## Alternatives

1. `null` literalとnullable reference special-caseを維持する。不存在が型から分からず、assignment、inference、boxing、array、stateの特例が残るため採用しない。
2. `T?`または第二のnullable reference type systemを導入する。既存のgeneric enumとmatchで表現できる意味を重複させ、型体系とbackendの責務を増やすため採用しない。
3. `Maybe<T>`をcompiler built-in runtime objectまたはnullable pointerとして実装する。ADR-0021／ADR-0022のconcrete aggregate layoutを迂回し、Udon専用runtime表現が増えるため採用しない。
4. すべてのraw extern reference returnを自動的に`Maybe<T>`へ変換する。ADR-0016の明示的な低レベル境界を失い、外部signatureとSobakasu wrapper policyを混同するため採用しない。
5. CLR null比較だけをsafe wrapperに使用する。Unity／VRChat／Udon固有のinvalid referenceを扱える範囲が狭いため採用しない。
6. Rust風ownership／borrow checker／lifetimeを同時に導入する。ADR-0013とUdon runtime ownership modelに反し、`Maybe<T>`が解決するpresenceの問題を越えるため採用しない。

## Rationale

通常のgeneric enumを使えば、不存在はsource typeに明示され、既存のconstruction、inference、match、monomorphization、flattening、state storageを再利用できる。source nullのためだけに存在したParser／Binder／conversion特例を除去しながら、Udon ABIに必要な具体型付きnull placeholderは維持できる。

raw extern、StandardLibrary safe wrapper、通常のSobakasu codeを三層に分けることで、低レベルAPIへのアクセスを失わず、標準APIは`Utilities.IsValid`に基づく明示的なpresenceを返せる。意味解決はBinderまでで完了し、IRとUASM backendへgeneric／nullability判断を持ち込まないため、ADR-0003の責務分離も維持される。

## Consequences

### Positive

* 値の不存在が`Maybe<T>`として型に現れる。
* `string`、array、`object`、external referenceに共通のsource-level規則を適用できる。
* null assignment、inference、boxing、array elementのcompiler special-caseを削除できる。
* generic enum、match、aggregate state、heap patchの既存実装を再利用できる。
* StandardLibraryは`Utilities.IsValid`を通じた安全側wrapperを提供できる。
* raw externとABI nullを維持し、Udon／CLR interoperabilityを損なわない。
* UASM backendはsource-level presenceを認識する必要がない。

### Negative

* 旧`null`利用コードはbreaking changeとして`Maybe<T>`へ移行する必要がある。
* `Maybe.Just`／`Maybe.Nothing`とexhaustive `match`の記述量が増える。
* `Maybe<T>`だけではUnity／VRChat referenceの将来の有効性を保証できない。
* reference-returning StandardLibrary wrapperはAPIごとにvalidity policyを設計・検証する必要がある。
* inactive reference payloadなど、sourceから見えないABI nullは引き続きcompiler／runtime実装上存在する。
