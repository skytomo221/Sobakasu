# ADR-0041: Canonical Primitive External Type Bindings

## Status

Accepted

## Context

ADR-0016 は external type binding が新しい Sobakasu type を宣言することを前提とし、builtin type の external binding を一律に禁止した。一方、Standard Library Generator から builtin primitive の Udon API を生成するには、primitive の既存名と型 identity を保ったまま、その canonical CLR ABI type に対する `impl = extern` を宣言する必要がある。

新しい wrapper `TypeSymbol` を作ると、例えば source 上の `i64` と `TypeSymbol.I64` が別 identity になり、組み込み演算、変換、格納、overload resolution の既存規則と食い違う。任意の別名や異なる ABI type への binding を許可すると、external call の ABI と Sobakasu の型意味論も一致しなくなる。

## Decision

Builtin type は原則として external type binding できない。ただし、compiler が canonical runtime identity を持つ次の primitive builtin は、同じ行に示す canonical CLR type への binding に限り許可する。

| Sobakasu | CLR |
| --- | --- |
| `bool` | `System.Boolean` |
| `char` | `System.Char` |
| `i8` | `System.SByte` |
| `u8` | `System.Byte` |
| `i16` | `System.Int16` |
| `u16` | `System.UInt16` |
| `i32` | `System.Int32` |
| `u32` | `System.UInt32` |
| `i64` | `System.Int64` |
| `u64` | `System.UInt64` |
| `f32` | `System.Single` |
| `f64` | `System.Double` |
| `string` | `System.String` |

例えば `pub impl i64 = extern System.Int64` は既存の `TypeSymbol.I64` を再利用し、`RuntimeQualifiedName == "System.Int64"` を維持する。external binding の syntax mapping と runtime type binding にも同じ symbol を登録し、impl 内の method はその既存 builtin symbol に追加する。新しい `TypeSymbol` は作らない。

Primitive external impl に追加された static method は `i64.method(...)` のように呼び出せるよう、builtin 型名も通常の型と同じく expression 上の明示的な type receiver として解決する。local、parameter、state、constant の既存 lexical binding は型名より先に解決する。

`pub impl Integer = extern System.Int64` のような alias binding は許可しない。alias ごとに別の source identity が生じ、builtin の canonical name と Standard Library API の対応が曖昧になるためである。`pub impl i64 = extern System.String` のような ABI mismatch も、格納形式、extern signature、演算および変換の前提を破るため拒否する。

`object = System.Object` はこの決定の対象外とし、従来どおり拒否する。`object` の Udon value model と API surface を primitive と同じ規則で公開するかは別の設計判断とする。

上記 13 primitive 名を known language item に追加する。Standard Library Generator は既存の version 3 `lang` configuration で CLR type と item を対応付け、builtin symbol の `Name` を wrapper 名に使用し、value type の通常規則より優先して `Impl` placement を選ぶ。生成形は次のとおりとする。

```sobakasu
lang "i64"
pub impl i64 = extern System.Int64 {
  // generated Udon API bindings
}
```

生成された primitive module は impl と language item を収集するため親 module から `mod` で読み込むが、新しい型宣言を含まないため `pub use module.primitive` は生成しない。通常 external type、struct、enum、top-level API の既存 re-export は維持する。

Parser、IR、Lowerer、UASM backend に primitive 専用処理は追加しない。Parser は既存構文を保持し、Binder が canonical identity と ABI の一致を検証し、以降の段階は既存の resolved type と external call を処理する。

この ADR は、ADR-0016 の「builtin type を external binding することは常にエラー」という決定を、上記 canonical primitive exception に限って supersede する。ADR-0016 のその他の external binding、method、extern expression、operator、および pipeline responsibility の決定は維持する。ADR-0039 の version 3 language item schema と binding phase はそのまま再利用する。

## Alternatives

### Primitive wrapper ごとに新しい `TypeSymbol` を作る

組み込み演算、変換、overload resolution が参照する builtin identity と分離するため採用しない。

### 任意の builtin alias を CLR primitive に bind できるようにする

同じ ABI に複数の source identity が生じ、canonical Standard Library API と名前解決が不安定になるため採用しない。

### Generator だけで primitive を特殊な構文として描画する

compiler が生成結果を意味的に受理できず、Renderer に型 policy が漏れるため採用しない。

## Rationale

既存 builtin symbol の `TypeKind`、`Name`、`RuntimeQualifiedName` を正本にすれば、CLR 対応表を compiler と Generator に重複させず、source identity と runtime ABI identity を同時に維持できる。external declaration syntax mapping を正として method target を取得することで、通常 external type と primitive exception が同じ declaration pipeline を通る。

## Consequences

### Positive

* Standard Library Generator が primitive の Udon API を通常の external impl として生成できる。
* primitive の組み込み identity、演算、変換、格納表現を維持できる。
* version 3 configuration と既存 Renderer を変更せず利用できる。
* Parser、IR、Lowerer、UASM backend の責務は増えない。

### Negative

* builtin external binding には canonical pair の一致検査が必要になる。
* primitive runtime type は、通常 external wrapper のように任意名へ rename できない。
* `object` の API generation は引き続き対象外である。
