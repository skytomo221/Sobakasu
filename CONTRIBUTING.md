# Contributing to Sobakasu

## Repository

```text
```text
Sobakasu/
├─ Packages/
│  └─ com.skytomo221.sobakasu/
│     ├─ Editor/                    # Compiler and Unity Editor integration
│     ├─ Tests/                     # Sobakasu tests
│     ├─ StandardLibrary~/          # Generated standard library
│     ├─ StandardLibraryAdditions~/ # Manually maintained standard library additions
│     └─ Tools~/                    # Package tools
├─ Scripts/                         # Development, test, and generation scripts
├─ docs/
│  └─ adr/                          # Architecture Decision Records
└─ Website/                         # Sobakasu website
```

The main Sobakasu implementation is located under:

```text
Packages/com.skytomo221.sobakasu
```

## Commit Message

Follow the [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/)

```text
feat(compiler): support generic Udon extern methods
fix(compiler): restore lazy language item resolution
feat(stdlib): regenerate bindings with generic extern methods
test(editor): add generator regression coverage
docs(adr): add ADR-0044
```

### Scope

```text
compiler
stdlib
editor
adr
website
```

If there is no appropriate scope, it may be omitted.

```text
chore: update repository configuration
```

### Summary

English.

```text
feat(compiler): support generic Udon extern methods

- parse explicit generic type arguments
- validate CLR generic constraints
- lower generic arguments through the Udon ABI
```

For changes that are meaningful on their own, such as compiler implementation, ADRs, and standard library regeneration, create separate commits whenever possible.

## Tests

Run Unity tests using the repository script:

```powershell
.\Scripts\run-unity-tests.ps1
```

## Standard Library Generation

Regenerate the standard library using:

```powershell
.\Scripts\run-standard-library-generator.ps1
```
