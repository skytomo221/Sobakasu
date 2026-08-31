# ADR-0040: External Struct and Enum Bindings

## Status

Accepted

## Context

Sobakasu は CLR / Udon 型を `impl T = extern Runtime.T` で宣言できるが、この形式は CLR class だけでなく struct と enum にも同じ source-level kind を与えてしまう。特に enum member は static function wrapper として表現され、通常 struct / payload enum の aggregate metadata と CLR runtime ABI identity を同時に保持できなかった。

ADR-0021 は Sobakasu 定義の struct と payload enum を Udon 上の leaf storage へ flatten する。CLR struct / enum は既存の単一 Udon ABI value であり、この表現へ流してはならない。ADR-0030 以降の declarative extern binding と generator policy、および ADR-0003 / ADR-0037 の責務分離に従い、runtime member の解決は Binder までに完了させる必要がある。

## Decision

次の external aggregate declaration を導入する。

```sobakasu
pub struct Vector3 = extern UnityEngine.Vector3 {
  x: f32 = extern x,
}

pub enum NetworkEventTarget = extern VRC.Udon.Common.Interfaces.NetworkEventTarget {
  All = extern All,
}
```

External struct field は Sobakasu 型と external member 名を必須とし、reflection による source type inference は行わない。External enum は `= extern` member binding を持つ unit variant だけを許可し、tuple / struct payload variant を拒否する。Enum の整数値や source enum tag を生成せず、CLR enum member の native ABI value を Binder で解決する。

`TypeSymbol` は source-level `AggregateKind`、external runtime identity、`IsExternalBinding` を同時に保持できるものとする。`IsAggregate` は source semantics を表し、物理 storage 判定は `UsesFlattenedAggregateStorage` として分離する。後者は aggregate かつ external binding ではない型だけを対象とする。配列、state、local、parameter、return、layout、SoA、IR lowering は物理 storage 判定を使用する。

External struct の field read/write は Binder が既存 extern catalog の getter / setter overloadへ解決する。External enum member access は Binder が CLR enum member valueへ解決する。IR と UASM backend は CLR member 名、reflection、overload、aggregate kind を再解決しない。

通常の `impl T` は external aggregate の runtime identity を利用して既存 extern method resolution を行う。`impl T = extern ...` は class 等の external type declaration として維持する。複数 external impl binding の可否は決定しない。

Standard Library Generator は static API container、class、struct/value type、enum をそれぞれ top-level API、external impl、external struct、external enum として生成する。Struct field は型付き field binding、enum member は名前 binding として出力する。Nested public CLR type は leaf nameの top-level Sobakasu declarationへ hoistし、完全な CLR nesting identity は external qualified nameに保持する。Hoist collision は既存 path/declaration collision diagnostic と明示的 `renames.types` で扱い、自動 rename、merge、silent skipを行わない。

この決定は ADR-0021 の flattening 対象を Sobakasu-owned aggregate storage に限定し、ADR-0030 / ADR-0035 / ADR-0039 の external declaration生成対象を struct / enumへ拡張する。既存の payload enum、declarative extern function、Maybe projection、language item、report invariant は維持する。

## Alternatives

1. `impl = extern` を struct / enum に使い続ける案は source kind と ABI kind を失うため採用しない。
2. External aggregate も ADR-0021 の storageへ flattenする案は native Udon ABI valueを破壊するため採用しない。
3. Enum数値を source tagへコピーする案は alias / flags と runtime identityを失うため採用しない。
4. Generatorまたはbackendで member reflectionを行う案は Binderの意味解決責務に反するため採用しない。
5. Nested CLR type用のnested Sobakasu構文や自動renameは言語・命名規則を不必要に拡張するため採用しない。

## Rationale

Source kindとphysical storageを別概念にすれば、型検査、member surface、runtime ABIのすべてを明示できる。既存 extern resolverへ早期にlowerすることで新しいbackend命令や遅延reflectionを避けられ、generated sourceも手書きsourceと同じParser/Binder validationを通る。

## Consequences

### Positive

* CLR struct / enum が正しい source-level kind と native ABI identityを持つ。
* 通常aggregateのflatteningとexternal native valueのstorageが明確に分離される。
* Enum alias値を含め、runtime member bindingを再定義せず保持できる。
* Nested public typeを既存module/collision policy内で生成できる。

### Negative

* Aggregate syntax / symbolはexternal metadataも保持する。
* Generatorはdeclaration kindごとのrenderingとmember filteringが必要になる。
* External struct fieldは型を重複記述し、CLR API変更時にdiagnosticとなり得る。
* External enumはpayload、source tag matching、payload pattern matchingを利用できない。
