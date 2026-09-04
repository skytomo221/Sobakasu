# ADR-0044: Declarative Operators and First-Operand Hosting

## Status

Accepted

## Context

ADR-0016 は `impl` による operator overload と declarative extern expression を導入したが、primitive operator は compiler builtin signature を先に解決し、その signature の再定義を禁止していた。このため、通常の `impl` operator と primitive builtin operator が別の availability / resolution 経路を持っていた。

Standard Library Generator も CLR/Udon の `op_*` を named function として表現できないものとして一律に除外していた。その結果、Udon が公開する operator の多くが生成されず、source-level operator の availability を Standard Library の宣言として管理できなかった。

さらに CLR では operator の physical member は、少なくとも一方の operand 型に宣言される。例えば `UnityEngine.Vector3.op_Multiply(System.Single, UnityEngine.Vector3)` の physical declaring type は `UnityEngine.Vector3` だが、Sobakasu の左 operand dispatch で必要な host は `f32` である。physical owner と source-level host を同一視すると、この operator を `f32` の `impl` から解決できない。

ADR-0041 は primitive builtin の canonical source identity と CLR ABI identity を一致させる external binding を定めた。この identity は維持しつつ、operator availability だけを declaration-driven にする必要がある。

## Decision

### Source-level operator availability

通常の source expression における unary / binary operator availability は、左 operand 型の `impl` に登録された operator overload のみによって決定する。Standard Library の operator とユーザー定義 operator は同じ method group と overload resolution を使用する。primitive 型であることを理由に compiler builtin operator table を先に適用したり、fallback として利用したりしない。

Primitive は型 identity、ABI mapping、literal typing、およびその他の language builtin として引き続き存在する。ただし、それ自体は source-level operator availability を付与しない。したがって Standard Library を利用しない source の `i32 + i32` は、対応する `impl i32` operator declaration がなければ利用できない。

ADR-0016 の「compiler builtin operator signature は再定義できない」という決定と `SBK2080` は、この ADR により supersede する。primitive `impl` には通常の operator overload を登録できる。

### Declarative extern operator

新しい keyword や文法は追加せず、ADR-0016 の operator overload syntax と declarative extern binding を組み合わせる。

```sobakasu
pub fn +(rhs: Self) -> Self
  = extern self + rhs

pub fn *(rhs: UnityEngine.Vector3) -> UnityEngine.Vector3
  = extern self * rhs

pub fn @- -> Self
  = extern -self

pub fn @~ -> Self
  = extern ~self
```

`@+` と `@!` も同じ declaration / overload resolution 経路を使用する。CLR の `op_*` 名から Sobakasu token への対応は Generator が明示的な mapping として保持し、`fn op_*` は生成しない。binary operator の第1 CLR parameter と unary operator の唯一の CLR parameter は暗黙の `self` となり、declaration の明示 parameter から除外する。

### Physical declaring type と generated host

CLR/Udon member の discovery、configuration identity、report identity、および選択済み Udon extern signature は physical declaring type を維持する。一方、生成モデル上の operator host は第1 CLR operand の型とする。

例えば physical member が `UnityEngine.Vector3.op_Multiply(System.Single, UnityEngine.Vector3)` なら、生成先は canonical `f32` の `impl` である。逆方向の `UnityEngine.Vector3.op_Multiply(UnityEngine.Vector3, System.Single)` は `Vector3` の `impl` に生成する。

第1 operand を host とするのは、Sobakasu の operator resolution が左 operand 型の method group を起点とするためである。ExternCatalog は physical declaring type の通常 member groupとは別に、第1 operand 型から physical operator へ到達できる index を持つ。`MethodInfo.DeclaringType` や extern signature は書き換えない。

### Unary operator synthesis

Source-level unary `-` を `0 - value` に、`~` を `value ^ all_bits_set` に展開する処理を廃止する。`-value` と `~value` はそれぞれ `fn @-` と `fn @~` を解決し、operand を一度だけ評価して選択済み operator invocation を lower する。

### Short circuit と assignment

`&&` と `||` は evaluation order と条件付き評価を定義する language semantics であり、extern operator declaration へ移さない。既存の short-circuit CFG lowering を維持する。代入 `=` も overloadable operator ではない。

Compound assignment は基になる operator overload を左 operand 型の `impl` から解決し、その結果を assignment する。local、state、array element、aggregate field のすべてで同じ選択済み operator function を使用する。array receiver と index などの assignment location は一度だけ評価する。

### Compiler-internal ABI operations

Array repeat の index 比較・加算、enum / pattern tag 比較など compiler 自身が合成する演算は、source-level operator resolution と分離した resolved ABI operation として扱う。これらは Standard Library の import や visibility に依存しない。

この ABI 経路は source expression の availability 判定には使用しない。Binder が source declaration または compiler intrinsic として operator を選択し、IR は選択済み invocation / extern signature を保持する。UASM backend は型解決や operator resolution を行わない。

### ADR-0041 との関係

ADR-0041 の canonical primitive type identity と canonical CLR external binding は維持する。`i32` と `System.Int32` などの対応、既存 builtin `TypeSymbol` の再利用、alias binding の禁止は変更しない。本 ADR が分離するのは primitive identity と operator availability であり、後者だけを `impl` declaration に一本化する。

### Non-goals

`op_Implicit` と `op_Explicit` は conversion 設計として別の ADR で扱う。`op_Increment` と `op_Decrement` も現在の Sobakasu operator overload surface へ追加しない。Generator はこれらを対応済みとせず、個別の unsupported reason を report する。

## Alternatives

### Primitive operator の builtin table を fallback として残す

Standard Library declaration がなくても同じ operator が利用可能になり、availability が二重化するため採用しない。

### CLR declaring type を operator host にする

左 operand 型と declaring type が異なる operator を通常の左 operand dispatch で発見できないため採用しない。

### `operator` keyword または専用 extern 構文を追加する

既存の operator declaration と extern expression で必要な情報を表現でき、別構文は parser と language surface を不要に増やすため採用しない。

### UASM backend で `op_*` を探索する

Binder で確定すべき型解決と overload resolution を backend へ移し、frontend / backend の責務分離を破るため採用しない。

## Rationale

Operator availability を declaration に統一すると、Standard Library generation、ユーザー定義 overload、visibility、duplicate detection、および overload resolution を同じモデルで扱える。Physical identity と generated host を分離すれば CLR/Udon ABI を改変せずに Sobakasu の左 operand dispatch と整合する。Compiler-internal ABI operation を別経路に限定することで、source semantics を declaration-driven にしながら compiler が合成する制御処理の自己完結性を保てる。

## Consequences

### Positive

* Udon が公開する対応可能な CLR operator を declarative Standard Library binding として生成できる。
* Primitive と非 primitive の source-level operator resolution が同じになる。
* CLR declaring type と第1 operand 型が異なる operator も正しい `impl` から利用できる。
* Unary `-` / `~` と compound assignment が通常の resolved operator invocation を共有する。
* Physical CLR identity と最終 Udon extern signature、および frontend / backend の責務分離を維持できる。

### Negative

* Primitive arithmetic を利用する source は、対応する Standard Library または user `impl` declaration を必要とする。
* Generator は physical member と generated host の両方を追跡する必要がある。
* Operator token mapping と対象外 operator の unsupported reason を明示的に保守する必要がある。
* 既存 Standard Library は新しい Generator で再生成しなければ operator declaration を含まない。
