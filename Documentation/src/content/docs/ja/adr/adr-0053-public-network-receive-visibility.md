# ADR-0053: Network Receive Endpoint の Public Visibility

## Status

Accepted

## Context

ADR-0024 は `receive` declaration を `fn` と異なる network event receiver として
導入し、`send` からのみ呼べるようにした。Sobakasu source file は一つの Udon
Program source を表すため、receive declaration は module の public API ではなく、
現在 compile している Program に属する。

Program が意図して公開する endpoint と実装詳細の endpoint を source level で
区別する必要がある。Udon runtime の到達可能性はこの区別には使えない。既存 network
ABI との互換性のため、private endpoint も Udon runtime 上は network-callable
entrypoint のままでなければならない。

## Decision

`receive` は private-by-default とし、`pub receive` で current Sobakasu Program の
public network endpoint を宣言する。

`NetworkReceiveSymbol.IsPublic` を immutable な semantic visibility の source of
truth とする。Binder が `ReceiveDeclarationSyntax.PubKeyword` から一度だけ設定し、
後続 stage は visibility のために syntax を再参照しない。

receive declaration は module API system の外に置く。`ModuleSymbol`、import、
canonical public path、re-export、`pub use` には追加しない。同一 Program 内の
`send` は private/public の両方を参照できる。

visibility は Udon runtime security boundary ではない。private/public の両方を Udon
entrypoint として export し、NetworkCalling metadata を生成する。visibility により
network ABI、aggregate flattening、metadata、rate は変更しない。

既存の `NetworkReceiveSymbol.Name` と `ExportName` の分離は維持する。現時点では
`ExportName == Name ==` source receive name である。send lowering、UASM export、
network metadata は同じ `ExportName` を使う。

`pub on` は引き続き無効とする。cross-program typed send は導入しない。これには
Program identity、runtime behaviour instance、public receive member table を結ぶ
first-class Program reference または Program API type の別設計が必要である。

## Alternatives

### receive を module public declaration として扱う

採用しない。receive は reusable library module ではなく entry Sobakasu Program に
属する。module path では scene 上の UdonBehaviour instance を特定できない。

### public receive だけを export する

採用しない。runtime behavior を変え、source visibility が security boundary である
かのような誤解を生む。

### raw UdonBehaviour と event string による cross-program send を追加する

採用しない。endpoint の存在、public visibility、引数 signature を compile-time に
検証できない。

## Rationale

semantic flag により、現在の local send と Udon runtime ABI を維持したまま、
documentation と将来の Program API modeling のための安定した source-level contract
を提供できる。runtime name を `ExportName` に集約することで、backend stage での
重複した name resolution を避けられる。

## Consequences

### Positive

- Program は `pub receive` により意図的な public network endpoint を宣言できる。
- private implementation endpoint は既存 runtime behavior を維持する。
- Compiler visibility は Unity / VRChat assembly から独立したままである。
- 将来の typed cross-program API の semantic boundary が明確になる。

### Negative

- `pub receive` だけでは別 Sobakasu Program から target Program を呼べない。
- source visibility は external Udon runtime caller による private endpoint の呼び出しを
  防止できない。
