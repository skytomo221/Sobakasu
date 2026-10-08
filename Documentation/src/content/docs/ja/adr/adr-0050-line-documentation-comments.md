---
title: 'ADR-0050: Line Documentation Comments'
---

# ADR-0050: Line Documentation Comments

## Status

Proposed

## Context

Sobakasu に、ソースコードからAPIドキュメントを生成する汎用ドキュメントツール Sobakasu Doc を導入する予定である。

Sobakasu Doc は標準ライブラリ専用ではなく、ユーザーが作成した任意の `.sobakasu` ライブラリにも利用できる必要がある。そのため、APIの説明はDoc Generatorがソースコードを独自解析するのではなく、Sobakasu言語の構文要素としてCompilerが認識し、SyntaxおよびSymbolまで保持できる必要がある。

documentation commentの構文については、Rust、C#、Java、Swiftなどで広く利用されている `///` 形式を参考にする。

一方、Rustの `//!` はinner documentation commentとして、Rust固有のinner/outer attributeモデルと対応している。Sobakasuには現時点で同等のattributeモデルがなく、file/module documentationの必要性もまだ確定していないため、Phase 1では導入しない。

また、documentation commentと宣言の関連付けが過度に緩いと、コード編集後に古いdocumentationが意図しない別の宣言へ関連付けられる可能性がある。ソースコード上の見た目と意味の対応を明確にする必要がある。

## Decision

Sobakasuのdocumentation commentとして、line documentation comment `///` を導入する。

基本形は次のとおりとする。

```sobakasu
/// Returns the distance between two points.
pub fn distance(a: Vector3, b: Vector3) -> f32 {
    ...
}
```

documentation commentの本文はMarkdownとして扱う。

documentationのcanonical languageは英語とし、ソースコード内のdocumentation commentに言語設定値は持たせない。他言語への翻訳はSobakasu Doc側の後段のi18n機構で扱う。

### Syntax

`///` で始まる行をdocumentation commentとする。

```sobakasu
/// Documentation.
```

連続した複数の `///` 行は、一つのdocumentation comment blockとして扱う。

```sobakasu
/// Returns the distance between two points.
///
/// This function operates on two vectors.
pub fn distance(a: Vector3, b: Vector3) -> f32 {
    ...
}
```

documentation本文中の空行は、内容を持たない `///` 行によって表現する。

### Documentation Text Normalization

各documentation lineについて、`///` より前のインデントは本文に含めない。`///` を除去した後、直後にASCII spaceが1文字だけある場合はその1文字だけを除去する。それ以外のwhitespaceと本文はそのまま保持する。

複数行の本文は `\n` で連結する。本文全体や各行に対する`Trim()`のような正規化は行わない。これにより、Markdownの段落、コードブロック、意図的な先頭・末尾のwhitespaceを失わない。

例えば次は、`Foo\nBar\n\n Indented` として保持される。

```sobakasu
/// Foo
/// Bar
///
///  Indented
```

`////` 以上の `/` で始まるコメントはdocumentation commentとはせず、通常のline commentとして扱う。

```sobakasu
//// This is an ordinary comment.
```

Phase 1では以下の形式をdocumentation commentとして導入しない。

```text
//!
/**
/*!
```

必要性が生じた場合は、file/module documentationやblock documentationの設計と合わせて別途決定する。

### Association

documentation comment blockは、物理的に直後に存在するdocumentable declarationにのみ関連付ける。

次は有効である。

```sobakasu
/// Documentation.
pub fn foo() {
}
```

documentation comment blockと宣言の間に空行が存在する場合は関連付けない。

```sobakasu
/// Documentation.

pub fn foo() {
}
```

通常コメントが挟まる場合も関連付けない。

```sobakasu
/// Documentation.
// Ordinary comment.
pub fn foo() {
}
```

したがって、documentationの段落を分割する場合は物理的な空行ではなく、`///` のみの行を使用する。

```sobakasu
/// First paragraph.
///
/// Second paragraph.
pub fn foo() {
}
```

他の構文要素がdocumentation commentと宣言の間に存在する場合も関連付けない。

ただし、`lang` のように構文上その宣言を構成するdeclaration prefixとして
許可されているlanguage itemは、独立した構文要素としてassociationを切断しない。
そのため、`lang` が許可されているdocumentable declarationでは、documentationは
prefixではなく最終的な宣言へ関連付ける。

```sobakasu
/// Documentation for Foo.
lang "..."
struct Foo {
}
```

この場合documentationは `StructDeclarationSyntax` と対応する `TypeSymbol` に
関連付ける。一方、`impl` はdocumentable declarationではないため、`lang` prefixを
伴っていてもorphan documentation commentとなる。

宣言へ関連付けられなかったdocumentation comment blockはorphan documentation commentとしてdiagnosticの対象とする。

### Documentable Declarations

documentation commentを付与できる対象は、Compiler上でAPIまたは名前付き宣言として表現される次の宣言とする。

- `type`
- `struct`
- struct field
- `enum`
- enum variant
- `fn`
- `const`
- `state`
- `on`
- `receive`
- `impl` 内の `fn`

`pub` の有無はdocumentation commentを記述できるかどうかには影響しない。

したがってprivate declarationにもdocumentationを記述できる。

```sobakasu
/// Internal cached value.
state cached_value: f32;
```

一方、将来のSobakasu Docでは通常のドキュメント生成対象を `pub` APIに限定できる。この公開範囲の選択はdocumentation commentの言語仕様とは分離する。

特に `pub state` は公開APIとしてdocumentation対象に含める。

```sobakasu
/// Controls the movement speed exposed by this program.
pub state speed: f32;
```

同期指定を持つstateも同様である。

```sobakasu
/// The current door state synchronized across clients.
pub sync state is_open: bool;
```

`on` および `receive` は通常の関数APIとは性質が異なるが、Sobakasuプログラムのentry pointまたはnetwork receiverとして意味のあるdocumentationを持てるため、言語機能として `///` を許可する。

```sobakasu
/// Initializes the door state.
on start() {
}
```

`impl` 自体にはdocumentation commentを関連付けない。

```sobakasu
impl Vector3 {
    /// Returns the length of this vector.
    pub fn length(self) -> f32 {
        ...
    }
}
```

型についてのdocumentationは型宣言へ、methodまたはassociated functionについてのdocumentationは各 `fn` 宣言へ記述する。

`use`、ローカル変数、statement、expressionなど、APIまたは名前付き宣言そのものではない構文要素はdocumentable declarationとしない。

### Compiler Representation

documentation commentはDoc Generator専用のテキスト処理として実装しない。

Lexer / Parserで認識したdocumentation commentをSyntax tree上で保持し、Binderによって対応するSymbolへ引き継げる構造とする。

基本的な情報フローは次のとおりとする。

```text
/// documentation
        ↓
Lexer / Parser
        ↓
Syntax
        ↓
Binder
        ↓
Symbol
```

通常の `//` および `/* ... */` コメントをdocumentation生成のために後から再解析してはならない。

Phase 1ではdocumentationをSyntaxおよびSymbolまで保持することを責務とし、MarkdownやJSONへのRenderer、公開API一覧の生成、i18nは実装しない。

documentation本文の構造化、例えばParameters、Returns、RemarksなどをCompilerレベルで個別フィールドとして扱うかどうかについてもPhase 1では決定しない。Phase 1ではcanonicalなMarkdown本文を失わず保持できることを保証する。

## Alternatives

### Rust-style inner and outer documentation comments

`///` に加えて `//!` を導入し、declaration documentationとmodule/file documentationを区別する案。

Rustでは `///` と `//!` がouter/inner attributeの構造と対応しているが、Sobakasuには現時点で同等のattributeモデルがない。また、module/file documentationの具体的な利用方法もまだ確定していない。

そのためPhase 1では採用しない。

### Block documentation comments

`/** ... */` や `/*! ... */` をdocumentation commentとして導入する案。

複数行documentationは連続する `///` で十分に表現できる。複数の記法を最初から提供するとLexer、Parser、diagnostic、formatter等の仕様面積が増えるため、必要性が確認されるまでは導入しない。

### Allow blank lines before declarations

documentation commentと宣言の間に空行があっても、次のdocumentable declarationへ関連付ける案。

編集によってdocumentationと宣言の距離が離れても自動的に関連付けられるため、意図しない宣言へ古いdocumentationが付く可能性がある。

Sobakasuではソース上で隣接していることを関連付けの条件とし、この案は採用しない。

### Allow ordinary comments between documentation and declarations

`///` と宣言の間に通常の `//` コメントを許可する案。

documentation blockの境界が視覚的・構文的に曖昧になり、どのコメントがAPI documentationを構成するか判断しにくくなるため採用しない。

### Restrict documentation comments to public declarations

`pub` 宣言にのみ `///` を許可する案。

private declarationにもIDE表示、内部設計の説明、将来的なprivate documentation生成などの用途がある。また、「documentationを保持できるか」と「Sobakasu Docがデフォルトで何を出力するか」は別の責務である。

そのため言語機能としてはprivate declarationにもdocumentation commentを許可する。

## Rationale

`///` は既存の複数のプログラミング言語でdocumentation commentとして使用されており、通常の `//` コメントとの差も小さく、Sobakasuのソースコードに自然に導入できる。

line documentation commentだけに限定することで、構文を単純に保ちつつMarkdownによる複数段落やコードブロックを表現できる。

documentation blockと宣言の物理的な隣接を要求することで、ソースコード上の見た目とCompiler上の関連付けを一致させられる。空行や通常コメントによってblockが終了するため、編集後に古いdocumentationが意図しない宣言へ移動することも防ぎやすい。

documentationを `pub` 専用機能にしないことで、言語機能とドキュメント公開ポリシーを分離できる。これにより、将来Sobakasu Docがpublic APIのみを列挙する場合でも、Compilerのdocumentation model自体はprivate declarationを含めて一貫したものにできる。

また、documentation commentをCompilerのSyntax / Binder / Symbol pipelineに組み込むことで、将来のSobakasu Docはソーステキストを再解析することなく、型、signature、visibility、extern binding、source locationなどCompilerが確定した情報とdocumentationを組み合わせられる。

## Consequences

### Positive

- `///` という単純で既知性の高い構文でAPI documentationを記述できる。
- documentation本文にMarkdownをそのまま使用できる。
- documentationと宣言の対応がソースコード上で明確になる。
- orphan documentationをCompilerが検出できる。
- `pub` とprivateの両方で同じdocumentation機構を利用できる。
- `pub state`、event、network receiverなどSobakasu固有の公開・実行インターフェースにもdocumentationを保持できる。
- StandardLibraryGeneratorが将来 `///` 付きソースを生成した場合も、ユーザーライブラリと同じCompiler pipelineで処理できる。
- Sobakasu Docが独自のソース解析器を持つ必要がなくなる。
- 将来のMarkdown / JSON / HTML rendererやi18n機構の共通入力としてSymbol上のdocumentationを利用できる。

### Negative

- 空行や通常コメントをdocumentation commentと宣言の間に置けないため、記述形式には一定の制約が生じる。
- documentation内の空行は `///` のみの行として記述する必要がある。
- `//!` やblock documentation commentを必要とする場合は、将来別途言語仕様を追加する必要がある。
- file/module全体のdocumentationはPhase 1では表現できない。
- orphan documentationを正確に診断するため、Lexer / Parserで通常コメントとは異なる扱いが必要になる。
- documentationをSyntaxからSymbolまで保持するため、既存Compilerの宣言およびSymbol生成経路に変更が必要になる。
- Parameters、Returns、Remarks等の構造化方法はこのADRでは決定しないため、Sobakasu Doc本体の設計時に別途決定が必要になる。
