---
title: 'ADR-0056: Sobakasu Source Kinds and Library Source Semantics'
---

# ADR-0056: Sobakasu Source Kinds and Library Source Semantics

## Status

Proposed

## Context

Sobakasu では、UdonBehaviour として実行される通常のスクリプトと、型・関数・定数などの再利用可能な定義を提供するライブラリでは、実行時の意味と利用可能な言語機能が異なる。

これらをすべて同じ `*.sobakasu` として扱い、ソース内容を解析してから役割を判定すると、人間や Unity importer、IDE tooling がファイル名だけから役割を判断できない。また、Library Source が script runtime の状態やライフサイクルへ依存できると、再利用可能な定義と Udon script instance の境界が曖昧になる。

将来的には、Sobakasu で定義された `struct` や `type` 等について Unity Editor 上の表示・編集方法を定義する Editor Source も想定している。ただし、その言語仕様や Unity Editor との統合方式はまだ決定していない。

そのため、Sobakasu Source に Source Kind を導入し、Script Source と Library Source の意味論を明確に分離する。また、将来の Editor Source の suffix だけを先に予約する。

## Decision

### Source Kind

Sobakasu Source に次の Source Kind を定義する。

| Source Kind | ファイル名 | 状態 |
| --- | --- | --- |
| Script Source | `*.sobakasu` | 使用する |
| Library Source | `*.library.sobakasu` | 使用する |
| Editor Source | `*.editor.sobakasu` | 予約する |

Source Kind はソース内部の宣言ではなく、ファイル名の suffix によって決定する。

`library` や `editor` といった Source Kind をソース内部で重複して宣言する構文は要求しない。

### Source Kind の判定

複数の suffix に一致し得る場合は、より具体的な suffix を優先する。

したがって `Foo.library.sobakasu` は文字列上 `*.sobakasu` にも一致するが、必ず Library Source として扱う。`Foo.editor.sobakasu` も同様に Editor Source として扱い、Script Source として扱ってはならない。

概念上の判定順序は次のとおりとする。

```text
*.library.sobakasu -> Library Source
*.editor.sobakasu  -> Editor Source
*.sobakasu         -> Script Source
```

### Script Source

`*.sobakasu` は通常の実行可能な Script Source とする。

Script Source は Udon runtime 上で script instance としての runtime identity を持つことができ、`state`、`behavior` など、Udon script instance を前提とする機能を使用できる。

Script Source から Library Source を参照できる。

### Library Source

`*.library.sobakasu` は、他の Sobakasu Source から再利用される定義を提供する Library Source とする。

Library Source 自身は独立した runtime identity を持たず、単独の UdonBehaviour として実体化されない。

そのため、Library Source では `state` と `behavior` を使用できない。

また、この制約は個別キーワードだけに限定しない。将来新しい言語機能が追加された場合も、Library Source 自身の Udon script instance、runtime state、event lifecycle、network identity 等を必要とする機能は原則として使用できない。

この原則により、Library Source では少なくとも次の種類の機能を使用できない。

- script `state`
- `behavior`
- script state の synchronization
- event handler / receive endpoint など、script instance がイベントを受け取るための宣言
- script instance を主体とする network send / receive

一方、runtime identity を必要としない再利用可能な定義は Library Source に記述できる。

例として、次の種類の定義を想定する。

- compile-time constants
- ordinary functions
- `struct`
- `enum`
- type declarations
- type implementation blocks
- `extern` declarations
- module import / re-export
- public declarations

この一覧は Library Source で使用可能な構文を永久に列挙するものではない。他の ADR で各言語機能の仕様が変更された場合は、その仕様を優先する。

Library Source の基本原則は次のとおりとする。

> Library Source は再利用可能な定義を提供するが、それ自身は実行主体にならない。

### Source 間の依存関係

本 ADR では、Script Source と Library Source の依存方向を次のように定める。

```text
Script  -> Library    allowed
Library -> Library    allowed
Library -> Script     forbidden
```

Library Source が Script Source に依存することは禁止する。

これにより、Library Source が特定の実行主体や runtime state に依存することを防ぐ。

Script Source 同士の参照規則や、Library Source 間の循環依存の扱いについては、本 ADR では新しい規則を定めない。

### Editor Source

`*.editor.sobakasu` を Editor Source 用の suffix として予約する。

Editor Source は、将来的に Sobakasu で定義された `struct`、`type` 等について Unity Editor 上の Inspector 表示や編集体験をカスタマイズする用途を想定している。

ただし、本 ADR では Editor Source の詳細仕様を決定しない。

少なくとも次の事項は未定とし、別 ADR で決定する。

- 構文
- 型システム上の扱い
- Script Source / Library Source との参照関係
- Inspector 定義方法
- Property Drawer 相当の機能
- Inspector 以外の Unity Editor UI を扱うか
- コンパイル方式
- 生成物
- 実行方式
- Unity Editor との統合方式

本 ADR で決定するのは、`*.editor.sobakasu` という Source Kind の名前と suffix を予約することのみである。

Editor Source の実装が存在しない段階でも、`*.editor.sobakasu` を通常の Script Source として暗黙に解釈してはならない。未対応の場合は、Editor Source が未サポートであることを明示的に診断する。

## Alternatives

### すべて `*.sobakasu` として扱う

ファイル内部の内容から Script / Library / Editor を判定する案。

ファイル名だけでは役割を判別できず、Unity importer や tooling が Source Kind を判断するためにソース解析を必要とするため採用しない。

### `*.lib.sobakasu`

Library Source に短い `lib` suffix を使用する案。

短く一般的ではあるが、Source Kind の正式名称を `Library` とし、Editor Source には `editor` を使用するため、正式名称と suffix を一致させる `library` を採用する。

### `*.module.sobakasu`

Library Source を Module Source と呼ぶ案。

Sobakasu では通常のソースもモジュールとして扱われ得るため、Library 固有の役割を示す名前としては曖昧になる。そのため採用しない。

### `*.ui.sobakasu`

将来の Unity Editor 用ソースを UI Source と呼ぶ案。

`UI` では runtime UI、Unity UI Toolkit、VRChat ワールド内 UI などとの区別が不明確になる。用途が Unity Editor 側に属することを明示するため、`editor` を予約する。

### `*.inspector.sobakasu`

Unity Inspector 専用の名前とする案。

現時点で主用途として Inspector カスタマイズを想定しているが、将来的に Inspector 以外の Unity Editor 機能へ拡張する可能性がある。そのため、より上位の概念である `editor` を予約する。

## Rationale

Source Kind をファイル名で明示することで、人間と tooling の双方がソース全文を解析することなくファイルの役割を判断できる。

Library Source に runtime identity を持たせないことで、ライブラリを実行主体から分離し、再利用可能な型・関数・定数・外部 API 定義などを共有できる。

また、Script から Library、Library から Library という依存方向を許可し、Library から Script への依存を禁止することで、Library Source が runtime Script の状態やライフサイクルへ依存することを防ぐ。

Editor Source については用途の方向性だけが決まっており、具体的な言語仕様や Unity Editor 統合方式はまだ検討段階である。そのため suffix の予約だけを先に行い、未成熟な Editor Source の設計を本 ADR で固定しない。

## Consequences

### Positive

- ファイル名だけで Source Kind を識別できる。
- Script Source と Library Source の runtime semantics が明確に分離される。
- Library Source が runtime state を持たないことを言語仕様として保証できる。
- Library Source から Script Source への依存を防ぎ、依存方向を単純化できる。
- Unity importer や IDE tooling が Source Kind をソース解析前に判定できる。
- `library` / `editor` という Source Kind 名と suffix が一致する。
- Editor Source の名前を確保しながら、未決定の仕様を将来の ADR に残せる。

### Negative

- `.library.sobakasu` は `.lib.sobakasu` よりファイル名が長くなる。
- Source Kind がファイル名によって決まるため、suffix の変更はソースの意味の変更になる。
- importer、compiler、IDE tooling 等は複合 suffix を正しく認識する必要がある。
- `*.library.sobakasu` や `*.editor.sobakasu` を単純な `*.sobakasu` として誤分類しない実装が必要になる。
- 既存のライブラリソースを Library Source として扱う場合、実装時にファイル名や参照解決の移行が必要になる可能性がある。
- Editor Source は名前だけが予約された状態となり、詳細仕様は別途設計する必要がある。

## Verification

Source Kind 判定について、少なくとも次を自動テストで検証する。

```text
Foo.sobakasu
-> Script Source

Foo.library.sobakasu
-> Library Source

Foo.editor.sobakasu
-> Editor Source
-> Script Source として扱われない
```

Library Source について、少なくとも次を検証する。

```text
state       -> compile error
behavior    -> compile error
```

また、次の依存方向を検証する。

```text
Script  -> Library    allowed
Library -> Library    allowed
Library -> Script     compile error
```
