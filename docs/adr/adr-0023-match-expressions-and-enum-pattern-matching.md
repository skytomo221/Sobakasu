# ADR-0023: Match Expressions and Enum Pattern Matching

## Status

Accepted

## Context

Sobakasu は ADR-0021 により、payload enum を宣言順の `i32` tag と全 variant の payload leaf storage として表現する。aggregate の代入、引数、戻り値は recursive leaf flattening と field-wise shallow copy によって処理される。ADR-0022 は generic enum と `Option<T>` のような constructed type を導入し、Binder で concrete type へ monomorphize してから同じ layout へ渡すことを決定した。ただし、両 ADR は `match` と pattern matching をそれぞれの決定範囲外としていた。

Payload enum の利用者には、現在の variant を判定し、active payload を型安全に取り出す source construct が必要である。variant ごとの問い合わせ method だけでは、利用者が tag の判定と payload の取得を手作業で組み合わせることになり、網羅性も保証できない。

Udon VM は pattern matching runtime や user-defined union operation を提供しない。一方、既存の tag + payload representation は、compile time に tag comparison、branch、payload copy からなる通常 CFG へ変換できる。Sobakasu は ADR-0012 により control flow を expression-oriented に扱うため、variant 分岐も値を生成できる expression として整合させる必要がある。

この ADR は ADR-0021 または ADR-0022 を supersede しない。それらが記録した当時の out-of-scope 項目も変更しない。本決定は ADR-0021 の enum tag + payload storage と aggregate shallow-copy / flattening、および ADR-0022 の concrete generic enum / monomorphization を前提として、その上に限定された第一段階の pattern matching を追加する。

## Decision

### Match expression

`match` を statement 専用構文ではなく expression として導入する。

```sobakasu
match expression {
  pattern => expression,
  pattern => expression,
}
```

Arm の右辺には通常の expression と block expression のどちらも使用できる。arm 間の comma は必須とし、最後の arm の trailing comma は任意とする。scrutinee の後の `{` は aggregate initializer として解釈せず、`if` と `while` が使用する既存の aggregate-initializer suppression と同じ原則で match body の開始として解釈する。

Arm は source order で検査され、最初に一致した arm だけを実行する。Pattern は expression syntax の再解釈ではなく専用の syntax と bound representation を持つ。

### Version 1 patterns

Version 1 は次の pattern だけを受理する。

* wildcard pattern `_`
* qualified enum unit variant pattern: `Option.None`
* qualified enum tuple variant pattern: `Option.Some(value)`
* qualified enum struct variant pattern: `WebEvent.Click { x, y }`
* integer、boolean、character、string literal pattern

Tuple payload の各要素は identifier binding または `_` に限定する。tuple arity は declaration と完全に一致しなければならない。Struct variant pattern は field と同名の binding を作る shorthand だけを許し、field 順は問わないが、unknown、duplicate、missing field を診断する。

Enum pattern は scrutinee の enum declaration identity と一致しなければならない。generic enum では scrutinee の concrete constructed type を基準に variant と payload type を解決する。bare variant shorthand は導入せず、既存の qualified name resolution に従う。

Float literal、`null`、guard、or pattern、range pattern、nested pattern、通常 struct の destructuring、`if let`、`while let` は version 1 の対象外とする。

### Pattern bindings

Payload binding は該当 arm だけを scope とする immutable local binding であり、既存の local shadowing 規則に従う。同一 pattern 内の duplicate binding は診断する。`mut` pattern syntax は導入しない。

Binding type は解決済み variant の concrete payload type とする。したがって `Option<i32>.Some(value)` の `value` は `i32` である。Aggregate payload も特殊な参照 binding にはせず、ADR-0013 と ADR-0021 の value semantics および field-wise shallow copy を用いる。

### Exhaustiveness and reachability

すべての `match` は exhaustive でなければならない。

* enum は全 variant または wildcard で cover する。
* `bool` は `true` と `false` の両方、または wildcard で cover する。
* integer、character、string は有限全値の列挙を行わず、wildcard を必須とする。

Binder は単純な coverage set を source order で更新する。Wildcard より後の arm、同じ enum variant の二度目以降、同じ literal の二度目以降、enum 全 variantまたは `bool` の両値を cover した後の wildcard は unreachable として診断する。Version 1 は guard や nested pattern を持たないため、generalized pattern usefulness algorithm は導入しない。

### Result type and Never

Reachable arm の結果型は ADR-0012 の expression-oriented control flow と同様に同一型でなければならない。`match` 専用の common supertype inference、implicit widening、`object` erasure は行わない。

既存の内部 `Never` semantics を再利用する。`Never` arm は他の reachable arm の型決定に参加せず、値を返す reachable arm の型を match expression の型にできる。全 reachable arm が `Never` の場合、match expression も `Never` とする。

### Evaluation and lowering

Scrutinee は副作用の有無や型にかかわらず一度だけ評価し、Lowerer が temporary logical storage に materialize する。Enum aggregate の tag と payload leaf は ADR-0021 の既存 layout をそのまま利用し、新しい runtime representation は作らない。

Lowerer は各 enum variant tag または literal と temporary scrutinee を既存の equality operation で比較し、conditional branch と jump からなる通常 CFG を生成する。一致する variant が決まった後、arm body の前に必要な payload leaf を immutable binding storage へ copy する。

値を生成する match は synthetic result storage と merge block を持つ。各 reachable value arm は結果を既存の scalar copy または aggregate shallow-copy で result storage に格納して merge へ進む。`Never` arm は既存 terminator を保ち、result copy や不要な merge jump を生成しない。

Concrete generic enum は ADR-0022 の Binder による substitution と monomorphization を完了してから lower する。generic definition、type parameter、variant name、coverage 情報を UASM backend へ渡さない。

UasmAssembler は `match`、pattern、exhaustiveness、variant name resolution を認識しない。Assembler が受け取るのは、解決済み storage、copy、comparison、branch、jump、label、extern からなる既存 IR だけである。Match 専用 opcode や runtime helper は追加しない。

## Alternatives

1. Enum に `is_some?` や `unwrap` のような個別 method だけを追加し、general match を導入しない。用途ごとに method が増え、利用側の分岐を網羅的・型安全に記述できないため採用しない。
2. `switch` statement として導入する。Sobakasu の expression-oriented control flow と合わず、値を作る分岐に別の代入規則が必要になるため採用しない。
3. Full Rust-style pattern matching を最初から導入する。nested、guard、or、range を同時に扱う coverage algorithm と型規則が大きすぎ、Udon-first の第一段階として不要なため採用しない。
4. Runtime pattern matching helper を Udon 側に生成する。既存の静的 tag layout を再び runtime object として解釈し、型情報と backend 責務を増やすため採用しない。
5. 限定された pattern language を持つ expression-oriented `match` を Binder で解決し、Lowerer で通常 CFG へ変換する。既存の enum storage、型検査、control-flow lowering を再利用できるため採用する。

## Rationale

採用案は Sobakasu の Udon-first 設計に合う。Source では enum の構造を直接かつ網羅的に扱える一方、Udon へは既存の tag comparison、branch、copy だけを出力できる。Compiler 内でも Parser は pattern structure を保持し、Binder は名前解決、型検査、coverage、arm-local binding を担当し、Lowerer は解決済みの意味を CFG と storage operation に変換し、UasmAssembler は既存 IR を出力するという責務分離を維持できる。

ADR-0021 の flattening と shallow-copy を再利用することで scalar payload と aggregate payload に同じ value semantics を適用できる。ADR-0022 の concrete type substitution を先に行うことで `Option<T>` の payload binding も backend に generic 情報を漏らさず正確に型付けできる。ADR-0012 の result storage、merge、`Never` の考え方を再利用することで、`match` を他の expression と自然に組み合わせられる。

## Consequences

### Positive

* Payload enum を source level で型安全に分解できる。
* Enum と `bool` の分岐漏れを compile time に検出できる。
* 重複または wildcard 後の unreachable arm を早期に検出できる。
* `match` を local initializer、戻り値、nested expression、block arm として利用できる。
* `Option<T>` を含む concrete generic enum で正確な payload type を得られる。
* Scrutinee の exactly-once evaluation と既存の aggregate shallow-copy semantics を維持できる。
* UASM に新しい runtime representation、helper、opcode を要求しない。
* Parser、Binder、IR Lowerer、UasmAssembler の既存の責務分離を維持できる。

### Negative

* Binder に enum / boolean / literal の coverage と arm reachability の解析が増える。
* Pattern 専用 syntax、bound nodes、diagnostics、parser recovery が増える。
* Enum payload binding のため、Lowerer は既存 flattened layout から正しい leaf path を選ぶ必要がある。
* Integer、character、string match は version 1 では wildcard が必須である。
* Nested pattern、guard、or pattern、range pattern 等を将来追加する場合、現在の単純な coverage set をより一般的な usefulness / coverage algorithm へ拡張する必要がある。
* Pattern language は意図的に限定され、通常 struct destructuring、`if let`、`while let`、float / `null` pattern、bare variant shorthand は使用できない。
