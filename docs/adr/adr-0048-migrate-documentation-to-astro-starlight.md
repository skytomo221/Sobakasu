# ADR-0048: Migration to a Multilingual Documentation Site with Astro/Starlight

## Status

Proposed

## Context

Sobakasu の利用者向けドキュメントは現在 `docs/` 以下にあり、Language documentation、Samples、Architecture Decision Records が同一ディレクトリツリーに置かれている。

一方、VPM repository listing の公開用ファイルは `Website/` にあり、GitHub Actions の `build-listing.yml` が `Website/` を VPM listing の生成・GitHub Pages 公開対象として使用している。

今後、Sobakasu では以下を満たす公式ドキュメントサイトが必要である。

- Guide、Language Reference、Standard Library Reference を体系的に公開できること
- 日本語、英語、韓国語、簡体字中国語を扱えること
- Markdown / MDX を中心にドキュメントを管理できること
- ドキュメント検索、サイドバー、ページ内目次、コード表示など、技術ドキュメントに必要なUIを提供できること
- 既存の VPM repository listing と共存できること
- 将来的に Standard Library Reference の自動生成を統合できること
- ADR の正本性や既存の開発ワークフローを不必要に変更しないこと

また、言語ごとにURL構造が異なる状態を避け、すべての公開ドキュメントで同じルーティング規則を使用したい。

## Decision

利用者向け公式ドキュメントサイトを Astro + Starlight で構築する。

Astro/Starlight プロジェクトはリポジトリルートの `Documentation/` に配置する。

```text
Documentation/
├── astro.config.mjs
├── package.json
├── tsconfig.json
├── public/
└── src/
    ├── assets/
    ├── components/
    ├── content.config.ts
    └── content/
        └── docs/
```

Starlight の標準構成に従い、公開ドキュメントは `Documentation/src/content/docs/` を正本とする。

### Locales

次の4言語を初期対応言語とする。

| Directory | Language | BCP 47  |
| --------- | -------- | ------- |
| `ja`      | 日本語   | `ja`    |
| `en`      | English  | `en`    |
| `ko`      | 한국어   | `ko`    |
| `zh-cn`   | 简体中文 | `zh-CN` |

日本語を default locale とする。

root locale は使用しない。

すべての言語に locale prefix を付与し、言語によってURL構造を変えない。

```text
/ja/...
/en/...
/ko/...
/zh-cn/...
```

Sobakasu が `/Sobakasu/` 以下で公開される場合は、実際のURLは次の形になる。

```text
/Sobakasu/ja/...
/Sobakasu/en/...
/Sobakasu/ko/...
/Sobakasu/zh-cn/...
```

サイトのルート `/Sobakasu/` は default locale である `/Sobakasu/ja/` への入口とする。

ブラウザの言語設定による強制的な自動振り分けは行わない。

### Documentation structure

利用者向けドキュメントは各localeで同じ論理構造を使用する。

```text
src/content/docs/
├── ja/
│   ├── index.md
│   ├── guide/
│   ├── language/
│   ├── reference/
│   │   └── standard-library/
│   └── samples/
├── en/
│   └── ...
├── ko/
│   └── ...
└── zh-cn/
    └── ...
```

各分類の責務は次のとおりとする。

- `guide/`
  - インストール、Getting Started、Unityとの統合、実際の使用方法など、目的達成型の説明
- `language/`
  - Sobakasu の構文、型、意味論、モジュールシステムなどの言語リファレンス
- `reference/standard-library/`
  - Standard Library および公開APIのリファレンス
- `samples/`
  - 実際に動作するコード例と、その解説

対応する翻訳ページは可能な限りlocale間で同じ相対パスを使用する。

### Standard Library Reference

Standard Library Reference は大量のAPI情報を人手で複製して管理することを前提としない。

将来的には Standard Library Generator またはコンパイラが保持する型・member metadata を正本として、Starlightで利用できるReferenceページを生成できる構造とする。

Guideなどの説明文と、自動生成可能なAPI Referenceは責務を分離する。

### Existing `docs/`

既存の `docs/` 全体をそのまま `Documentation/` にrenameしない。

利用者向けドキュメントのみを段階的に移行する。

例:

```text
docs/language/
    → Documentation/src/content/docs/ja/language/

docs/samples/
    → Documentation/src/content/docs/ja/samples/
```

画像などのassetは用途に応じて `Documentation/src/assets/` または `Documentation/public/` へ移行する。

移行後、利用者向けドキュメントの正本を `Documentation/` に一本化し、同一内容を `docs/` と二重管理しない。

### ADR

Architecture Decision Records はこの移行の対象外とする。

ADR の正本は引き続き次に置く。

```text
docs/adr/
```

既存のADR作成手順、`AGENTS.md`、開発者向け参照、およびADR間の相対的な関係を、Webサイト構築上の都合だけで変更しない。

ADRを公式サイトから参照する必要がある場合は、`docs/adr/` の正本への導線を提供する。

ADRの翻訳版は原則として作成しない。

### Root README

リポジトリルートの `README.md` は GitHub 上で Sobakasu の概要を理解するための入口として残す。

詳細な利用方法、言語仕様、チュートリアルなどについては `Documentation/` を正本とし、README から公式ドキュメントへ誘導する。

README と公式ドキュメントで詳細説明を重複管理しない。

### VPM repository listing

既存の `Website/` は Astro/Starlight 導入と同時には削除しない。

現在の VPM repository listing の生成と公開を維持したまま、Astro/Starlight サイトを導入する。

最終的には GitHub Pages に対して単一の公開artifactを構築し、

1. Astro/Starlight の静的サイト
2. VPM repository listing に必要な生成物

を同じ公開成果物へ統合する。

この統合によって、既存利用者が使用している VPM repository listing の公開URLを破壊してはならない。

`Website/` はこの統合が完了し、既存listingとの互換性を確認した後にのみ削除する。

## Alternatives

### `docs/` をそのままStarlightプロジェクトにする

既存パスの変更は少ない。

一方で、ADRなどリポジトリ内の開発資料とAstroプロジェクトの設定・依存関係・生成物が同じディレクトリに混在する。

そのため採用しない。

### `src/content/docs/` を使わず独自のcontent directoryを設ける

既存構造に合わせやすいが、Starlight標準構成から外れる理由がなく、設定と保守の複雑さを増やす。

そのため採用しない。

### 日本語だけlocale prefixを付けない

日本語を `/` に配置し、他言語だけ `/en/`、`/ko/` などにする構成も可能である。

しかし言語によってURL構造とファイル構造が非対称になるため採用しない。

### `ja-JP`、`en-US` など地域を常に指定する

現時点では日本語や英語について地域別コンテンツを提供する要求がない。

必要以上にlocaleを細分化するため採用しない。

簡体字中国語については文字体系・地域を明確にするため `zh-CN` をlanguage tagとして使用する。

### ADRもStarlightへ移動する

公式サイトから閲覧しやすくなる一方、ADRの保存場所がWebサイト実装に依存し、既存の開発ワークフローも変更する必要がある。

ADRは利用者向けドキュメントとは異なるライフサイクルを持つため採用しない。

### `Website/` を直ちにAstroサイトへ置き換える

構成は早期に単純化できるが、現在のVPM repository listing生成・公開経路を同時に変更することになり、既存の配布経路を破壊するリスクがある。

そのため段階移行を採用する。

## Rationale

Astro + Starlight は Sobakasu のようなプログラミング言語・ツールチェーンの公式ドキュメントに必要な、Markdown中心のコンテンツ管理、技術文書向けナビゲーション、多言語化、検索、コード表示などを一つの基盤で扱える。

Starlightの標準ディレクトリ構成をそのまま使用することで、独自設定を減らし、Starlight自体の更新にも追従しやすくする。

利用者向けドキュメントとADRを分離することで、それぞれの正本とライフサイクルを明確にできる。

また、VPM repository listing を一度に置き換えず段階移行することで、Sobakasuの配布経路を維持しながら公式サイトを刷新できる。

## Consequences

### Positive

- 利用者向けドキュメントの正本が明確になる。
- Guide、Language Reference、Standard Library Reference を明確に分離できる。
- 日本語を中心として複数言語を同じ構造で管理できる。
- すべての言語でURL規則が統一される。
- Starlight標準構成を利用できる。
- Standard Library Reference の自動生成を将来統合しやすい。
- ADRの既存ワークフローを維持できる。
- VPM listing を壊さず段階的に移行できる。

### Negative

- 移行期間中は `Documentation/`、`docs/adr/`、`Website/` の3系統が存在する。
- 既存Markdownのリンクやasset pathを移行時に修正する必要がある。
- 多言語ドキュメントの翻訳と同期を継続的に管理する必要がある。
- Astro/Node.jsの依存関係とビルド工程がリポジトリに追加される。
- GitHub Pagesへの最終的な公開処理では、Astro buildとVPM listing生成を統合する追加作業が必要になる。
