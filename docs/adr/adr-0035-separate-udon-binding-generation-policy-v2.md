# ADR-0035: Separate Udon Binding Generation Policy v2

## Status

Accepted

## Context

ADR-0034 の version 1 configuration は、既定値、名前変換、配置、nullable projection、除外を同じ `defaults` / `namespaces` / `types` / `members` に保持していた。この構造では、CLR API の source identity と生成後の Sobakasu path が混在し、同じ意図を複数フィールドで指定する必要があった。

また、CLR static class の配置を設定で `impl` または `top_level` に切り替えると、CLR に由来する構造を Sobakasu の型 API として公開できてしまう。生成器が担うのは物理 API の機械的な wrapper であり、複数の CLR API を意味論的に統合する高級 API は手書き標準ライブラリが担うべきである。

## Decision

generation configuration version 2 は、次の4責務だけを持つ。

* `renames`: CLR namespace/type/member identity から生成名への変換
* `prelude`: rename 後の生成済み Sobakasu path の再エクスポート
* `maybe`: canonical CLR member ID に対する return/out projection
* `excludes`: CLR namespace/type/member identity に対する生成除外

`defaults` と旧 top-level `namespaces` / `types` / `members` は削除し、version 1 compatibility layer は持たない。reference return/out の既定値は raw、predicate naming は固定の自動命名規則とする。

Namespace rename は CLR namespace prefix に対する longest-prefix match とし、残りの segment を既存の Sobakasu identifier normalization で保存する。`to: null` は一致 prefix の削除であり、`to` 省略とは区別して検証する。Type/member/Maybe/exclude は序数比較による完全一致とする。

Member 設定の identity は discovery 済み Reflection metadata から canonical CLR member ID を生成して得る。JSON の文字列から Reflection member を逆引きする parser は作らない。Method/constructor は ordered parameter type を含み、property/field は CLR member 単位の ID を使う。

CLR static class は常に public Sobakasu child module として生成し、Sobakasu の型や instance API を生成しない。`Mathf` や `Debug` のように public constructor 以外の declared API が static member だけである Unity の API container も同じ規則で扱う。通常 class/struct/enum は従来どおり external `impl` として生成する。Type rename は通常型の leaf type 名、static class/module API の module 名を変更する。

Prelude は生成済み path を解決した後に `prelude.sobakasu` へ `pub use` を生成する。Declaration を複製しない。Namespace wildcard は指定 module の direct public symbols だけを対象とし、再帰しない。Stale target と public symbol collision は generation error とする。

処理順序は、discovery、CLR identity による exclusion、rename、Maybe projection、生成 path と collision の確定、binding source 生成、prelude 解決、source validation/report の順を基本とする。Binder、IR、UASM backend へ設定解決を移さない。

既存 report schema と Udon API coverage の分母・covered/unsupported の意味は維持し、rule identity と count だけを version 2 schema に合わせる。

## Consequences

### Positive

* source CLR identity と generated Sobakasu identity の境界が明確になる。
* nullable projection と exclusion が rename や配置から独立する。
* static class は一貫して module API となり、CLR の型概念を公開 API に持ち込まない。
* Prelude は既存 declaration の単一 identity を保ったまま構成できる。
* stale/duplicate/collision configuration を生成前後の明確な段階で診断できる。

### Negative

* version 1 configuration は breaking migration となる。
* generated module/type/member path の変更は prelude 設定も同時に更新する必要がある。
* installed SDK の API 変更により exact rule が stale になると生成が失敗する。

## Supersedes

ADR-0034 を置き換える。ADR-0017/0018 の標準ライブラリ module、`pub use`、Prelude、明示的 `extern` 境界は維持する。ここでいう static class module は生成された通常の Sobakasu source module であり、compiler が CLR namespace/static class を暗黙に module 解決するものではない。
