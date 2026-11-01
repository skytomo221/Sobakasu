# Changelog

## 0.0.5

### Changed

- Updated highlighting for current Sobakasu declarations, including `type`, `behavior`, and `field`.
- Highlighted contextual `maybe` in extern bindings and nullable `out` parameters without treating ordinary `maybe` identifiers as keywords.
- Added `::` path separator highlighting and Unicode identifier support.
- Scoped synchronization modes to `sync(...)` and marked the removed `static` modifier as deprecated.
- Matched nested block comments to the current language syntax.
- Added backtick auto-closing and surrounding pairs for quoted identifiers.
- Updated the README example to use `state` and `behavior` blocks.

### Added

- TextMate tokenization regression tests, compiler keyword drift checks, and Unicode identifier coverage checks (`npm test`).

## 0.0.4

### Added

- Added syntax highlighting for the `lang` keyword.
