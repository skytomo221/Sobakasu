---
title: 'ADR-0065: Curated API Output Nullability'
---

## Status

Accepted

## Context

[ADR-0062](../adr-0062-standard-library-receiver-and-maybe-projection-policy/) は参照型の戻り値と `out` を原則 `Maybe<T>` とする。CLR / Udon metadata は物理的な型を表すが、失敗時の null、引数や成功フラグとの相関、ネイティブリソースの状態まで十分に表さない。non-null の誤認は null を通常の値として公開する一方、安全な API を Maybe のまま残す誤りは主に利用上の手間を増やす。このリスクは非対称である。

[ADR-0064](../adr-0064-curated-value-type-receiver-mutability/) と同様に、確認した外部 API の性質を設定で補完する。ただし nullability は、receiver の値コピーの有無に加えて、API contract、正常終了と失敗、入力、外部実装に強く依存する。実装中に `ldnull` が見当たらないことだけでは保証にならない。

対象は Unity 2022.3.22f1 / VRChat SDK Base・Worlds 3.10.4。Task 0 の `semantics.json` に現行仕様を固定し、5,648候補を Worker A〜E が分類した。この ADR は Maybe の構文や意味、reference-like 判定、`ref` の扱い、serialized schema を変更しない。

## Decision

### 保守的な既定と採用条件

参照型出力の保守的な Maybe 既定を維持する。全正常終了経路について non-null を積極的に確認し、Worker の `non-null / high` と独立した反証レビューの `accept` を両方満たすものだけ nonMaybe 例外へ追加する。反例を発見できなかったことを証明としない。

名前、`Get*` / `Create*`、getter、配列という分類による推測は禁止する。成功フラグや入力条件による保証を単純な nonMaybe にしない。nullable / conditional / uncertain、および medium / low は Maybe のまま残す。既存設定も証拠とはみなさず、誤りが判明した場合は影響と破壊的変更を調べて修正する。安全でない保証を互換性のために保持しない。

戻り値の供給元を、receiver、新規割り当て、検査済みの値、field、callee、ネイティブ結果、外部実装に分けて調べる。入力や callback の結果が nullable でも、外側の返却値と同一とは限らない。外側の配列の保証は要素の保証を含まない。null を検出して例外になる経路は null を正常返却する経路と区別する。CLR null、Unity の overloaded-null、ネイティブリソースの生存性も区別する。

ネイティブ由来の参照結果については対象環境の contract または十分な一次証拠を必要とする。managed IL だけで境界を埋めない。ネイティブ呼び出しが値型結果や配列内容だけへ作用し、返却する managed 参照を置換できない場合は、その値の流れを記録する。

### Curated metadata と exact selector

config は workaround ではなく、外部 API contract を補完する curated metadata として管理する。assembly identity / SHA-256、method signature、metadata token、normal return position、条件、未解決の境界、physical member / output identity、および判定根拠を保存する。採用した curated metadata の判断を後から監査・再確認できる evidence を残し、全量 IL dump や一次調査の intermediate data の恒久保存は要求しない。Unity / SDK 更新時は、設定の match 確認に加えて evidence を再確認する。今回の実装証拠はインストールされた配布物に限定し、将来バージョンや別の client 実装への保証に一般化しない。

現行 config v5 の `nonMaybeReturns` は opaque な `physical.ClrSignature`、`nonMaybeOuts` は同じ signature と元の out parameter 名を使う。[ADR-0063](../adr-0063-structured-physical-member-selectors/) の structured receiver selector と混同しない。今回の追加は全 physical model 上で1件だけに一致するものに限定し、overload、accessor、passing mode、generic arity、physical host、out position を照合する。

継承 host や generic arity 等を安全に一意指定できない場合は追加しない。広い selector で代用せず、必要な schema 拡張は別設計課題とする。Maybe rule の既存 multi-match 許容を変更する schema 再設計は行わない。

`nonMaybeTypes` は、その型を出力するすべての return / out と該当する generic instantiation に作用する。将来の一致対象も含む型単位の保証が必要であり、rule 数の削減を理由に使用しない。今回は採用しない。

### 今回の統合と互換性

独立レビューは121件中112件を accept、9件を uncertain とした。reject / conditional は0件。うち98件を個別戻り値ルールとして採用する。accept でも8件は複数 physical target に一致し、6件は params による生成対象外なので採用しない。type-wide / out ルールは0件。既存の3種類の nonMaybe 配列はいずれも空であり、削除・保持する既存 rule はない。

生成済み API の98宣言で `Maybe<T>` が `T` に変わる。既存利用コードがその戻り値を Maybe として照合している場合は変更が必要であり、公開 API の型変更として扱う。言語の null safety の規則は変わらない。

監査・再確認用の記録は `Documentation/maybe-nullability/2022.3.22f1-sdk-3.10.4/` の次の6ファイルに保存する。

- `README.md`: 調査方法、範囲、集計、成果物と検証結果の説明。
- `semantics.json`: Task 0 で固定した分類基準と evidence policy。
- `source-manifest.json`: 対象環境、ソース metadata、証拠 assembly の identity / SHA-256。
- `accepted.json`: 最終的に config へ採用した98件の exact selector、member / output identity、Worker 判定、independent review、evidence を集約した監査用データ。
- `summary.json`: 抽出・一次分類・review・integration の集計と検証要約。
- `task-f-verification.json`: config validation、generation diff、テスト結果・失敗理由・未検証範囲。

採用98件の evidence は `accepted.json` を中心に保存し、`sourceManifestRef` から `source-manifest.json` の証拠 assembly へ対応付ける。signature / token と記録された正常終了位置・経路の根拠から、対象 method と判定を再確認できるようにする。5,648候補すべての raw data、Worker A〜E の全結果、candidate dump、全量 IL dump、その他の intermediate data は保存対象としない。再現可能性のために全中間生成物を Git に保存するのではなく、採用した curated metadata の判断を監査・再確認できる記録を維持する。

## Alternatives

### すべての参照型出力を常に Maybe にする

保守コストは小さいが、non-null を証明できる API にも取り出し処理を要求する。安全な既定として維持し、証拠のある例外だけ許可する。

### Naming heuristic

名前だけでは not-found、失敗、外部実装の null を判定できないため採用しない。

### CLR nullability metadata だけに依存する

有用な証拠だが、対象 Unity / SDK の情報不足や条件付き annotation、ネイティブ境界を解決できない。annotation の不在を non-null と解釈しない。

### Implementation analysis だけに依存する

固定配布物の直接 allocation 等には強い証拠となるが、外部 dispatch、ネイティブ実装、バージョン差を自動的には解決しない。contract と対象環境を併せて評価する。

### Optimistic non-null default

未知の API を安全な値として公開してしまうため採用しない。

## Rationale

false positive を避けるため、不明な境界は Maybe に残す。curated metadata は物理 catalog、生成 policy、renderer の責務を保ちつつ、検証できる API contract を利用側の型へ反映できる。独立レビューと設定の単一 match 検証は別々に必要であり、片方だけでは十分でない。

## Consequences

### Positive

- 証明できた戻り値は通常の値として扱え、不要な Maybe の取り出しを減らせる。
- exact selector と保存した証拠により、判定と生成宣言の対応を追跡できる。
- 言語構文や Generator architecture を変更せず保守的な既定を保てる。

### Negative

- 調査、独立レビュー、証拠更新に保守コストがかかる。
- Unity / SDK のバージョンに依存するため、更新時に契約と実装を再確認する必要がある。
- 条件付き nullability を現行の単純な例外設定や型で完全には表せない場合がある。
- 現行 selector で絞れない安全な API は Maybe のまま残る。
- 今回の Maybe 除去は公開 return type の変更となり、利用コードの修正が必要な場合がある。
