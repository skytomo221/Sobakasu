# ADR-0028: C-style line and block comments

## Status

Accepted

## Context

Sobakasu has a C- and Rust-like surface syntax built around braces and declarations such as `fn`, `struct`, and `impl`, but it does not currently provide a comment syntax. Users need a concise way to annotate a line and a safe way to temporarily disable multiple lines or an entire region of code. Any comment design must preserve the existing `/` and `/=` operators, string literals, source spans, and diagnostic locations without adding syntax tree complexity that the compiler does not otherwise need.

## Decision

Sobakasu supports two comment forms:

```sobakasu
// line comment
/* block comment */
```

A line comment begins with `//` and continues to the next `\r`, `\n`, or end of file. A trailing newline is not required.

A block comment begins with `/*` and ends at its matching `*/`. Block comments may span lines and may nest. The lexer tracks a nesting depth, incrementing it for each `/*` and decrementing it for each `*/`; the comment ends only when the depth returns to zero.

Comments are trivia. The lexer consumes them together with whitespace before producing the next token, and it does not produce comment tokens or preserve comments in the AST. Consequently, text that resembles Sobakasu syntax inside a comment is not parsed.

Comment recognition only occurs between tokens. Sequences such as `//`, `/*`, and `*/` inside string or character literals remain literal content. When `/` is not followed by `/` or `*`, the existing `/` tokenization applies, including `/=`.

The lexer advances through every character in a comment. Existing source spans and the `SourceText` line map therefore continue to identify the original source position after LF, CRLF, or CR line endings.

Reaching end of file while the block-comment nesting depth is nonzero produces an `Unterminated block comment` lexer diagnostic at the opening `/*` of the outermost unterminated comment.

The following are outside this decision:

* Documentation-comment semantics for `///`, `//!`, or `/** ... */`; these are ordinary comments for now.
* Preserving comments in an AST or CST for formatting or documentation generation.
* `#` comments or any additional comment syntax.

## Alternatives

### Support only `//`

This is the simplest implementation, but it is inconvenient for multi-line explanations and temporarily disabling a region of code.

### Support `//` and non-nesting `/* ... */`

This resembles C and C#, but commenting out code that already contains a block comment would terminate at the inner `*/` and expose the remaining text as code.

### Support `#`

This is familiar from Ruby, Python, and shell languages, but `//` is visually more consistent with Sobakasu's C- and Rust-like syntax.

### Preserve comments as parser tokens

This would help future formatters, documentation generators, or source-preserving tools, but it would complicate the parser and syntax tree without affecting current compilation semantics.

## Rationale

`//` and `/* ... */` fit Sobakasu's existing visual style and are natural to users familiar with C#, Rust, and related languages. Line comments are concise for short explanations and end-of-line notes. Block comments are useful for longer explanations and temporarily disabling code. Nesting block comments, as in Rust, lets users safely comment out code that already contains `/* ... */`.

Consuming comments in the lexer gives them whitespace-like semantics while keeping the parser, AST, Binder, IR, and UASM backend unchanged. Explicit nesting-depth tracking is small, deterministic, and handles nested comments more reliably than a regular-expression-based preprocessing pass.

## Consequences

### Positive

* Sobakasu source can contain line, multi-line, inline, and nested block comments.
* Existing code containing block comments can be disabled as one outer block comment.
* String literals, division, compound division assignment, and source locations retain their existing behavior.
* No comment-specific parser or AST nodes are introduced.

### Negative

* Unterminated block comments require a dedicated lexer diagnostic.
* Comments are discarded, so the current syntax tree cannot support source-preserving formatting or documentation extraction.
* Nested block-comment scanning requires explicit depth tracking rather than a simple search for the first `*/`.
