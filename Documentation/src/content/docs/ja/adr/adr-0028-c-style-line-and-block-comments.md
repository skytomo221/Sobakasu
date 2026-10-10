---
title: 'ADR-0028: C-style line and block comments'
---

## Status

Accepted

## Context

Sobakasu は、波括弧と `fn`、`struct`、`impl` などの宣言を中心とした C および Rust 風の表層構文を持つが、現時点ではコメント構文を提供していない。利用者には、1 行を簡潔に注記する手段と、複数行またはコードの領域全体を一時的に無効化する安全な手段が必要である。コメント設計では、コンパイラに本来不要な構文木の複雑さを追加することなく、既存の `/` および `/=` 演算子、文字列リテラル、source span、診断位置を維持しなければならない。

## Decision

Sobakasu は次の 2 種類のコメント形式をサポートする。

```sobakasu
// line comment
/* block comment */
```

行コメントは `//` で始まり、次の `\r`、`\n`、またはファイル末尾まで続く。末尾の改行は必須ではない。

ブロックコメントは `/*` で始まり、対応する `*/` で終わる。ブロックコメントは複数行にまたがってよく、入れ子にできる。lexer は入れ子の深さを追跡し、`/*` ごとに増やし、`*/` ごとに減らす。深さが 0 に戻ったときだけコメントは終了する。

コメントは trivia とする。lexer は次の token を生成する前に whitespace とともに消費し、comment token を生成せず、AST にコメントを保持しない。したがって、コメント内で Sobakasu 構文に見える文字列も parse されない。

コメントの認識は token 間でのみ行う。文字列または文字リテラル内の `//`、`/*`、`*/` などの並びは literal content のままとする。`/` の直後に `/` または `*` が続かない場合は、`/=` を含む既存の `/` の tokenization を適用する。

lexer はコメント内のすべての文字を進める。このため、既存の source span と `SourceText` の line map は、LF、CRLF、CR のいずれの行末の後でも元の source position を識別し続ける。

block-comment nesting depth が 0 でないままファイル末尾に到達した場合は、最も外側の未終端コメントの開始 `/*` に `Unterminated block comment` lexer diagnostic を生成する。

以下はこの決定の対象外とする。

* `///`、`//!`、`/** ... */` の documentation-comment semantics。これらは現時点では通常のコメントである。
* formatting または documentation generation のためにコメントを AST または CST に保持すること。
* `#` コメントおよび追加のコメント構文。

## Alternatives

### `//` のみをサポートする

実装は最も簡単になるが、複数行の説明やコードの領域を一時的に無効化する用途には不便である。

### `//` と入れ子にできない `/* ... */` をサポートする

C および C# に似ているが、すでに block comment を含むコードをコメントアウトすると、内側の `*/` で終了して残りのテキストがコードとして露出する。

### `#` をサポートする

Ruby、Python、shell language の利用者には馴染みがあるが、`//` の方が Sobakasu の C および Rust 風の構文と視覚的に一貫する。

### コメントを parser token として保持する

将来の formatter、documentation generator、source-preserving tool に役立つが、現在の compilation semantics に影響を与えずに parser と syntax tree を複雑化する。

## Rationale

`//` と `/* ... */` は Sobakasu の既存の視覚的なスタイルに合い、C#、Rust、および関連言語に慣れた利用者にとって自然である。行コメントは短い説明や行末の注記に簡潔であり、block comment は長い説明やコードを一時的に無効化する用途に有用である。Rust と同様に block comment を入れ子にできれば、すでに `/* ... */` を含むコードも安全にコメントアウトできる。

lexer でコメントを消費すると、parser、AST、Binder、IR、UASM backend を変更せずに whitespace と同様の semantics を与えられる。入れ子の深さを明示的に追跡する方式は小さく決定的であり、regular-expression-based preprocessing pass よりも入れ子コメントを確実に扱える。

## Consequences

### Positive

* Sobakasu source に行コメント、複数行コメント、inline comment、入れ子の block comment を記述できる。
* block comment を含む既存コードを、外側の 1 つの block comment として無効化できる。
* 文字列リテラル、除算、compound division assignment、source location は既存の動作を維持する。
* コメント専用の parser または AST node を導入しない。

### Negative

* 未終端 block comment には専用の lexer diagnostic が必要となる。
* コメントは破棄されるため、現在の syntax tree は source-preserving formatting または documentation extraction をサポートできない。
* 入れ子 block comment の走査には、最初の `*/` を単純に検索するのではなく、明示的な depth tracking が必要になる。
