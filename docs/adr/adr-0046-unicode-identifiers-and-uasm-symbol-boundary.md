# ADR-0046: Unicode Identifiers and the UASM Symbol Boundary

## Status

Accepted

## Context

Sobakasu previously recognized identifiers with `char.IsLetter` and
`char.IsLetterOrDigit`. That differed from Unicode identifier semantics,
excluded valid XID names, and left the Standard Library Generator unable to
render CLR member names that collide with Sobakasu keywords. The source
language also needs names that cannot be written as ordinary identifiers
without coupling its grammar to UASM's narrower data-symbol grammar.

This decision follows ADR-0003 by keeping lexical/source naming in the
front-end and UASM emission checks in the backend. It preserves the public
state ABI of ADR-0014, callable naming rules of ADR-0015, aggregate physical
symbol requirements of ADR-0021, declarative extern bindings of ADR-0030, and
the shared generator/compiler policy required by ADR-0035.

## Decision

Normal Sobakasu identifiers use Unicode XID properties:

```text
NormalIdentifier := ("_" | XID_Start) XID_Continue*
```

A compiler-owned, versioned XID range table is the single implementation of
this rule. Lexer, standard-library resolution, and generator rendering use
the same identifier facts; no component maintains a keyword or identifier
grammar of its own. Source normalization is not performed.

Backtick-quoted identifiers are accepted wherever an identifier is accepted.
They cannot contain a backtick or line break. The lexer records their token
text as the unquoted semantic name, so Parser, Binder, symbols, IR, and extern
resolution never receive a backtick-containing name. Bare names are rendered
only when they are both normal identifiers and not Sobakasu keywords;
otherwise a representable name is rendered with backticks.

Sobakasu identifiers and UASM data symbols are separate. The backend validates
externally visible UASM names against the current UAssembly character grammar.
Public state and other externally named ABI entries retain their semantic name
and report a compile error if it cannot be emitted; they are never silently
mangled. For non-ABI names that must be represented in UASM, the backend may
use the deterministic UTF-8 encoding `__sbk_q_<HEX>`, adding `_1`, `_2`, and
so on when needed. User-provided valid UASM names always reserve their exact
name before generated or mangled candidates, and `__sbk_q_` remains available
to users.

## Alternatives

### Keep `char.IsLetter`-based identifiers

Rejected because it is not the XID specification and cannot represent the
language contract consistently across compiler and generator.

### Treat keywords as identifiers after a dot

Rejected because it changes keyword parsing contextually and does not handle
spaces, punctuation, or emoji.

### Mangle every public name

Rejected because it changes the Udon-visible ABI established by ADR-0014.

## Rationale

The shared facts establish one source-language contract while preserving the
existing front-end/backend separation. Quoting solves source spelling without
altering semantic names, and the explicit UASM boundary makes ABI failures
actionable instead of accidental assembler failures.

## Consequences

### Positive

* Unicode XID names and generated keyword members are supported consistently.
* Quoted names resolve exactly like their unquoted semantic counterparts.
* Public Udon ABI names remain predictable and are checked before assembly.

### Negative

* The compiler carries a Unicode XID table and must intentionally update it
  when the supported Unicode version changes.
* Backticks and line breaks remain unavailable inside quoted names until an
  explicit escaping design is adopted.
