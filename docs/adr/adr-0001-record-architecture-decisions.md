# ADR-0001: Sobakasu用カスタム軽量ADRテンプレートの採用

## Status

Accepted

## Context

Sobakasuは独自言語およびUdonAssembly向けコンパイラ基盤を持つプロジェクトである。
設計判断は以下の特徴を持つ：

* コンパイラパイプライン（Lexer / Parser / Binder / IR / UASM など）に関する構造的判断が多い
* IR層や中間表現など、代替案比較が重要
* 将来的にOSS化・共同開発の可能性がある
* 長期的に設計思想を残す必要がある

既存のADRテンプレートには以下の選択肢がある：

* Joel Parker Henderson系（非常に軽量）
* MADR（代替案比較が明確だがやや重い）
* adr-tools系（CLI管理向き）

Sobakasuでは、

* 過度に重いフォーマットは避けたい
* 代替案比較は必須
* 長期保守可能な簡潔さを維持したい

という要件がある。

## Decision

以下の構造を持つ「Sobakasu用カスタム軽量ADRテンプレート」を採用する：

```md
# ADR-XXXX: Title

## Status
Proposed | Accepted | Deprecated | Superseded

## Context

## Decision

## Alternatives

## Rationale

## Consequences
```

この形式は、

* Joel Parker Henderson系の軽量性
* MADRの代替案明示
* 企業実務向けADRの合理性

を折衷したものとする。

## Alternatives

1. Joel Parker Henderson原典テンプレート
   → 軽量だが代替案比較が弱い

2. MADRテンプレート
   → 強力だがやや冗長

3. adr-tools完全導入
   → CLI依存を増やす可能性

## Rationale

* コンパイラ設計では「なぜその層を挿入したのか」「なぜIRを分離したのか」などの比較が重要
* 将来的なSupersede管理を容易にするため、明示的なStatusを保持
* Markdownのみで完結し、ツール非依存

## Consequences

### Positive

* 設計判断が体系的に蓄積される
* IR・コード生成・Unity統合の設計根拠が明確になる
* 将来のリファクタリング時に参照可能

### Negative

* ADR作成の運用コストが発生
* 記録しない判断が発生する可能性
