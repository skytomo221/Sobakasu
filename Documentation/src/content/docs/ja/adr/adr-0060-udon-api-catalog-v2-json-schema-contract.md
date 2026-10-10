# ADR-0060: Udon API Catalog v2 JSON Schema Contract

## Status

Proposed

## Context

ADR-0059 では、Udon API Catalog format version 2 の責務と正規化方針を決定した。

主な決定は以下である。

- namespace 単位で型を配置する
- 型を `exposed` / `unexposed` に分ける
- 型種別を `classes` / `interfaces` / `structs` / `enums` の構造で表現する
- exposed type のみ member catalog を持つ
- exposed type 内では member を `exposed` / `unexposed` に分ける
- static / instance、member kind 等を JSON structure で表現する
- CLR declaration owner に member を一度だけ保存する
- inheritance は direct relationship のみ保存する
- TypeRef は普通の named type を string、複雑な型だけ object とする
- Compiler 向け ABI lowering 情報を保存しない
- array / synchronization を Udon 固有 capability として扱う
- model 化できなかった Udon signature のみ `unmodeledUdonSignatures` に残す
- empty collection / empty object は出力しない
- deterministic serialization を要求する

しかし ADR-0059 は architecture decision を定めるものであり、以下のような永続フォーマット上の詳細までは完全には規定していない。

- 各 property の required / optional
- JSON primitive type
- object union の排他条件
- enum 値
- field の省略条件
- duplicate の定義
- invalid catalog の判定条件
- canonical ordering の厳密な比較規則
- unknown property の扱い

Catalog は Compiler と Standard Library Generator の間で共有される永続 contract であるため、Generator と consumer が独自解釈を持つことを許容しない。

そのため、本 ADR で format version 2 の JSON schema contract を厳密に定義する。

本 ADR は ADR-0059 の設計判断を具体化するものであり、矛盾する場合は ADR-0059 の architecture decision を変更するのではなく、本 ADR または後続 ADR で明示的に解決する。

format version は引き続き `2` とする。

## Decision

### 1. Schema の正本

Udon API Catalog format version 2 の意味論上の正本は本 ADR とする。

併せて repository に machine-readable JSON Schema を配置する。

推奨 path:

```text
Packages/com.skytomo221.sobakasu/Editor/Tools/UdonApiCatalog/udon-api-catalog.schema.json
```

実際の配置場所は package structure に合わせて変更してよいが、schema file は source control 下に置く。

JSON Schema は本 ADR の構造規則を機械検証可能な形で表現する。

本 ADR と JSON Schema が矛盾した場合は、本 ADR を正とし、JSON Schema を修正する。

---

## 2. 共通規則

### 2.1 JSON object の unknown property

定義されていない property は禁止する。

各 schema object は概念上、

```json
{
  "additionalProperties": false
}
```

として扱う。

format version 2 に新しい永続 property を追加する場合は、本 ADR または後続 ADR で schema contract を変更する。

format version 2 が正式な互換 contract として固定された後に backward incompatible な変更を行う場合は、format version を更新する。

---

### 2.2 null

`null` は使用しない。

optional property が値を持たない場合は、その property 自体を省略する。

以下は禁止する。

```json
{
  "baseType": null
}
```

以下を使用する。

```json
{}
```

---

### 2.3 empty collection

空 array は serialize しない。

以下は禁止する。

```json
{
  "interfaces": []
}
```

値が存在しない場合は `interfaces` 自体を省略する。

consumer は absent collection を empty collection として解釈する。

---

### 2.4 empty object

子 property を1つも持たない optional object は serialize しない。

以下は禁止する。

```json
{
  "static": {}
}
```

以下とする。

```json
{}
```

この規則は再帰的に適用する。

---

### 2.5 boolean

既定値が `false` の optional boolean は、`true` の場合だけ serialize する。

例:

```json
{
  "abstract": true
}
```

以下は canonical output ではない。

```json
{
  "abstract": false
}
```

Loader は canonical catalog のみを受理し、明示的な `false` を許容するための互換処理を必須とはしない。

---

### 2.6 string

特記がない限り required string は以下を満たす。

```text
string length >= 1
```

空文字列は使用しない。

例外は global namespace の `Namespace.name` とする。

---

### 2.7 identifier casing

JSON property 名は lower camel case とする。

例:

```text
formatVersion
genericParameters
propertyGetters
udonSignature
```

---

## 3. Root Catalog

Catalog の schema は以下とする。

```text
Catalog
    formatVersion: integer                required, exactly 2
    target: Target                        required
    namespaces: Namespace[]               optional
    capabilities: Capabilities            optional
    unmodeledUdonSignatures: string[]     optional
```

JSON:

```json
{
  "formatVersion": 2,
  "target": {
    "unityVersion": "2022.3.22f1",
    "vrchatSdkVersion": "..."
  },
  "namespaces": [],
  "capabilities": {},
  "unmodeledUdonSignatures": []
}
```

上記例の空 collection / object は説明用であり、canonical serialized JSON では省略する。

`formatVersion` は必ず整数 `2` とする。

---

## 4. Target

```text
Target
    unityVersion: string        required
    vrchatSdkVersion: string    required
```

例:

```json
{
  "unityVersion": "2022.3.22f1",
  "vrchatSdkVersion": "3.10.0"
}
```

version string は Catalog Generator が取得した文字列をそのまま保存する。

Catalog schema 自体は version string の内部形式を解釈しない。

---

## 5. Namespace

```text
Namespace
    name: string        required
    exposed: TypeSet    optional
    unexposed: TypeSet  optional
```

例:

```json
{
  "name": "UnityEngine",
  "exposed": {
    "classes": [...]
  },
  "unexposed": {
    "interfaces": [...]
  }
}
```

global namespace は、

```json
{
  "name": ""
}
```

で表す。

`name` に末尾 `.` を含めてはならない。

nested type 名を namespace に含めてはならない。

同じ `name` を持つ Namespace record は Catalog 内に複数存在してはならない。

---

## 6. TypeSet

```text
TypeSet
    classes: ClassType[]        optional
    interfaces: InterfaceType[] optional
    structs: StructType[]       optional
    enums: EnumType[]           optional
```

空 TypeSet は禁止する。

したがって、

```json
{
  "exposed": {}
}
```

は invalid とする。

`exposed` または `unexposed` の中に少なくとも1つ type declaration が存在する場合だけ object を出力する。

---

## 7. Type path

全 nominal type declaration は `path` を持つ。

```text
path: string[] required
```

条件:

```text
path.length >= 1
```

各 segment は空文字列であってはならない。

例:

```json
{
  "path": ["GameObject"]
}
```

nested type:

```json
{
  "path": [
    "Outer`1",
    "Inner`1"
  ]
}
```

`path` の各 segment は CLR metadata name を使用する。

generic type では metadata arity suffix を保持する。

例:

```text
List`1
Dictionary`2
Outer`1
```

namespace は `path` に含めない。

---

## 8. Canonical nominal type identity

Namespace:

```text
Example
```

path:

```json
[
  "Outer`1",
  "Inner`1"
]
```

から canonical nominal identity を以下のように構築する。

```text
Example.Outer`1+Inner`1
```

規則:

```text
namespace separator = "."
nested type separator = "+"
```

global namespace の場合は namespace prefix を付与しない。

```text
Outer+Inner
```

同一 canonical nominal identity を持つ type declaration は Catalog 全体で1回だけ存在できる。

---

## 9. ClassType

```text
ClassType
    path: string[]                              required
    genericParameters: GenericParameter[]       optional
    abstract: boolean                           optional, true only
    sealed: boolean                             optional, true only
    hasPublicParameterlessConstructor: boolean  optional, true only
    baseType: TypeRef                           optional
    interfaces: TypeRef[]                       optional
    exposed: MemberExposure                     optional
    unexposed: MemberExposure                   optional
```

ただし `MemberExposure` を持てるのは外側の `TypeSet` が `Namespace.exposed` の場合だけとする。

したがって、

```text
Namespace.unexposed.classes[*].exposed
Namespace.unexposed.classes[*].unexposed
```

は禁止する。

unexposed type は member catalog を持たない。

---

## 10. InterfaceType

```text
InterfaceType
    path: string[]                         required
    genericParameters: GenericParameter[]  optional
    interfaces: TypeRef[]                  optional
    exposed: MemberExposure                optional
    unexposed: MemberExposure              optional
```

member exposure は `Namespace.exposed.interfaces` の場合だけ許可する。

`baseType`、`abstract`、`sealed`、`hasPublicParameterlessConstructor` は禁止する。

`interfaces` は直接継承する interface のみを含む。

---

## 11. StructType

```text
StructType
    path: string[]                         required
    genericParameters: GenericParameter[]  optional
    interfaces: TypeRef[]                  optional
    exposed: MemberExposure                optional
    unexposed: MemberExposure              optional
```

member exposure は `Namespace.exposed.structs` の場合だけ許可する。

以下は禁止する。

```text
baseType
abstract
sealed
hasPublicParameterlessConstructor
```

value type の implicit base type `System.ValueType` は `baseType` として保存しない。

---

## 12. EnumType

```text
EnumType
    path: string[]                    required
    underlyingType: TypeRef           required
    constants: EnumConstant[]         optional
    exposed: MemberExposure           optional
    unexposed: MemberExposure         optional
```

member exposure は `Namespace.exposed.enums` の場合だけ許可する。

通常 enum に member catalog を生成する必要がなければ `exposed` / `unexposed` は省略する。

`underlyingType` は CLR が許容する integral enum underlying type でなければならない。

例:

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

---

## 13. EnumConstant

```text
EnumConstant
    name: string   required
    value: string  required
```

`value` は underlying integral value の10進文字列表現とする。

signed underlying type は signed decimal を使用する。

例:

```json
{
  "name": "Negative",
  "value": "-1"
}
```

unsigned underlying type は unsigned decimal を使用する。

hexadecimal 表現は使用しない。

同一 enum 内で `name` は一意でなければならない。

複数の enum constant が同じ numeric value を持つことは許容する。

---

## 14. MemberExposure

```text
MemberExposure
    constructors: Constructor[]        optional
    instance: MemberSet                optional
    static: MemberSet                  optional
    operators: Operator[]              optional
```

空 MemberExposure は禁止する。

constructor は `instance` の中には置かない。

operator は `static.methods` の中には置かない。

---

## 15. MemberSet

```text
MemberSet
    methods: Method[]                    optional
    propertyGetters: PropertyAccessor[]  optional
    propertySetters: PropertyAccessor[]  optional
    fieldGetters: FieldAccessor[]        optional
    fieldSetters: FieldAccessor[]        optional
    events: Event[]                      optional
```

空 MemberSet は禁止する。

collection の位置そのものが member kind を表す。

member record に以下の property を追加してはならない。

```text
kind
sourceKind
isStatic
origin
hostType
clrDeclaringType
```

---

## 16. Exposed と unexposed member の共通規則

`MemberExposure` 自体の record schema は exposed / unexposed で共通とする。

ただし `udonSignature` の規則だけが異なる。

```text
Type.exposed 以下の member:
    udonSignature REQUIRED

Type.unexposed 以下の member:
    udonSignature PROHIBITED
```

例:

```json
{
  "exposed": {
    "instance": {
      "methods": [
        {
          "name": "Foo",
          "returnType": "System.Void",
          "udonSignature": "..."
        }
      ]
    }
  },
  "unexposed": {
    "instance": {
      "methods": [
        {
          "name": "Bar",
          "returnType": "System.Void"
        }
      ]
    }
  }
}
```

---

## 17. Constructor

```text
Constructor
    genericParameters: GenericParameter[]  prohibited
    parameters: Parameter[]                optional
    udonSignature: string                  conditional
```

constructor name は保存しない。

declaring type は record の配置から決定する。

constructor return type は保存しない。

例:

```json
{
  "parameters": [
    {
      "name": "value",
      "type": "System.Int32"
    }
  ],
  "udonSignature": "..."
}
```

---

## 18. Method

```text
Method
    name: string                           required
    genericParameters: GenericParameter[]  optional
    parameters: Parameter[]                optional
    returnType: TypeRef                    required
    udonSignature: string                  conditional
```

`System.Void` method でも `returnType` は省略せず、

```json
{
  "returnType": "System.Void"
}
```

とする。

method overload identity は少なくとも以下から構成される。

```text
name
generic arity
parameter TypeRef sequence
parameter passing mode sequence
return TypeRef
```

CLR metadata 上 overload identity に return type は通常使用されないが、Catalog の deterministic ordering と Udon mapping validation では return type も比較対象とする。

---

## 19. PropertyAccessor

```text
PropertyAccessor
    name: string           required
    type: TypeRef          required
    parameters: Parameter[] optional
    udonSignature: string  conditional
```

通常 property:

```json
{
  "name": "Value",
  "type": "System.String",
  "udonSignature": "..."
}
```

indexer:

```json
{
  "name": "Item",
  "type": "System.String",
  "parameters": [
    {
      "name": "index",
      "type": "System.Int32"
    }
  ],
  "udonSignature": "..."
}
```

setter で property value を `parameters` に追加してはならない。

setter value type は `type` から導出する。

getter / setter の区別は collection path で表す。

---

## 20. FieldAccessor

```text
FieldAccessor
    name: string          required
    type: TypeRef         required
    udonSignature: string conditional
```

例:

```json
{
  "name": "x",
  "type": "System.Single",
  "udonSignature": "..."
}
```

field setter 用の artificial `value` parameter は保存しない。

getter / setter の区別は collection path から導出する。

---

## 21. Event

```text
Event
    name: string
    type: TypeRef
    udonSignature: string conditional
```

現時点では Catalog Generator は CLR event を Udon exposed として model 化しない。

したがって canonical format version 2 では通常、

```text
Type.unexposed.instance.events
Type.unexposed.static.events
```

にのみ Event が現れる。

例:

```json
{
  "name": "Changed",
  "type": "System.EventHandler"
}
```

CLR event の add / remove accessor を独立した Method として保存しない。

将来 `exposed.events` を生成する場合は、その Event record に `udonSignature` が必須となる。

---

## 22. Operator

```text
Operator
    name: string           required
    parameters: Parameter[] required
    returnType: TypeRef     required
    udonSignature: string   conditional
```

operator の `parameters` は1件以上でなければならない。

通常 unary operator は1 parameter、binary operator は2 parameters を持つ。

Catalog schema は将来の他 arity を構文上禁止しない。

例:

```json
{
  "name": "op_Addition",
  "parameters": [
    {
      "name": "left",
      "type": "System.Int32"
    },
    {
      "name": "right",
      "type": "System.Int32"
    }
  ],
  "returnType": "System.Int32",
  "udonSignature": "..."
}
```

primitive synthetic operator と Reflection 上の operator は同一 schema を使用する。

provenance field は存在しない。

---

## 23. Parameter

```text
Parameter
    name: string        required
    type: TypeRef       required
    passing: Passing    optional
    params: boolean     optional, true only
```

`passing` が省略された場合は normal value parameter とする。

許容値:

```text
"ref"
"out"
"in"
```

それ以外は禁止する。

例:

```json
{
  "name": "value",
  "type": "System.Int32",
  "passing": "out"
}
```

`params`:

```json
{
  "name": "values",
  "type": {
    "array": "System.Int32"
  },
  "params": true
}
```

`params: true` の parameter type は array TypeRef でなければならない。

`params` と `passing` は同時に指定してはならない。

---

## 24. optional / default parameter

以下の CLR metadata は format version 2 では永続化しない。

```text
IsOptional
HasDefaultValue
DefaultValue
OptionalAttribute
```

したがって Parameter に以下の property は存在しない。

```text
optional
defaultValue
```

Sobakasu は現在既定引数機構を持たず、呼び出し側で全 argument を明示するため、この metadata は consumer に不要である。

この省略は意図的な schema decision である。

---

## 25. GenericParameter

type generic parameter と method generic parameter は同じ declaration schema を使用する。

```text
GenericParameter
    name: string                    required
    variance: Variance              optional
    referenceType: boolean          optional, true only
    nonNullableValueType: boolean   optional, true only
    defaultConstructor: boolean     optional, true only
    constraints: TypeRef[]          optional
```

`variance` 許容値:

```text
"in"
"out"
```

invariant の場合は省略する。

例:

```json
{
  "name": "T",
  "referenceType": true,
  "defaultConstructor": true,
  "constraints": [
    "System.IDisposable"
  ]
}
```

generic parameter declaration の順序が ordinal を定義する。

最初の parameter の ordinal は `0` とする。

`constraints` には CLR の type / interface constraints を保存する。

---

## 26. TypeRef

TypeRef は以下の tagged union とする。

```text
TypeRef =
    NamedTypeRef
  | ArrayTypeRef
  | TypeParameterRef
  | MethodParameterRef
  | ConstructedGenericTypeRef
```

---

## 27. NamedTypeRef

NamedTypeRef は JSON string とする。

```json
"System.String"
```

nested:

```json
"Example.Outer+Inner"
```

generic definition:

```json
"System.Collections.Generic.List`1"
```

空文字列は禁止する。

named type identity は canonical CLR nominal identity を使用する。

source language 表示名を使用してはならない。

---

## 28. ArrayTypeRef

```text
ArrayTypeRef
    array: TypeRef    required
    rank: integer     optional
```

rank 1:

```json
{
  "array": "System.Int32"
}
```

rank > 1:

```json
{
  "array": "System.Int32",
  "rank": 2
}
```

`rank` の条件:

```text
rank >= 2
```

rank 1 を明示してはならない。

以下は canonical ではなく invalid とする。

```json
{
  "array": "System.Int32",
  "rank": 1
}
```

jagged array:

```json
{
  "array": {
    "array": "System.Int32"
  }
}
```

---

## 29. TypeParameterRef

type declaration の generic parameter を参照する。

```text
TypeParameterRef
    typeParameter: integer required
```

条件:

```text
typeParameter >= 0
```

例:

```json
{
  "typeParameter": 0
}
```

参照 ordinal は enclosing type declaration の `genericParameters` index 内で有効でなければならない。

---

## 30. MethodParameterRef

method generic parameter を参照する。

```text
MethodParameterRef
    methodParameter: integer required
```

条件:

```text
methodParameter >= 0
```

例:

```json
{
  "methodParameter": 0
}
```

参照 ordinal はその Method / Operator 等の method generic declaration scope 内で有効でなければならない。

method generic parameter scope を持たない record から MethodParameterRef を使用してはならない。

---

## 31. ConstructedGenericTypeRef

```text
ConstructedGenericTypeRef
    generic: string
    arguments: TypeRef[]
```

両方 required。

例:

```json
{
  "generic": "System.Collections.Generic.Dictionary`2",
  "arguments": [
    "System.String",
    "System.Int32"
  ]
}
```

`arguments` は空であってはならない。

`generic` は generic type definition の canonical nominal identity でなければならない。

`arguments.length` は definition の CLR generic arity と一致しなければならない。

---

## 32. TypeRef object union の排他性

object 形式の TypeRef は、以下の discriminator property のうちちょうど1種類だけを持つ。

```text
array
typeParameter
methodParameter
generic
```

例えば以下は禁止する。

```json
{
  "array": "System.Int32",
  "typeParameter": 0
}
```

以下も禁止する。

```json
{
  "generic": "Example.Foo`1",
  "typeParameter": 0,
  "arguments": [...]
}
```

`arguments` は `generic` と組み合わせた場合だけ許可する。

`rank` は `array` と組み合わせた場合だけ許可する。

---

## 33. Generic arity

type generic arity は `genericParameters.length` から取得する。

別の `genericArity` field は保存しない。

type metadata name の `` `N `` と `genericParameters.length` は整合していなければならない。

non-generic type は `genericParameters` を持たず、metadata name に arity suffix を持たない。

nested generic type では各 path segment の arity はその CLR nested type segment 自身が宣言する generic parameter 数と整合しなければならない。

---

## 34. Member identity と duplicate

同じ member collection 内で、同一 physical member identity を持つ record は複数存在してはならない。

Method の identity:

```text
name
generic parameter count
parameter type sequence
parameter passing sequence
```

Constructor:

```text
parameter type sequence
parameter passing sequence
```

PropertyAccessor:

```text
name
index parameter type sequence
index parameter passing sequence
type
```

FieldAccessor:

```text
name
type
```

Event:

```text
name
type
```

Operator:

```text
name
parameter type sequence
parameter passing sequence
returnType
```

exposed と unexposed の両 collection に同一 CLR member identity が同時に存在してはならない。

---

## 35. Declaring type

member record 自体は declaring type identity を保持しない。

member を包含する type declaration が CLR declaration owner を表す。

inherited member を derived type に複製してはならない。

Loader / Compiler は inheritance graph を利用して inherited member を解決する。

---

## 36. Direct inheritance

`baseType` は class の直接 base class だけを表す。

`interfaces` は直接 relation のみを表す。

transitive relation を重複保存してはならない。

Generator は `Type.GetInterfaces()` の結果をそのまま保存してはならず、direct interface set を算出する。

---

## 37. hasPublicParameterlessConstructor

`ClassType.hasPublicParameterlessConstructor` は、

```text
public instance parameterless constructor が CLR metadata 上存在する
```

ことだけを表す。

generic `new()` constraint を満たすかどうかそのものは表さない。

以下の derived field は存在しない。

```text
satisfiesDefaultConstructorConstraint
```

consumer は必要に応じて、

```text
type category
abstract
generic construction state
hasPublicParameterlessConstructor
```

から constraint validation を行う。

---

## 38. Capabilities

```text
Capabilities
    arrays: ArrayCapability[]                      optional
    synchronization: SynchronizationCapability[]  optional
```

空 Capabilities object は禁止する。

---

## 39. ArrayCapability

```text
ArrayCapability
    type: ArrayTypeRef    required
    constructor: string   required
    getter: string        required
    setter: string        required
    length: string        required
```

例:

```json
{
  "type": {
    "array": "UnityEngine.Transform"
  },
  "constructor": "...",
  "getter": "...",
  "setter": "...",
  "length": "..."
}
```

`type` は rank 1 ArrayTypeRef でなければならない。

`indexType` は保存しない。

array index type は consumer が `System.Int32` として扱う。

各 signature は Udon が実際に expose しており、Generator がその array capability に使用すると決定した signature でなければならない。

Object-derived array 等で fallback intrinsic を使用する場合、fallback 後の実際の Udon signature を保存する。

同一 ArrayTypeRef の ArrayCapability は1件だけ存在できる。

---

## 40. SynchronizationCapability

```text
SynchronizationCapability
    type: TypeRef       required
    modes: SyncMode[]   required
```

`modes` は1件以上必要。

SyncMode の許容値:

```text
"none"
"linear"
"smooth"
```

同一 `modes` 内に duplicate は許可しない。

例:

```json
{
  "type": "System.Single",
  "modes": [
    "none",
    "linear",
    "smooth"
  ]
}
```

array:

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

同一 TypeRef に対する SynchronizationCapability は1件だけ存在できる。

---

## 41. unmodeledUdonSignatures

```text
unmodeledUdonSignatures: string[]
```

各要素は non-empty Udon exposed signature とする。

duplicate を許可しない。

以下のいずれかとして model 化された signature は含めてはならない。

- exposed member の `udonSignature`
- operator の `udonSignature`
- ArrayCapability の `constructor`
- ArrayCapability の `getter`
- ArrayCapability の `setter`
- ArrayCapability の `length`
- 将来追加された他の明示的 capability の Udon signature

`unmodeledUdonSignatures` は、

```text
Udon exposed signature
-
Catalog が構造化して説明できた Udon signature
```

の差集合を表す。

---

## 42. Canonical ordering

JSON object property order と array ordering を deterministic に固定する。

### Root property order

```text
formatVersion
target
namespaces
capabilities
unmodeledUdonSignatures
```

### Target

```text
unityVersion
vrchatSdkVersion
```

### Namespace

```text
name
exposed
unexposed
```

Namespace array は `name` の ordinal ascending order。

### TypeSet property order

```text
classes
interfaces
structs
enums
```

各 type array は `path` の canonical ordinal comparison で ascending order。

path comparison は segment ごとの ordinal string comparison とする。

同じ prefix の場合、短い path を先にする。

### ClassType

```text
path
genericParameters
abstract
sealed
hasPublicParameterlessConstructor
baseType
interfaces
exposed
unexposed
```

### InterfaceType

```text
path
genericParameters
interfaces
exposed
unexposed
```

### StructType

```text
path
genericParameters
interfaces
exposed
unexposed
```

### EnumType

```text
path
underlyingType
constants
exposed
unexposed
```

### MemberExposure

```text
constructors
instance
static
operators
```

### MemberSet

```text
methods
propertyGetters
propertySetters
fieldGetters
fieldSetters
events
```

### Method ordering

ascending by:

```text
name
generic parameter count
parameter TypeRef sequence
parameter passing sequence
returnType
udonSignature
```

最後の `udonSignature` は deterministic tie-breaker としてだけ使用する。

### Constructor ordering

ascending by:

```text
parameter TypeRef sequence
parameter passing sequence
udonSignature
```

### Property ordering

ascending by:

```text
name
index parameter TypeRef sequence
type
udonSignature
```

### Field ordering

ascending by:

```text
name
type
udonSignature
```

### Event ordering

ascending by:

```text
name
type
udonSignature
```

### Operator ordering

ascending by:

```text
name
parameter TypeRef sequence
parameter passing sequence
returnType
udonSignature
```

### GenericParameter

declaration ordinal を保持するため source declaration order のままとする。

sort してはならない。

### Parameter

CLR declaration order を保持する。

sort してはならない。

### interfaces / constraints

canonical TypeRef comparison による ascending order。

### Enum constants

`name` の ordinal ascending order。

### Synchronization modes

canonical order:

```text
none
linear
smooth
```

### unmodeledUdonSignatures

ordinal ascending order。

---

## 43. Canonical TypeRef comparison

deterministic sorting 用に TypeRef comparison を定義する。

category order:

```text
Named
Array
TypeParameter
MethodParameter
ConstructedGeneric
```

同一 category 内:

Named:

```text
canonical type identity ordinal comparison
```

Array:

```text
element TypeRef
rank
```

rank 省略時は `1` として比較する。

TypeParameter:

```text
ordinal
```

MethodParameter:

```text
ordinal
```

ConstructedGeneric:

```text
generic definition identity
argument count
arguments lexicographically
```

---

## 44. Catalog validation

Loader は少なくとも以下を invalid catalog として拒否する。

- `formatVersion != 2`
- required property の欠落
- unknown property
- required string が空
- duplicate namespace
- duplicate nominal type identity
- exposed / unexposed 両方への同一 type declaration
- invalid type path
- invalid generic arity
- TypeRef union の排他性違反
- generic argument arity mismatch
- generic parameter ordinal の範囲外参照
- method generic parameter ordinal の範囲外参照
- unknown named TypeRef
- invalid direct inheritance reference
- duplicate member identity
- exposed と unexposed に同一 member identity が存在する
- exposed member の `udonSignature` 欠落
- unexposed member に `udonSignature` が存在する
- invalid `passing`
- `params` と `passing` の同時指定
- non-array parameter に `params: true`
- invalid enum underlying type
- duplicate enum constant name
- invalid synchronization mode
- duplicate synchronization mode
- duplicate capability record
- empty collection の明示
- empty optional object の明示
- `null`
- array rank < 1
- explicit `rank: 1`
- capability と `unmodeledUdonSignatures` の重複

Generator が出力する Catalog は必ずこの validation を通過しなければならない。

---

## 45. Self-contained type graph

Catalog 内の named TypeRef は原則として同一 Catalog 内の type declaration に解決可能でなければならない。

この invariant は exposed / unexposed を問わない。

例:

```text
Example.Foo
    interfaces -> Example.IFoo
```

なら `Example.IFoo` の type declaration が Catalog 内に存在しなければならない。

ただし以下は nominal declaration lookup の対象外とする。

- TypeParameterRef
- MethodParameterRef

ArrayTypeRef は element TypeRef を再帰的に解決する。

ConstructedGenericTypeRef は generic definition および arguments を再帰的に解決する。

---

## 46. Serialization

Catalog Generator は canonical form だけを出力する。

以下のような「意味は同じだが canonical ではない表現」を生成してはならない。

```json
{
  "abstract": false
}
```

```json
{
  "interfaces": []
}
```

```json
{
  "rank": 1,
  "array": "System.Int32"
}
```

```json
{
  "static": {}
}
```

serialization は同一入力から byte-for-byte 同一 JSON を生成できる deterministic なものでなければならない。

line ending は LF とする。

UTF-8 BOM は付与しない。

file 末尾には newline を1つ付与する。

---

## 47. Parsing と内部 model

Persisted JSON schema と Compiler / Generator 内部 DTO を同一構造にすることは要求しない。

Loader は persisted schema を読み、必要に応じて consumer 向け内部 model に変換してよい。

特に Compiler は以下を Catalog から導出してよい。

- inherited member lookup
- transitive supertype closure
- static API container 判定
- generic `new()` constraint satisfaction
- Udon extern ABI operands
- receiver operand
- generic type argument operand
- result storage
- source-language-facing type name
- display signature
- CLR member ID

これらを persisted Catalog に逆流させてはならない。

---

## Alternatives

### ADR-0059 に厳密な schema をすべて含める

設計判断と wire-format contract を1文書にまとめられる。

しかし architecture decision と機械的な schema 詳細が混在し、設計理由を確認したい場合も field-level 仕様を読まなければならなくなる。

また schema の細部を変更すると architecture decision 自体の変更に見えやすい。

採用しない。

### C# DTO を schema の正本とする

実装量は少ない。

しかし serializer の既定動作、nullable field、collection 初期値などが暗黙の wire contract となり、言語や serializer implementation に schema が依存する。

採用しない。

### JSON Schema のみを正本とする

機械検証には適する。

しかし inheritance の direct-only rule、declaration ownership、Udon signature completeness、canonical ordering など、JSON Schema 単体では表現しにくい semantic invariant が存在する。

したがって ADR を意味論上の正本とし、JSON Schema を構造 validation の実行可能表現とする。

### unknown property を許容する

forward compatibility は高くなる。

しかし consumer ごとに無視される field が増えると、同じ `formatVersion` が異なる意味を持つ可能性がある。

Udon API Catalog は controlled artifact であり、Generator と consumer を同一 package 内で更新できるため、strict validation を優先する。

採用しない。

## Rationale

Udon API Catalog は単なる diagnostic dump ではなく、Compiler と Standard Library Generator が依存する persistent contract である。

persistent contract に曖昧さがあると、

- Generator が書いた情報を Compiler が異なる意味で解釈する
- consumer ごとに fallback behavior が増える
- 不完全な Catalog が silent に受理される
- schema migration の境界が曖昧になる
- JSON size 削減のために削除した metadata が別名で再導入される

といった問題が発生する。

そのため format version 2 では、

> 同じ Catalog JSON から、どの consumer も同じ物理 API graph を再構築できる

ことを schema contract の目標とする。

また、canonical serialization を contract に含めることで、generated artifact の Git diff、review、regression test を安定させる。

## Consequences

### Positive

- Generator と Loader の解釈差を排除できる。
- invalid / incomplete Catalog を早期に検出できる。
- TypeRef の ambiguous な object を防止できる。
- exposed / unexposed の意味が機械的に検証可能になる。
- duplicate type / member を確実に検出できる。
- empty object や redundant boolean を排除し、Catalog size を抑えられる。
- deterministic output を regression test できる。
- JSON Schema による automated validation を追加できる。
- persisted contract と consumer internal model を明確に分離できる。
- 将来 schema を変更する際に compatibility boundary が明確になる。

### Negative

- Generator / Loader の validation 実装量が増える。
- direct inheritance や generic arity など、JSON Schema だけでは検証できない semantic validation が必要になる。
- strict unknown-property rejection により、format version 2 の field 追加には明示的な contract 更新が必要になる。
- canonical serialization の sort / omission rule をすべての Generator path で維持する必要がある。
- Catalog test fixture を新 schema に全面更新する必要がある。
- format version 2 が正式に固定された後は、互換性を壊す schema 変更について新しい format version が必要になる。
