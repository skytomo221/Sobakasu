---
title: 'ADR-0055: Sobakasu source keyword vocabulary'
---

## Status

Proposed

## Context

Sobakasu は Rust を含む複数のプログラミング言語から影響を受けており、現在の source language には `fn`、`impl`、`mut`、`pub`、`mod` など、Rust と同じ短縮形のキーワードが存在する。

これらの構文や、それらが表現する言語機能そのものを他言語から受け継ぐことは問題ではない。`use`、`struct`、expression-oriented control flow、`self` など、他言語から影響を受けた設計を採用すること自体は Sobakasu の設計方針と矛盾しない。

一方で、source keyword の綴りまで既存言語の慣習をそのまま採用する必要はない。

特に次の語は、それぞれ表現する概念に対して強く省略されている。

```text
fn
impl
mut
pub
mod
lang
```

Sobakasu はすでに `on`、`receive`、`send`、`behavior`、`state` など独自の語彙を持つ。通常関数を `fn` とだけ表現すると、`on` や `receive` と並べた際にも宣言種別の名称として不均衡になる。

また、Sobakasu はまだ alpha 段階であり、既存 source compatibility を維持するために不適切な語彙を固定する段階ではない。

この変更では Rust らしさを排除することを目的としない。Rust、F#、Objective-C、JavaScript、PHP、Lua、Pascal など既存言語の語彙と実績を参考にしつつ、Sobakasu の概念を最も明瞭に表現できる語を選択する。

## Decision

Sobakasu の source keyword は、特定言語の表面的な綴りを模倣するのではなく、その概念を Sobakasu として最も明瞭に表現する語を選ぶ。

一般的なプログラミング用語として定着した `struct`、`enum`、`const`、`extern`、`ref` 等の略語はそのまま許容する。

以下のキーワードを変更する。

```text
fn   -> function
impl -> implementation
mut  -> mutable
pub  -> public
mod  -> module
lang -> language item
```

`language item` は単一の複合識別子や `language_item` ではなく、2つのキーワードを連続して記述する構文とする。

```sobakasu
language item "maybe"

public enum Maybe<T> {
    Nothing,
    Just(T),
}
```

### `function`

通常関数は `function` で宣言する。

```sobakasu
function add(a: i32, b: i32) -> i32 {
    a + b
}
```

callable declaration の種類を先頭から明示できる。

```sobakasu
function update() {
}

on interact() {
}

receive reset() {
}
```

`function`、`on`、`receive` はそれぞれ異なる callable declaration を表す。

`function` というフルスペルの関数宣言キーワードについては、JavaScript、PHP、Lua、Pascal などを参考にした。

ただし Sobakasu がこれらの言語の function model を採用するという意味ではない。Sobakasu では `on` や `receive` と同様に、宣言の種類を先頭の明示的な語で表現するために `function` を採用する。

### `implementation`

型に属する associated function、instance method、operator その他の実装を定義するブロックには `implementation` を使用する。

```sobakasu
implementation Vector {
    public function new(x: f32, y: f32) -> Vector {
        Vector { x, y }
    }

    public function length(self) -> f32 {
        // ...
    }
}
```

`implementation` という語については、Objective-C の `@implementation` を既存言語における先例として参考にした。

ただし Objective-C の interface / implementation 分離モデルを採用するものではない。

Sobakasu の `implementation T` は Rust の inherent `impl T` に近く、型の associated function、instance method、operator、external binding 等をまとめる基本的な実装単位である。

`implementation` は inherent implementation の基本構文とする。

将来 interface、trait、その他の契約型機構を導入する場合、その実装構文を同じ `implementation` から拡張できる余地を残すが、その具体的な構文はこの ADR では決定しない。

### `mutable`

local binding の可変性は `mutable` で明示する。

```sobakasu
let x = 1;
let mutable y = 2;
```

`let` は binding declaration を表し、`mutable` はその binding の性質を表す。

この構文については、特に F# の次の形式を参考にした。

```fsharp
let x = 1
let mutable y = 2
```

OCaml でも mutable field に `mutable` というフルスペルのキーワードが使用されており、`mutable` 自体は既存言語で実績のある語である。

Sobakasu では Swift、Kotlin、Zig 等のように immutable / mutable で `let` と `var` を使い分けるのではなく、binding declaration と mutability を独立した概念として維持する。

したがって、

```sobakasu
let mutable value = 0;
```

を採用する。

### `public`

public visibility は `public` で明示する。

```sobakasu
public function foo() {
}

public struct Foo {
}

public module math;
```

`public` は C#、Java、F# その他多数の言語で使用されている一般的な visibility keyword である。

特に Sobakasu では `pub` を短縮形として維持する必要性がなく、可読性を優先して `public` を採用する。

### `module`

子 module の宣言には `module` を使用する。

```sobakasu
module internal;
public module math;
```

`module` というフルスペルのキーワードについては F#、Ruby その他の module concept を持つ言語を参考にした。

ただし Sobakasu の module semantics はそれらの言語と同一ではない。

Sobakasu の `module child;` は、規約に基づいて存在する直接の子 module を module graph へ接続する declaration である。既存の module graph、visibility、re-export semantics は変更しない。

### `language item`

compiler-known semantic identity を宣言へ関連付ける prefix は `language item` とする。

```sobakasu
language item "maybe"

public enum Maybe<T> {
    Nothing,
    Just(T),
}
```

これは既存言語の特定構文を直接参考にしたものではなく、Sobakasu compiler 内で既に使用している `language item` という概念名をそのまま source language に表出させる。

2語からなる keyword-like construct を許容すること自体は特異なものではない。例えば C# にも `yield return` のように複数のキーワードを組み合わせて一つの構文上の概念を表す例がある。

`language item` は頻繁に使用する一般ユーザー向け構文ではなく、主として standard library や compiler integration のための declaration prefix である。そのため短さより意味の明示を優先する。

### `use`

`use` は変更しない。

```sobakasu
use core::string;
public use math::Vector;
```

`use` は Rust から影響を受けた語彙である。

しかし Sobakasu の目的は Rust 由来の構文を排除することではない。

`use` は短縮語ではなく、それ自体が一般的な英単語であり、`using` 等の類似構文とも明確に区別できる。また現在の import、alias、re-export semantics を簡潔に表現できている。

そのため Rust と同じ語であることだけを理由として変更しない。

### Existing established abbreviations

以下のようなキーワードは変更しない。

```text
struct
enum
const
extern
ref
```

これらは特定の一言語に強く依存した表記ではなく、プログラミング言語およびソフトウェア開発の用語として広く定着している。

したがって、機械的に次のようなフルスペル化は行わない。

```text
struct -> structure
enum   -> enumeration
const  -> constant
extern -> external
ref    -> reference
```

特に `struct` は C、C++、C#、Rust、Go 等、多数の言語で一般的に使用されている。

この ADR は「すべての略語を禁止する」という規則を導入するものではない。

### Compatibility

旧キーワード、

```text
fn
impl
mut
pub
mod
lang
```

には compatibility syntax や migration-only keyword を設けない。

変更後は、それらが他の予約語として必要でない限り通常の identifier として扱えるようにする。

例えば `fn` は変更後に予約語ではない。

```sobakasu
let fn = 1;
```

旧構文を Parser が特別扱いして migration diagnostic を出すこともしない。

Sobakasu は alpha 段階であり、この時点では source compatibility よりも言語仕様を適切な形へ収束させることを優先する。

## Language influences

今回の語彙選択で主に参考にした言語と要素を以下にまとめる。

| Sobakasu         | 主な参考                     | 参考にした点                                               |
| ---------------- | ---------------------------- | ---------------------------------------------------------- |
| `function`       | JavaScript, PHP, Lua, Pascal | `function` を省略せず関数宣言に使用する                    |
| `implementation` | Objective-C                  | `implementation` という語を実装単位の名称として使用する    |
| `mutable`        | F#, OCaml                    | 可変性を `mutable` という明示的な修飾語で表す              |
| `public`         | C#, Java, F# 等              | visibility を `public` と明示する                          |
| `module`         | F#, Ruby 等                  | `module` を省略せず使用する                                |
| `language item`  | Sobakasu 自身                | compiler concept の名称をそのまま source syntax に使用する |
| `use`            | Rust                         | 現在の語彙を意図的に維持する                               |
| `struct`         | C, C++, C#, Rust, Go 等      | 広く定着した略語として維持する                             |

これらは各言語の文法体系をそのまま組み合わせることを意味しない。

Sobakasu は、既存言語で実績のある語彙や概念を参考にしながら、それぞれを Sobakasu の semantics に合わせて独立して採否判断する。

## Alternatives

### Keep the current Rust-style spellings

```sobakasu
pub mod math;

impl Foo {
    pub fn update(self) {
        let mut value = 0;
    }
}
```

短く、Rust 経験者には馴染みがある。

しかし、これらの短縮形を Sobakasu が採用し続ける積極的な理由は弱く、`on`、`receive`、`behavior` 等の Sobakasu の他の語彙とも統一しにくい。

そのため採用しない。

### Use short but different keywords

`func`、`fun`、`var`、`export`、`extend` 等へ置き換える案。

文字数を抑えられる一方で、単に別言語由来の短縮語へ置き換えることになりやすく、概念を明示するという今回の目的に合わない。

採用しない。

### Use `implement` instead of `implementation`

```sobakasu
implement Foo {
}
```

短くできるが、`implement` は動詞であり、declaration block そのものの名称としては `implementation` の方が明確である。

Sobakasu の `implementation` は型の implementation block を表すため、名詞である `implementation` を採用する。

### Use `extension`

```sobakasu
extension Foo {
}
```

Swift 等に先例があり、既存型へ機能を追加する構文として自然である。

しかし Sobakasu の implementation block は単なる後付け extension ではなく、型の associated function、instance method、operator、external binding 等を定義する基本的な実装単位である。

そのため意味が狭すぎる。

### Use `instance`

Haskell 等では type class implementation に `instance` が使用される。

しかし、

```sobakasu
instance Foo {
}
```

では Foo が何の instance であるかが明確でなく、Sobakasu の inherent implementation を表す語として適切ではない。

将来 contract implementation を導入する場合でも、必要であれば `implementation` を拡張できるため、現時点では別の `instance` keyword を導入しない。

### Rename `use` to `import`

一般的な import syntax として理解しやすい。

しかし変更の必要性がなく、Sobakasu の `use` は import だけでなく alias や re-export にも関与する。

Rust と同じ語であることのみを理由として変更することは、この ADR の目的に反するため採用しない。

### Keep `lang`

短く、既存コードへの変更も少ない。

しかし `lang` は `language` の強い省略であり、実際に表している概念は language item である。

頻出構文でもないため、`language item` と明示する方を採用する。

## Rationale

Sobakasu の文法は、他言語からの影響を隠すことを目的としない。

プログラミング言語は相互に既存の構文、概念、用語から影響を受けるものであり、Sobakasu も Rust、C#、F#、Objective-C、Ruby、JavaScript、PHP、Lua、Pascal その他の言語から有用な設計を採用できる。

重要なのは由来ではなく、Sobakasu の言語モデルに対してその構文が適切かどうかである。

`function` は通常関数という declaration category を明示し、`on` や `receive` と対比できる。

`implementation` は型に属する実装ブロックであることを明確に表現する。語は長いが、implementation block は local variable declaration 等ほど高頻度ではなく、可読性を優先できる。

`mutable`、`public`、`module` は、それぞれの意味を直接表す一般的なプログラミング用語であり、既存言語に実用上の先例もある。

`language item` は compiler-specific な低頻度構文であるため、短さより意味の明示を優先する。

一方、`struct`、`enum`、`const`、`extern`、`ref` のように略語そのものが一般的な専門用語として定着しているものまで機械的に展開しない。

この方針によって、Sobakasu は特定言語の表面的な語彙セットを借りるのではなく、複数の言語設計を参考にしながら各機能について自身の semantics に適した語を選択する。

## Consequences

### Positive

- `function`、`on`、`receive` の違いが declaration の先頭から明確になる。
- source code から `public`、`mutable`、`module` の意味を直接読み取りやすくなる。
- implementation block の役割が `implementation` という名称で明示される。
- Sobakasu 自身の語彙体系を形成できる。
- 他言語からの影響を拒否するのではなく、必要な要素を意図的に選択するという設計方針が明確になる。
- 将来の keyword design について「どの言語に似ているか」ではなく「Sobakasu の概念を最も明瞭に表すか」という判断基準を持てる。
- alpha 段階で不要な compatibility syntax を抱えずに済む。
- `fn`、`impl`、`mut`、`pub`、`mod`、`lang` を将来 identifier として利用できる。

### Negative

- `implementation` は `impl` より大幅に長い。
- `function`、`mutable`、`public`、`module` も既存構文より入力文字数が増える。
- 既存の Sobakasu source、standard library、generated standard library、tests、documentation、syntax highlighting 等を一括して更新する必要がある。
- 旧 source syntax には compatibility がないため、変更後は既存コードをそのままコンパイルできない。
- `language item` は Parser 上で2つの連続した keyword を1つの declaration prefix として扱う必要がある。
