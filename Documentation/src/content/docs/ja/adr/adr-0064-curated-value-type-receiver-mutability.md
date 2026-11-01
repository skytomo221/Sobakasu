---
title: 'ADR-0064: Curated Value-Type Receiver Mutability'
---

## Status

Accepted

## Context

[ADR-0061](../adr-0061-mutable-self-receiver-semantics/) と [ADR-0062](../adr-0062-standard-library-receiver-and-maybe-projection-policy/) は値型 receiver の変更可能性を保守的に扱う。[ADR-0063](../adr-0063-structured-physical-member-selectors/) により、config v5 は overload と accessor を区別する structured selector を持つ。

Unity 2022.3.22f1 / VRChat SDK Base・Worlds 3.10.4 の API について、Worker A〜E の調査結果を統合する。調査の結論そのものを API contract とみなさず、readonly/high 候補を独立に反証レビューする。Task 0 のメモが指す固定コミット `a86442bb8565e59e22ec6b8550a834c050bc8057` の対象 JSON 5件を入力とし、2,256件・重複0件を現在の physical model と照合した。全 API への一般化は行わない。

## Decision

### Receiver の意味と既定

`mutable self` は extern instance member の呼び出しによって **value-type receiver 自身の値が変更される可能性**を表す。一般的な副作用の有無を表さない。依頼でいう `mut self` に対応する現行の Sobakasu 構文は `mutable self` であり、構文は変更しない。

value-type instance member は、receiver の非変更性を積極的に確認できない限り `mutable self` とする。参照先の ParticleSystem や配列要素を変更することだけでは、receiver の値の変更を意味しない。反対に、getter であっても receiver 内の遅延初期化・キャッシュ更新があれば readonly ではない。

### Curated metadata

`receivers.readonly` は CLR/Udon metadata だけでは十分に表現できない API の receiver mutability semantics を補完する **curated metadata** とする。場当たり的な workaround や member 名からの推測ではない。対象バージョン、physical selector、判定理由、検証可能な根拠を調査記録に残す。

証拠は、対象バージョンの公式実装、対象バージョンの公式 API contract、CLR metadata、VRChat / Udon 公式情報、その他一次情報の順に優先する。対象環境に実際に配布された managed assembly の IL も公式実装の証拠として使用できる。assembly の SHA-256 と method token を記録し、別バージョンの実装や参照用 stub と混同しない。

名前、`Get*` / `Contains*`、getter という分類だけから readonly と推測しない。native 本体に receiver のアドレスを渡し、その本体または非変更 contract を確認できない場合は `uncertain` とする。一方、値コピーを渡すことを確認できれば、元の receiver とコピーを区別して判断する。

### 採用条件と識別

今回追加できる member は Worker が `readonly` / `confidence: high` と分類し、独立レビューが `accept` としたものに限る。非変更性を積極的に確認した場合だけ accept し、反証を見つけられなかっただけでは accept しない。`mutable`、`uncertain`、`reject` は追加しない。既存エントリも誤りが判明した場合は修正する。

入力が矛盾する場合は多数決にしない。一次情報で再検証し、解消できなければ `uncertain` に残す。入力で accessor / overload を一意に特定できない場合も、配列位置や分類から identity を推測して採用しない。

config は ADR-0063 の canonical identity、すなわち physical host の `member`、`kind`、parameter type / passing、generic arity を使う。宣言元型を host の代わりにしない。readonly rule を複数 overload や派生 host へ暗黙に拡張しない。並び順は member、kind、構造化 parameters、generic arity による決定的な順とする。

Generator の既存の conservative default と exact selector の仕組みを使用し、推測処理を追加しない。config 読み込み、全 rule の単一実在 match、stale rule 不在、選択した member の `self` 生成、未選択の mutable / unknown member の `mutable self` 維持を検証する。config の変更は生成済み standard library にも反映する。

### 今回の統合

2,256件のうち readonly/high 807件をレビューし、563件を accept、244件を uncertain、0件を reject とした。accept の563件を追加し、今回の対象一覧に含まれない既存2件も対象 DLL の実装を再確認して保持する。config は合計565件となる。readonly/medium 339件は今回の採用条件を満たさないため追加しない。統合後の調査対象内の分類は readonly 563件、mutable 270件、uncertain 1,423件である。

個別の structured selector、根拠の IL、入力との対応、および追加・保持した全 member は [調査記録](https://github.com/skytomo221/Sobakasu/tree/main/Documentation/receiver-mutability/2022.3.22f1-sdk-3.10.4) に保存する。リポジトリ内の配置は `Documentation/receiver-mutability/2022.3.22f1-sdk-3.10.4/` である。

## Alternatives

### A. すべて mutable

安全だが、非変更性が分かる API にも不必要な `mutable self` が大量に生成される。既定として維持し、根拠のある例外を許可する。

### B. 名前・getter 等から自動推測

receiver 内のキャッシュ更新や native 実装を見落とし得る。精度を保証できず、false positive が危険なので採用しない。

### C. CLR readonly metadata のみ使用

確実な情報として利用価値はあるが、対象 Unity API では annotation が十分とは限らない。annotation の不在を mutable の証明にも readonly の証明にも使わない。それだけに限定せず、公式実装・contract に基づく個別判断を記録する。

### D. API semantics を調査し curated config として管理

採用する。保守的な既定を維持しながら、根拠のある member だけを正確に指定できる。

## Rationale

mutable member を readonly と誤認する false positive は、変更不可の値から変更可能な extern を呼べるようにし、言語の意味論を壊す可能性がある。readonly member を mutable のままにする false negative は、主に利用上の制約を増やす。このリスクの非対称性から false positive を避け、不明な API は `mutable self` のまま残す。

physical catalog と generation policy の責務を維持する。実装・型解決・semantic analysis を renderer や UASM backend に移さず、source-language semantics と serialized schema も変更しない。

## Consequences

### Positive

- 根拠のある API は変更不可の binding から利用できる。
- 独立レビューと exact selector により、安全性と追跡可能性を保つ。
- 外部オブジェクトの副作用と receiver 自身の変更を混同しない。

### Negative

- native 実装や対象バージョンの保証が不明な API は、実際には readonly でも mutable binding を要求する。
- Unity / SDK の更新時に、curated metadata とその根拠の再確認が必要になる。
- config の実在性を確認するテストだけでは、外部 API の非変更性を証明できない。実装の調査記録も維持する。
