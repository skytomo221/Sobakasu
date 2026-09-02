# ADR-0042: Generic Udon Extern Methods and CLR Type Patterns

## Status

Accepted

## Context

Udon は `UnityEngineGameObject.__GetComponent__T` のような generic extern node を公開している。これらは CLR の generic method と対応するが、Udon ABI は CLR source parameter にはない `System.Type` operand を要求し、extern signature 自体は concrete type 名へ specialize されない。従来の reflection catalog と standard library generator は generic method および open/constructed generic CLR type を一律に除外していたため、`GetComponent<T>`、`GetComponents<T>`、`List<T>` を受け取る overload などを Sobakasu から利用できなかった。

ADR-0022 の generic type/monomorphization は user-defined aggregate を対象とし、generic extern method、CLR generic constraint、generic CLR-Udon binding を対象外としている。一方、ADR-0016 と ADR-0033 は extern の semantic resolution を Binder に置き、logical signature と physical ABI、return/ref/out projection を分離する。ADR-0040 は CLR native value と Sobakasu aggregate storage の区別を維持する。

## Decision

1. Udon exposed な CLR generic method を extern catalog と declarative extern binding でサポートする。これは runtime generics や一般的な generic user function の導入ではない。
2. generic parameter、array、generic definition、constructed generic type を組み合わせた CLR type pattern を extern boundary で再帰的に表現する。constructed extern type は generic definition、ordered type arguments、CLR runtime identity、reference/value classification、および external-binding identity を保持し、既存の `TypeSymbol.Construct`、`TypeSymbol.Substitute`、`GenericSubstitution` を利用する。
3. explicit type application を Parser が既存の expression generic application syntax として保持し、Binder が method generic arity、constraint、substitution、overload applicability を解決する。IR に到達する selected symbol の logical/ABI types は concrete でなければならない。
4. Udon extern signature は `T`、`TArray`、`ListT` などを含む公開 node 名のまま保持する。`__GetComponent__UnityEngineTransform` のような concrete signature を合成しない。
5. CLR signature に現れない Udon generic type operand は `GenericTypeArgument` という明示的な physical ABI parameter とする。Lowerer は selected extern symbol の concrete type argumentから `System.Type` constant を生成し、receiver、generic operands、CLR parameters、result storage の順序を ABI metadata に従って構築する。UASM backend は通常の slot、`PUSH`、`EXTERN` だけを扱う。
6. generic parameter constraint は `GenericParameterAttributes` と `GetGenericParameterConstraints()` から catalog metadata に保存する。Binder は CLR runtime identity と CLR assignability/constructed method validation を source of truth として `class`、`struct`、`new()`、base class、interface constraint を検証する。
7. constraint のために trait、interface declaration、`where` syntax、Sobakasu 固有 constraint language は導入しない。
8. open generic metadata、generic inference、constraint validation、CLR `MethodInfo` 再探索を IR/UASM backend に持ち込まない。
9. generic CLR type が extern signature で representable であることと、通常の Udon heap state/local/storage に格納可能であることを分離する。既存 storage/ABI compatibility checks を緩和しない。
10. standard library generator の discovery、formatter、renderer、report は Udon exposed generic signature を扱う。formatter は generic parameter、array、任意 arity の nested constructed generic type を再帰的に処理し、renderer は function generic parameter list と extern invocation type argumentsを出力する。coverage は引き続き physical `extern_signature` 単位とする。

Explicit type arguments を第一要件とし、引数からの一般的な generic method inference は必須としない。declarative extern adapter に必要な method-specific generic parameters は許可するが、通常本体を持つ top-level generic function 全般は今回の機能範囲に含めない。

## Alternatives

### Concrete type ごとに extern signature を生成する

Udon node catalog に存在しない signature を作り、generic node の ABI と一致しないため却下した。

### `List<T>` と `GetComponent<T>` だけを特別扱いする

配列や nested/複数引数 generic pattern に拡張できず、reflection metadata と extern formatter の情報を重複させるため却下した。

### UASM backend で generic method を解決する

Binder/IR/backend の責務分離に反し、constraint と overload resolution が backend に漏れるため却下した。

### Generic CLR type を通常 storage として一律に許可する

extern node の operand representability は Udon heap storage compatibility を保証しないため却下した。

## Rationale

open pattern と concrete selected symbol を分けることで、reflection と generator は CLR metadata を忠実に保持しつつ、既存 overload resolver と recursive substitution を再利用できる。hidden operand を ABI metadata に置くことで、Lowerer は決定済みの呼び出しを機械的に展開でき、UASM backend は generic semantics を持たずに済む。CLR constraint metadata を source of truth とすることで Sobakasu 独自の不完全な型階層を追加せず、Unity/SDK の実際の制約と一致させられる。

## Consequences

### Positive

* Udon exposed generic API が standard library に生成され、physical signature coverage が向上する。
* `T`、`[T]`、`List<T>`、nested generic type が同じ substitution 機構で扱われる。
* extern logical ABI、physical ABI、backend の責務境界が維持される。
* CLR constraint 違反は lowering 前に診断される。

### Negative

* extern catalog symbol と callable application に generic metadata と constructed-symbol cache が必要になる。
* `System.Type` constant の安定した serialization/heap patch support が必要になる。
* explicit type argument のない一般的 inference と、任意の generic CLR storage は引き続き unsupported である。
