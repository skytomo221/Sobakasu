# ADR-0059: Normalized Persistence Schema for Udon API Catalog v2

## Status

Proposed

## Context

Sobakasu は、Unity / VRChat SDK 上で取得した CLR 型情報と Udon の exposed API 情報を `udon-api-catalog.json` として永続化し、Compiler および Standard Library Generator から利用する。

Udon API Catalog の目的は、Unity Editor や Reflection に依存しない consumer から、対象となる Udon API を再構築できる安定した物理 API 記述を提供することである。

現在開発中の format version 2 では、Standard Library Generator を Reflection 直接参照から Catalog 参照へ移行する過程で、consumer が必要としていた情報をそのまま Catalog に追加してきた。その結果、以下の問題が生じている。

- CLR namespace、static API container 判定、表示用 signature など、導出可能または consumer 固有の情報が永続化されている。
- `members` が top-level の flat list であり、型、static / instance、member kind などの情報が各 record に重複している。
- CLR API の記述と Udon 呼び出し ABI へ lower した情報が混在している。
- `abiParameters` には generic method 用の `System.Type` operand など、元の CLR API には存在しない Compiler 向け情報が含まれている。
- inherited member が surface type ごとに扱われることで、CLR declaration identity と Udon surface の境界が曖昧になっている。
- namespace と nested type の境界を `runtimeName` の `.` だけでは区別できない。
- `unexposedClrTypeNames` のように、同じ型について完全な type record と別の補助 list が存在する。
- `unmatchedUdonSignatures` に、array intrinsic など他の構造で表現可能な signature まで残り得る。
- 空 collection が多数出力され、JSON サイズと Git diff の可読性を悪化させている。
- format version 2 への変更後、Catalog のサイズが format version 1 より大幅に増加している。

Catalog は Standard Library Generator の中間状態や report 表現を保存するものではない。

Catalog が保存すべきものは、CLR API の物理的構造、Udon が何を expose しているかという事実、および通常の CLR member model では表現できない Udon 固有 capability である。

Compiler が Udon extern ABI を呼び出すために必要な hidden operand、receiver、result storage、generic type argument の ABI 展開などは、Catalog consumer が導出する。

なお、本 ADR はまだ完成していない format version 2 のスキーマを確定するものであり、`formatVersion` は引き続き `2` とする。

## Decision

### 1. Catalog の責務

Udon API Catalog を、以下の3種類の情報から構成する。

1. CLR API と Udon exposure の対応
2. 通常の CLR member model では表現できない Udon 固有 capability
3. それらのいずれにも構造化できなかった Udon exposed signature

概念的な top-level structure は以下とする。

```json
{
  "formatVersion": 2,
  "target": {
    "unityVersion": "...",
    "vrchatSdkVersion": "..."
  },
  "namespaces": [],
  "capabilities": {},
  "unmodeledUdonSignatures": []
}
```

空の collection / object は後述する省略規則に従って出力しない。

---

### 2. namespace ごとに型を配置する

型ごとに CLR namespace を繰り返し保存せず、namespace record の下に型を配置する。

```json
{
  "name": "Example",
  "exposed": {
    "classes": [],
    "interfaces": [],
    "structs": [],
    "enums": []
  },
  "unexposed": {
    "classes": [],
    "interfaces": [],
    "structs": [],
    "enums": []
  }
}
```

型の Udon exposure と型種別は JSON structure 自体で表す。

したがって、従来の以下のような field は保存しない。

- `clrNamespace`
- `shape`
- `udonExposed`
- `unexposedClrTypeNames`

同一 CLR 型は Catalog 全体でちょうど1回だけ declaration を持つ。

同じ型が `exposed` と `unexposed` の両方に存在してはならない。

---

### 3. 型種別を構造で表す

型は以下の4種類に分類する。

- `classes`
- `interfaces`
- `structs`
- `enums`

`shape` field は使用しない。

例:

```json
{
  "name": "Example",
  "exposed": {
    "classes": [
      {
        "path": ["Foo"]
      }
    ],
    "interfaces": [
      {
        "path": ["IFoo"]
      }
    ],
    "structs": [
      {
        "path": ["Value"]
      }
    ],
    "enums": [
      {
        "path": ["Mode"],
        "underlyingType": "System.Int32",
        "constants": [
          {
            "name": "None",
            "value": "0"
          }
        ]
      }
    ]
  }
}
```

---

### 4. nested type identity

型 definition の `path` は namespace より後の CLR metadata name を segment 単位で保持する。

generic arity suffix も保持する。

```json
{
  "path": [
    "Outer`1",
    "Inner`1"
  ]
}
```

named TypeRef では CLR の canonical nested type identity として `+` を使用する。

```text
Example.Outer`1+Inner`1
```

`.` は namespace separator、`+` は nested type separator とする。

表示用に `+` や `` `1 `` を変換する処理は consumer の責務とする。

---

### 5. Catalog は型 metadata closure を保持する

Catalog に含まれる型は Udon exposed type だけに限定しない。

以下の情報を Catalog 単独で解決するために必要な CLR 型も `unexposed` type として保持する。

- direct base type
- direct interface
- member parameter type
- member return type
- constructed generic argument
- generic constraint
- enum underlying type
- その他 Catalog 内の TypeRef から必要となる型

原則として、Catalog 内に現れる named TypeRef は同一 Catalog 内の type declaration に解決できなければならない。

ただし generic parameter 自体は type declaration を持たない。

---

### 6. exposed type と unexposed type の metadata

型そのものを表す CLR metadata は、exposed / unexposed にかかわらず同じ規則で保存する。

unexposed type は単なる名前だけの stub にはしない。

一方、member catalog は原則として Udon exposed type に対してのみ生成する。

したがって、

```text
exposed type
    CLR type metadata
    exposed members
    unexposed members

unexposed type
    CLR type metadata only
```

とする。

型が closure に入った理由によって保存する metadata の種類を変更してはならない。

---

### 7. inheritance は直接関係だけ保存する

transitive inheritance closure は保存しない。

class は直接の base class のみ `baseType` に保存する。

```json
{
  "path": ["Foo"],
  "baseType": "Example.Base"
}
```

interface は直接継承する interface のみ保存する。

class / struct は直接実装する interface のみ保存する。

```json
{
  "path": ["Foo"],
  "interfaces": [
    "Example.IFoo"
  ]
}
```

consumer は Catalog の graph から transitive closure を導出する。

Reflection の `Type.GetInterfaces()` が返す継承済み interface をそのまま保存してはならない。

---

### 8. member は CLR declaration owner にだけ保存する

同じ CLR member を derived type / inherited surface ごとに複製しない。

member は実際の CLR `DeclaringType` に一度だけ保存する。

例:

```csharp
class Parent
{
    public void Foo() {}
}

class Child : Parent {}
```

`Foo` は `Parent` にだけ保存し、`Child` には再掲しない。

ただし interface declaration と concrete implementation は別の CLR member である。

```csharp
interface IFoo
{
    void Bar();
}

class Foo : IFoo
{
    public void Bar() {}
}
```

`IFoo.Bar` と `Foo.Bar` は同一 member とみなさず、それぞれ CLR declaration として扱う。

override / new による別 declaration も同様である。

---

### 9. type metadata

#### Class

class は必要に応じて以下を保持する。

- `path`
- type generic parameters
- `baseType`
- direct `interfaces`
- `abstract`
- `sealed`
- `hasPublicParameterlessConstructor`

`abstract: false`、`sealed: false` は省略する。

`hasPublicParameterlessConstructor` は public parameterless constructor が実際に存在するという CLR の物理的事実を表し、false の場合は省略する。

従来の `satisfiesDefaultConstructorConstraint` は保存しない。

generic `new()` constraint の判定は consumer が以下の情報から導出する。

- type kind
- abstract 여부
- open / constructed generic の状態
- `hasPublicParameterlessConstructor`

これにより、generic constraint 用に加工された結果ではなく元の CLR metadata を Catalog に保持する。

#### Interface

interface は必要に応じて以下を保持する。

- `path`
- type generic parameters
- direct `interfaces`

interface を `abstract class` として表現しない。

#### Struct

struct は必要に応じて以下を保持する。

- `path`
- type generic parameters
- direct `interfaces`

#### Enum

enum は `enums` collection に配置し、以下を保持する。

- `path`
- `underlyingType`
- `constants`

```json
{
  "path": ["Mode"],
  "underlyingType": "System.Int32",
  "constants": [
    {
      "name": "None",
      "value": "0"
    }
  ]
}
```

`shape: "Enum"` や `enum: { ... }` のような二重表現は使用しない。

unexposed enum でも enum metadata は保持する。

---

### 10. exposed type の member structure

exposed type の member は `exposed` と `unexposed` に分ける。

外側の `exposed / unexposed` は型そのものの Udon exposure を表し、型内部の `exposed / unexposed` は member の Udon exposure を表す。

この2つは独立した概念である。

member structure は以下とする。

```json
{
  "exposed": {
    "constructors": [],
    "instance": {
      "methods": [],
      "propertyGetters": [],
      "propertySetters": [],
      "fieldGetters": [],
      "fieldSetters": [],
      "events": []
    },
    "static": {
      "methods": [],
      "propertyGetters": [],
      "propertySetters": [],
      "fieldGetters": [],
      "fieldSetters": [],
      "events": []
    },
    "operators": []
  },
  "unexposed": {
    "constructors": [],
    "instance": {},
    "static": {},
    "operators": []
  }
}
```

実際の JSON では空 collection / object を省略する。

この structure により、以下を record field として保持しない。

- `hostType`
- `clrDeclaringType`
- `kind`
- `sourceKind`
- `isStatic`

それらは JSON 上の配置から一意に決まる。

constructor は `instance` 内には置かず `constructors` に置く。

operator は CLR 上 static member であっても、API 上の役割が異なるため `static.methods` ではなく `operators` に置く。

---

### 11. exposed member と Udon signature

Udon に exposed された member のみ `udonSignature` を保持する。

```json
{
  "name": "Log",
  "parameters": [
    {
      "name": "message",
      "type": "System.Object"
    }
  ],
  "returnType": "System.Void",
  "udonSignature": "..."
}
```

`externSignature` という名前は使用せず、Udon node の実際の signature であることを明確にするため `udonSignature` とする。

unexposed member は `udonSignature` を持たない。

「unexposed collection に存在する」という事実だけで Udon が expose していないことを表現する。

diagnostic reason は Catalog に保存しない。

Reflection または Catalog generation 自体の失敗は warning / error とし、永続 schema の `reason` field にはしない。

---

### 12. Compiler 向け ABI 情報を保存しない

Catalog は CLR API と Udon exposure の mapping であり、Udon call ABI へ lower 済みの IR ではない。

以下は保存しない。

- `abiParameters`
- `abiReturnType`
- receiver operand
- result storage
- generic method 用 hidden `System.Type` operand
- その他 Compiler が extern call を生成するための人工的 operand

例えば、

```csharp
T GetComponent<T>()
```

について Catalog が保存するのは、

- method generic parameter `T`
- CLR parameters
- CLR return type `T`
- `udonSignature`

である。

Udon ABI 上必要な generic type operand は Compiler が生成する。

---

### 13. CLR signature / presentation metadata を保存しない

以下は Catalog から削除する。

- `clrSignature`
- `displaySignature`
- `origin`

`clrSignature` は structured CLR metadata から機械的に再構築する。

`displaySignature` は report / UI presentation の責務とする。

primitive operator が Reflection 上の実在 member ではなく Catalog Generator によって補完された場合でも、通常の operator と同一形式で保存する。

その provenance を `origin` や `synthetic` field として保存しない。

primitive 型など一部の演算子は CLR Reflection 上の実在 member ではないが、Udon が公開する operator API を完全に表現するため Catalog Generator が補完する。

Catalog consumer にとって由来の違いは意味を持たないため、永続 schema では区別しない。

---

### 14. TypeRef

普通の named type は JSON string として表現する。

```json
"System.String"
```

nested type:

```json
"Example.Outer+Inner"
```

generic definition:

```json
"System.Collections.Generic.List`1"
```

複雑な型だけ JSON object とする。

概念上の TypeRef は以下とする。

```text
TypeRef =
    string
  | { array: TypeRef, rank?: int }
  | { typeParameter: int }
  | { methodParameter: int }
  | { generic: string, arguments: TypeRef[] }
```

例:

```json
{
  "array": "System.Int32"
}
```

```json
{
  "array": "System.Int32",
  "rank": 2
}
```

```json
{
  "generic": "System.Collections.Generic.List`1",
  "arguments": [
    {
      "typeParameter": 0
    }
  ]
}
```

```json
{
  "methodParameter": 0
}
```

array rank 1 は省略する。

generic parameter reference は名前ではなく scope ごとの ordinal を使用する。

type-level generic parameter と method-level generic parameter は別の field で区別する。

`ref` / `out` / `in` は TypeRef に含めない。

---

### 15. Parameter metadata

parameter は CLR source API の parameter を表す。

```json
{
  "name": "value",
  "type": "System.Int32"
}
```

`ref` / `out` / `in` がある場合は `passing` を保持する。

```json
{
  "name": "value",
  "type": "System.Int32",
  "passing": "out"
}
```

通常 parameter では `passing` を省略する。

`params` parameter は保存する。

```json
{
  "name": "values",
  "type": {
    "array": "System.Int32"
  },
  "params": true
}
```

`params: false` は省略する。

CLR の optional parameter / default value metadata は今回の Catalog には保存しない。

Sobakasu は既定引数機構を提供せず、呼び出し側が引数を明示する必要があるため、現在の consumer に optional / default metadata は不要である。

この情報を意図的に省略していることを schema の仕様として明記する。

将来 Sobakasu が既定引数または省略可能引数を導入する場合は、別途 schema 拡張を検討する。

---

### 16. Generic parameter metadata

type generic と method generic は同一の GenericParameter schema を使用する。

declaration では generic parameter の名前を保持する。

reference では名前ではなく ordinal を使用する。

保持対象には以下を含める。

- name
- reference type constraint
- non-nullable value type constraint
- default constructor constraint
- type / interface constraints

空または false の constraint は省略する。

例:

```json
{
  "genericParameters": [
    {
      "name": "T",
      "referenceType": true,
      "defaultConstructor": true,
      "constraints": [
        "System.IDisposable"
      ]
    }
  ]
}
```

---

### 17. Property

property getter / setter は独立した member collection に配置する。

getter:

```json
{
  "name": "Value",
  "type": "System.String",
  "udonSignature": "..."
}
```

setter:

```json
{
  "name": "Value",
  "type": "System.String",
  "udonSignature": "..."
}
```

setter 用の人工的な `value` ABI parameter は保存しない。

property type 自体が setter value type を表す。

indexed property は index parameter を保持する。

```json
{
  "name": "Item",
  "type": "System.String",
  "parameters": [
    {
      "name": "index",
      "type": "System.Int32"
    }
  ]
}
```

setter でも index parameters だけを `parameters` に保存し、property value は `type` から導出する。

---

### 18. Field

field getter / setter も独立 collection に配置する。

field setter 用の人工的な `value` ABI parameter は保存しない。

field type から value type を導出する。

readonly / literal field など setter が存在しない場合は setter record を生成しない。

---

### 19. CLR event

CLR `event` は CLR API metadata として扱う。

現在の Sobakasu / Udon mapping では CLR event は Udon exposed member として扱わない。

現行 Generator は `EventInfo` を列挙するが、`udonSignature` を生成せず常に unexposed として扱っている。

したがって現時点では event は `unexposed.events` にのみ生成される。

例:

```json
{
  "name": "SomethingChanged",
  "type": "System.EventHandler"
}
```

CLR event の add / remove accessor を独立した method member として保存しない。

将来 Udon が CLR event accessor に対応することが確認され、Sobakasu がそれを扱う場合でも、schema 上は `exposed.events` を利用できる。

Sobakasu の `on` event、Udon custom event、network event などは CLR `event` とは別概念であり、本項の対象ではない。

---

### 20. Array capability

array は named CLR type declaration ではないため、`classes` / `structs` 等には配置しない。

Udon が array に対して提供する intrinsic operation は `capabilities.arrays` に保存する。

```json
{
  "capabilities": {
    "arrays": [
      {
        "type": {
          "array": "System.Int32"
        },
        "constructor": "...",
        "getter": "...",
        "setter": "...",
        "length": "..."
      }
    ]
  }
}
```

保持する Udon signature は以下。

- constructor
- getter
- setter
- length

array getter / setter は必ずしも array element type 固有の Udon signature を利用するとは限らない。

UnityEngine.Object 派生型の array では object array intrinsic へ fallback する場合があるため、実際に選択された signature を保存する。

従来の `indexType` は保存しない。

array index type は Sobakasu / Udon array semantics 上 `System.Int32` として consumer が扱う。

array capability として構造化済みの signature は `unmodeledUdonSignatures` に含めてはならない。

---

### 21. Synchronization capability

Udon synchronization capability は CLR type metadata から導出できない VRChat / Udon 固有情報であるため、`capabilities.synchronization` に保存する。

```json
{
  "synchronization": [
    {
      "type": "System.Single",
      "modes": [
        "none",
        "linear",
        "smooth"
      ]
    }
  ]
}
```

array TypeRef も対象にできる。

```json
{
  "type": {
    "array": "System.Int32"
  },
  "modes": [
    "none"
  ]
}
```

mode は以下をそのまま保存する。

- `none`
- `linear`
- `smooth`

record の存在から `none` を暗黙化せず、対応 mode を明示する。

---

### 22. unmodeledUdonSignatures

従来の `unmatchedUdonSignatures` は `unmodeledUdonSignatures` に改める。

ここに保存するのは、

> Udon が expose しているが、type member、operator、array capability、その他定義済み capability のいずれにも構造化できなかった signature

だけとする。

「CLR member に match しなかった」という理由だけでこの collection に入れてはならない。

array intrinsic など別構造で正常に model 化できた signature は除外する。

この collection は Catalog Generator がまだ model 化できていない Udon surface を検出するための completeness signal として扱う。

---

### 23. 空 collection / object の省略

空 array は serialize しない。

空 object も serialize しない。

この規則を再帰的に適用する。

例:

```json
{
  "exposed": {
    "instance": {
      "methods": [...]
    }
  }
}
```

`static` に member がなければ `static` 自体を出力しない。

`unexposed` が完全に空なら `unexposed` 自体を出力しない。

`capabilities.arrays` と `capabilities.synchronization` が両方空なら `capabilities` 自体を出力しない。

consumer は省略された collection / object を空として解釈する。

`null` を空 collection の意味として使用しない。

---

### 24. Deterministic serialization

Catalog 出力は deterministic でなければならない。

少なくとも以下の canonical ordering を固定する。

namespace:

1. namespace name の ordinal 順

type:

1. nested `path` の ordinal 順

type field order:

1. `path`
2. type 固有 metadata
3. generic metadata
4. inheritance
5. member exposure

type exposure 内:

1. `constructors`
2. `instance`
3. `static`
4. `operators`

`instance` / `static` 内:

1. `methods`
2. `propertyGetters`
3. `propertySetters`
4. `fieldGetters`
5. `fieldSetters`
6. `events`

method overload:

1. name
2. generic arity
3. parameter type sequence
4. return type

property / field / event:

1. name
2. 必要に応じて type

operator:

1. name
2. parameter type sequence
3. return type

enum constant:

1. name

capability record:

1. TypeRef の canonical ordering

`unmodeledUdonSignatures`:

1. ordinal lexical order

serialization 前に canonical sorting を行い、同一入力から byte-level で安定した JSON を生成する。

---

### 25. Loader の解決手順

Catalog Loader は参照を読みながら逐次型を生成するのではなく、最低でも以下の2段階で処理する。

1. Catalog 内の全 type declaration を登録して canonical CLR type identity index を構築する。
2. `baseType`、`interfaces`、generic constraint、parameter、return type 等の TypeRef を解決する。

これにより exposed / unexposed や namespace / nested type の配置にかかわらず循環参照を解決できる。

---

### 26. Schema invariant

format version 2 の Catalog は以下を満たさなければならない。

- 同一 CLR type declaration は Catalog 全体で1回だけ存在する。
- 型の `exposed / unexposed` と member の `exposed / unexposed` は独立した概念である。
- named TypeRef は原則として同一 Catalog 内の type declaration に解決できる。
- nested type identity は `+` を用いて namespace と区別する。
- inheritance は direct relationship のみ保存する。
- inherited CLR member を derived type に複製しない。
- member は CLR declaration owner に保存する。
- unexposed type は CLR type metadata を持つが、原則 member catalog を持たない。
- exposed type は exposed member と unexposed member の両方を保持する。
- unexposed member は `udonSignature` を持たない。
- exposed member の `udonSignature` は実際に expose される Udon node signature である。
- CLR event の add / remove accessor を独立 member として保存しない。
- ABI lowering 用の人工 operand を保存しない。
- report / presentation 用情報を保存しない。
- consumer policy の判定結果を可能な限り保存しない。
- array capability として model 化された signature は `unmodeledUdonSignatures` に含めない。
- empty collection / empty object は serialize しない。
- absent collection / object は empty として解釈する。
- serialization order は deterministic である。

## Alternatives

### 現在の flat `types` / `members` schema を維持する

各 record に `hostType`、`kind`、`isStatic`、namespace 等を保持すれば実装変更は小さい。

しかし構造から導出可能な情報が大量に重複し、Catalog のサイズ、可読性、Git diff の品質が悪化する。

また CLR API、Udon exposure、Compiler ABI、Standard Library Generator の presentation metadata の境界が曖昧なまま残る。

採用しない。

### 型を `types[]` に統一して `shape` field を保存する

```json
{
  "path": ["Foo"],
  "shape": "Interface"
}
```

のように型種別を field で表す案。

一般的な schema としては単純だが、今回の Catalog では `exposed / unexposed` や member kind についても structure に意味を持たせている。

`classes / interfaces / structs / enums` に分ければ `shape` を繰り返す必要がなく、type-specific metadata も明確になるため採用しない。

### exposed type だけを Catalog に保存する

JSON は小さくなる。

しかし base class、interface、generic constraint、signature type などが unexposed の場合、Catalog だけでは型 graph を再構築できず Reflection 依存が復活する。

採用しない。

### unexposed type の全 member も保存する

Catalog は CLR API dump に近づき完全性は高まる。

しかし inheritance closure や signature dependency のためだけに収録された型から大量の不要 member が流入し、サイズが大幅に増える。

Standard Library Generator が必要とする unexposed member count は exposed type 内の unexposed member から再構築できるため採用しない。

### `satisfiesDefaultConstructorConstraint` をそのまま保存する

Compiler からは直接利用しやすい。

しかしこれは CLR の物理 metadata ではなく generic constraint validation 向けに加工した結果である。

`abstract`、type kind、generic state、`hasPublicParameterlessConstructor` から consumer が導出する方が責務分離に適するため採用しない。

### ABI parameter を Catalog で事前計算する

Compiler の実装は単純になる。

しかし CLR API metadata と Udon ABI lowering が Catalog に混在し、Compiler 実装変更が永続 schema の変更につながる。

Catalog は API mapping に限定し、ABI lowering は Compiler に置く。

### TypeRef をすべて文字列 mini-language にする

JSON はさらに短くできる。

しかし array、generic parameter、constructed generic 等を独自文字列 parser で解釈する必要があり、JSON の structured data を使う利点を失う。

普通の named type だけ文字列とし、複雑な type expression は JSON object とする。

### numeric type ID を導入する

ファイルサイズをさらに削減できる。

しかし可読性、Git diff、手動調査性を大きく損なう。

まず structural redundancy を除去し、それでもサイズが問題となる場合に別 ADR で検討する。

## Rationale

Catalog の永続 schema は、現在の consumer 実装をそのまま dump するのではなく、consumer が依存すべき安定した事実を保存する必要がある。

特に以下を設計原則とする。

- 物理的事実を保存し、導出可能な値は保存しない。
- CLR API と Udon exposure の対応を保存し、Compiler lowering を保存しない。
- report / UI presentation のためだけの情報を保存しない。
- structure で表現できる分類を各 record の文字列 field として繰り返さない。
- Udon exposed でなくても graph の再構築に必要な CLR metadata は Catalog 内に閉じる。
- consumer が Reflection や Unity Editor API に戻らなくてよい self-contained な contract とする。
- JSON は機械処理だけでなく、人間による review、debug、Git diff に耐える形式とする。
- Udon 固有の intrinsic / capability は無理に CLR member として表現せず、明示的に別領域へ分離する。

Sobakasu は Udon ファーストの言語およびツールチェーンであるため、Catalog も「一般的な .NET Reflection dump」ではなく、Udon API を正確かつ保守可能に記述することを目的とする。

一方で、Udon に合わせるために CLR API の元の意味を失ってはならない。

そのため Catalog は「CLR API の物理構造」と「Udon exposure の mapping」を保持し、実際の extern call ABI への変換は Compiler に委ねる。

## Consequences

### Positive

- `hostType`、`kind`、`isStatic`、`shape` 等の structural redundancy を削減できる。
- `clrSignature`、`displaySignature`、ABI metadata 等の大量の重複情報を削除できる。
- Catalog サイズの削減が期待できる。
- namespace / nested type の identity が明確になる。
- Interface を含む unexposed inheritance graph を Catalog 単独で再構築できる。
- Compiler と Standard Library Generator が Reflection に戻る必要がなくなる。
- Compiler の ABI lowering 方針を変更しても Catalog schema への影響を抑えられる。
- member kind や staticness が JSON structure から明確になり、人間による確認が容易になる。
- exposed / unexposed API coverage の確認が容易になる。
- array / synchronization のような Udon 固有 capability と通常 member の責務が分離される。
- `unmodeledUdonSignatures` により、Catalog Generator がまだ構造化できていない Udon API を明確に検出できる。
- deterministic ordering と empty value omission により Git diff が安定する。

### Negative

- 既存 format version 2 の Generator、Loader、Compiler、Standard Library Generator を同時に更新する必要がある。
- Loader は flat record の単純走査ではなく、全型登録後に reference を解決する複数段階処理が必要になる。
- direct interface relationship の算出は `Type.GetInterfaces()` をそのまま使用できず、Reflection metadata から正しく直接関係を導出する必要がある。
- CLR declaration owner にのみ member を保存するため、consumer は inheritance graph を利用して inherited member lookup を行う必要がある。
- `satisfiesDefaultConstructorConstraint` を削除するため、Compiler は元 metadata から generic `new()` constraint を正しく判定する必要がある。
- optional parameter / default value metadata は意図的に保持しないため、将来 Sobakasu が省略可能引数を導入する場合は schema 拡張が必要になる。
- CLR event は現状 Udon mapping を持たないため、将来 Udon 側で accessor mapping が必要になった場合は `exposed.events` の生成規則を追加する必要がある。
- primitive operator の Reflection provenance は永続化しないため、consumer から synthetic / reflected の区別はできなくなる。
- numeric interning を採用しないため、structural redundancy 削除後も JSON が一定以上大きい可能性は残る。
