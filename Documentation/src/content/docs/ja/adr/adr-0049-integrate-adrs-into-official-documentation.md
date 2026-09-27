---
title: 'ADR-0049: ADRを公式Documentationへ統合する'
---

# ADR-0049: ADRを公式Documentationへ統合する

## Status

Proposed

## Context

ADR-0048 は、ADR の正本を `docs/adr/` に残し、Astro/Starlight への移行対象外とした。その後、設計判断を公式 Documentation 上で読みやすく参照し、利用者と開発者が同じ入口から履歴を確認できるようにする必要が生じた。

この変更は ADR-0048 全体を置き換えるものではない。ADR の配置、公開、言語管理に関する決定だけを更新する。

## Decision

ADR の正本を `Documentation/src/content/docs/ja/adr/` に移行する。旧 `docs/adr/` には複製を残さない。

ADR は日本語のみで管理する。英語・韓国語・簡体字中国語の ADR は作成しない。

Starlight から ADR 一覧および各 ADR を閲覧できるようにし、ADR 一覧を主入口とする。他 locale に ADR ディレクトリを作成せず、日本語の ADR へのリンクだけを使用する。

既存 ADR の番号、ASCII の Markdown ファイル名、Status、および履歴を維持する。過去の設計判断を現在の仕様に合わせて改変しない。英語で記述された既存 ADR 本文は日本語へ翻訳するが、意味、決定内容、トレードオフを変更しない。

テンプレートは `Documentation/adr-template.md` に置く。このファイルは通常の ADR ではなく、Starlight の公開一覧には表示しない。

## Alternatives

### ADR を `docs/adr/` に残して GitHub へリンクする

既存の配置を維持できるが、公式 Documentation と正本が分かれ、Starlight のナビゲーションや検索から自然に辿れない。そのため採用しない。

### 各 locale に ADR を翻訳して配置する

各言語で閲覧できるが、履歴文書の同期と翻訳差分の管理負担が増え、正本が曖昧になる。そのため採用しない。

### ADR ごとに日本語ファイル名へ変更する

日本語利用者には分かりやすいが、既存リンクと履歴を不必要に変更する。そのため採用しない。

## Rationale

ADR を Documentation の日本語 locale に置くことで、公式サイトの導線、検索、ナビゲーションを利用しながら正本を一か所に保てる。日本語のみを正本にすることで、履歴文書の翻訳同期による不整合を避けられる。

既存の番号と ASCII ファイル名を維持し、翻訳を内容変更と分離することで、過去の判断を追跡可能な記録として保存できる。

## Consequences

### Positive

- ADR を Starlight 上で一覧・個別ページとして参照できる。
- ADR の正本が `Documentation/` に一本化される。
- 既存の番号、ファイル名、Status、履歴を維持できる。
- 多言語の複製を作らず、正本と翻訳管理の責務を明確にできる。

### Negative

- ADR 作成時の確認先とテンプレートの固定パスが変わる。
- 英語で書かれた既存 ADR を日本語へ翻訳・レビューする必要がある。
- ADR は日本語のみとなるため、他 locale の読者は日本語ページを参照する必要がある。
