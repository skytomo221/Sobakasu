# Agent Instructions

@RTK.md @CONTRIBUTING.md

これが読めていれば UTF-8 BOM なしで正しく表示されています。

## Repository

See CONTRIBUTING.md for the repository structure.

Keep searches narrowly scoped to the files relevant to the task.

Ignore these directories unless explicitly needed:

```text
Library
Temp
Logs
obj
UserSettings
```

## Design

Sobakasu is an Udon-first language and compiler for the VRChat Udon VM.

* C# compatibility is not a goal.
* Prioritize Unity Editor integration.
* Keep Parser, Binder, IR, and UASM backend responsibilities separate.
* Do not move semantic analysis or type resolution into the backend.

## Before Changes

Inspect the relevant implementation and tests before modifying code.

For language changes, consider all affected compiler stages as needed.

## Changes

* Do not make unrelated changes or refactors.
* Preserve existing naming and code style.
* Check compatibility when changing public APIs or serialized formats.
* Prefer existing catalogs, symbols, and type-resolution mechanisms over new hardcoded logic.
* Use the existing diagnostic system; do not silently ignore errors.
* Do not commit generated files or Unity caches.
* Do not modify unrelated files.

## ADRs

Treat the current request as the primary design basis.

When creating an ADR:

1. List `docs/adr` to determine numbering and naming.
2. Read `docs/adr/template.md` if it exists.
3. Read existing ADRs only when directly relevant to the requested change or a concrete conflict.

Do not broadly read historical ADRs for background or examples.

If a decision conflicts with an existing ADR, update it or add a superseding ADR.

Unless the task is ADR-only, continue with implementation and tests after creating the ADR.

## Tests

Run relevant existing tests after changes.

Add or update tests for new syntax, typing rules, lowering, or UASM output.

Use the repository script to run Unity tests:

```powershell
.\Scripts\run-unity-tests.ps1
```

Prefer filtered test runs when possible.

If tests cannot be run, report:

* which tests were not run,
* why,
* what was verified instead.

Do not ignore failing tests without evidence that they are unrelated.

## PowerShell 5.1 and Encoding

Use UTF-8 without BOM for text files.

When using Windows PowerShell 5.1:

* Prefer `powershell.exe -NoProfile`.
* Prefer RTK for supported file reads, searches, Git commands, and shell operations.
* Do not use `Get-Content` for BOM-less UTF-8 when `rtk read` is available.
* Avoid rewriting files only to fix display encoding.
* Preserve existing line endings unless a change is required.

When PowerShell 5.1 must write a text file, explicitly use UTF-8 without BOM:

```powershell
$encoding = [Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText($path, $content, $encoding)
```

If non-RTK command output is garbled, configure PowerShell UTF-8 console output only when needed.

Prefer partial edits over rewriting entire files.

## Final Report

Keep the final report concise and include:

* changes made,
* main files changed,
* tests and results,
* remaining limitations or unverified items,
* a suggested Conventional Commits-compatible commit message.

Do not create commits unless the user explicitly asks you to do so.
