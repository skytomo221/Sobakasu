# Agent Instructions

@RTK.md

これが読めていれば UTF-8 BOM なしで正しく表示されています。

## Scope

Applies to this repository and all subdirectories.

If a deeper `AGENTS.md` exists, its instructions take precedence for that subtree.

## Repository

Main Sobakasu package:

```text
Packages/com.skytomo221.sobakasu
```

Search in this order:

1. `Packages/com.skytomo221.sobakasu`
2. `docs`
3. `ProjectSettings`
4. repository root

Ignore unless explicitly needed:

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
* Respect existing ADRs.
* Check new design decisions against existing ADRs.
* Keep Parser, Binder, IR, and UASM backend responsibilities separate.
* Do not move semantic analysis or type resolution into the backend.

## Before Changes

Inspect relevant implementation, tests, and ADRs before modifying code.

Understand responsibilities and call relationships instead of relying only on filenames.

For language features, check all affected stages as needed:

```text
Lexer
Parser
Binder
Desugar
IR Lowerer
Optimizer
UASM Assembler
Unity Editor integration
Tests
```

Do not assume a feature is complete after changing only one layer.

## Changes

* Do not make unrelated changes or refactors.
* Preserve existing naming and code style.
* Check compatibility when changing public APIs or serialized formats.
* Prefer existing catalogs, symbols, and type-resolution mechanisms over new hardcoded logic.
* Use the existing diagnostic system; do not silently ignore errors.
* Do not commit generated files or Unity caches.
* Do not modify unrelated files.

## ADRs

Before changing architecture or language semantics, search existing ADRs under `docs`.

If a decision conflicts with an existing ADR:

* update the ADR, or
* add a new ADR that supersedes it.

If no ADR is needed, explain why when the change represents a significant design decision.

Unless the task is ADR-only, do not stop after writing an ADR; implement and test the requested change.

## Unity Editor Resolution

When a task needs the Unity Editor, resolve its path once for that task and reuse the resolved path for every subsequent Unity invocation.

* Read the required version from `m_EditorVersion` in `ProjectSettings/ProjectVersion.txt`; do not hardcode a Unity version.
* First use `UNITY_EDITOR_PATH` when it points to an existing `Unity.exe`.
* Otherwise, directly test these version-specific candidates in order:

```text
%ProgramFiles%\Unity\Hub\Editor\<version>\Editor\Unity.exe
C:\Unity\Editor\<version>\Editor\Unity.exe
D:\Unity\Editor\<version>\Editor\Unity.exe
E:\Unity\Editor\<version>\Editor\Unity.exe
D:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe
E:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe
```

Use one small PowerShell command to read the version and test the candidates. Do not repeatedly read `ProjectVersion.txt`, retest the same candidates, or rerun discovery after a path has been found.

Only perform broader discovery if none of the direct candidates exists. Do not begin with recursive searches of entire drives or emit large file listings merely to locate Unity.

When running EditMode tests with `-runTests`, do not add `-quit` by default. Wait for Unity to finish the test run and verify that the requested results XML was generated before treating the run as complete.

## Tests

Run relevant existing tests after changes.

Add or update tests for new syntax, typing rules, lowering, or UASM output.

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
* remaining limitations or unverified items.
