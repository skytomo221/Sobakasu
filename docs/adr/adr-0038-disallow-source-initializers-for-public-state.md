# ADR-0038: Disallow Source Initializers for Public State

## Status

Accepted

## Context

Sobakasuのstateには二種類の初期値所有モデルがある。

private stateでは、Sobakasu sourceが初期値を所有する。

```sobakasu
state count = 1;
```

initializerはcompile-time evaluationされ、既存のstate initializer、`GlobalInitializer`、heap patch semanticsを使用する。

public stateはUdonの`.export`によりUdonBehaviourのpublic variableとして公開され、Unity Inspector、serialized UdonBehaviour、`ProgramVariable` interoperabilityなどの外部側から値を与えられる。

```sobakasu
pub state count: i32;
```

従来の次の宣言は、sourceが実行時初期値を所有するように見える。

```sobakasu
pub state count: i32 = 1;
```

しかし、実際の実行時値にはInspectorに保存されたpublic variableの値が優先されることがあり、Sobakasu sourceは`1`を保証できない。source initializerとInspector serialized valueの両方が「初期値」に見える二重の所有モデルを避ける必要がある。

ADR-0025はprivate/publicを区別せず、すべてのstate declarationにinitializerを要求していた。本ADRは、そのgrammarとpublic stateにsource initializerが存在するという部分だけをsupersedeする。ADR-0014はADR-0025にsupersede済みであり、`.export`、`.sync`、publicとsyncの独立性などの歴史的経緯として参照する。

## Decision

state declarationを意味論上、次の二種類として扱う。

```text
PrivateStateDeclaration
  ::= SyncModifier? "state" Identifier TypeClause? "=" Expression ";"

PublicStateDeclaration
  ::= "pub" SyncModifier? "state" Identifier TypeClause ";"
```

正規例は次のとおりである。

```sobakasu
state count = 1;
state count: i32 = 1;
sync state count = 1;
sync(linear) state value: f32 = 0.0;

pub state count: i32;
pub sync state count: i32;
pub sync(linear) state value: f32;
```

次はsource initializerを持つため不正である。

```sobakasu
pub state count = 1;
pub state count: i32 = 1;
pub sync state count: i32 = 1;
pub sync(linear) state value: f32 = 1.0;
```

`pub state count;`も不正とする。public stateにはinitializerがなく、initializerから型を推論できないため、explicit type annotationを必須とする。

禁止されたpublic initializerはParserで式全体をconsumeして専用診断を報告し、Binderでは式をbindまたはconstant evaluationしない。これによりinitializer内部の二次診断を抑え、後続memberのparse recoveryを維持する。

正常なpublic stateは、型、public metadata、sync metadataを持ち、source initializerとsource `InitialValue`を持たない。IRは従来のstate storageを生成し、UASMは型に応じた既存placeholderを持つdata slotと`.export`を生成する。同期指定があれば`.sync`も生成する。`GlobalInitializer` heap patchは生成しない。

次の既存仕様は変更しない。

* private stateのinitializer必須、initializerからの型推論、constant evaluation、`InitialValue`、`GlobalInitializer`
* private synchronized stateとそのinitializer
* stateが常にmutableであること
* `pub`と`sync`の独立性、およびprivate synchronized state
* `.export`、`.sync`、public array／aggregate ABI validation、synchronization compatibility validation
* `pub const`、const initializer、local `let`
* `Maybe`／`Nothing` semantics

したがって、`sync state count = 1;`は引き続き合法である。initializerを禁止する条件は`sync`ではなく、`pub`によりInspector／public variable側が値の所有者になることである。

## Alternatives

### `pub state x = 1`を維持し、Inspectorが優先すると文書化する

却下する。source initializerが実行時値を保証するように見える問題が残る。

### Sobakasu initializerをInspectorより常に優先する

却下する。Inspectorで利用者が設定したserialized public variableをコンパイル時に上書きすることになり、Unity／Udonのpublic variable semanticsと相性が悪い。

### source initializerをInspectorのdefault valueとして自動転写する

今回は採用しない。再コンパイル時に、過去の自動生成defaultと利用者がInspectorで明示的に変更した値を区別する追加仕様が必要になる。本決定の目的はpublic stateの初期値所有者を一つにすることであり、その複雑性を導入しない。

## Rationale

主目的は実装上の可否ではなく、sourceの意味を明確にすることである。

`state x = 1;`は「Sobakasu sourceが初期値1を定義する」と読める。`pub state x: i32;`は「UdonBehaviour／Inspectorから値が提供される公開状態」と読める。初期値の所有境界を構文に表すことで、sourceと実行時の解釈を一致させる。

Parserは構文上禁止されたinitializerの診断とrecovery、Binderは正常なinitializer absence、型、ABI、同期compatibilityの意味解析、IR／UASMは解決済みstate storageのemissionを担当するため、既存の責務分離も維持できる。

## Consequences

### Positive

* public stateのsource initializerとInspector valueの競合がなくなる。
* sourceだけで初期値所有者を判断できる。
* public stateの型が明示される。
* Unity／Udonのpublic variable semanticsと一致する。
* private stateのinitializer semanticsを維持できる。

### Negative

* 既存の`pub state x = ...;`はbreaking changeになる。
* 既存source、tests、samples、docsの更新が必要になる。
* public stateではinitializerによる型推論を使用できない。
