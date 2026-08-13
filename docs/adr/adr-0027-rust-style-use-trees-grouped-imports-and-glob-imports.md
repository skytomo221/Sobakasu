# ADR-0027: Rust-style Use Trees, Grouped Imports, and Glob Imports

## Status

Accepted

## Context

ADR-0018は、階層モジュール、`pub use`による再エクスポート、暗黙Prelude、canonical public path、module visibility、`.`区切りのパスを定義した。一方で同ADRは、grouped importとglob importを導入せず、単純な`use path [as alias];`だけを扱うと決定していた。この制約では、Preludeや大きな公開APIを構成する際に同じprefixを持つ`pub use`を繰り返す必要がある。

標準ライブラリには通常のgeneric payload enumである`Maybe<T>`があり、型とvariantをPreludeから公開する場合、従来は型だけを再エクスポートして`Maybe.Just`、`Maybe.Nothing`と修飾する必要があった。`Maybe`や将来の`Option`、`Result`だけを特別扱いせず、module、declaration、enum variantへ共通して適用できるimport treeが必要である。

## Decision

### 再帰的なuse tree

`UseDirectiveSyntax`は単一のpathとaliasを直接保持する形から、再帰的な`UseTreeSyntax`を保持する形へ変更する。groupはbrace、各item、commaを保持し、各leafはpath、`self`、glob、任意のaliasとsource spanを保持する。Parserはsource textを書き換えず、malformedなgroupから構文回復する。

Sobakasuでは既存どおり`.`を名前空間区切りに使用し、`::`は導入しない。

```sobakasu
use foo.Bar;
use foo.Bar as Baz;
use foo.{Bar, Baz};
use foo.{bar.{A, B}, C};
use foo.{self, Bar};
use foo.*;
```

groupは再帰的にnestでき、末尾commaを許可する。group内のglobも許可し、次を同じ一般則で処理する。

```sobakasu
use foo.{bar.*, Baz,};
use foo.{*};
```

aliasはimportされるleafだけに適用する。既存のmodule aliasと同じ意味になるため、`self as Alias`を許可する。

```sobakasu
use foo.{Bar as Baz, self as f};
```

`self`は現在のuse tree prefixそのものを表す。したがって、`use foo.bar.{self, A};`は`use foo.bar; use foo.bar.A;`と同じ意味になる。globはprefixが公開するすべてのaccessible exportを表す。

### 意味処理

Module Resolverはuse treeをsource textへ展開せず、ASTを走査して通常のimport requestの列へ平坦化する。各requestについて規約に従う最長のmodule prefixを読み込み、残りのdeclaration pathをBinderへ渡す。これにより`module.Type.Variant`もmodule systemを複製せず表現できる。

Binderは既存の`ModuleSymbol`、public declaration index、visibility、再エクスポート、canonical public path、Preludeを使って各pathを解決する。pathの途中がpublic enum typeなら、その通常の`EnumVariantSymbol`を解決できる。globの対象はmoduleまたはenum typeとし、moduleではpublic exportだけ、enumではそのvariantだけを列挙する。private declaration、private module、inaccessible exportをglobから漏らさない。

`pub use`はgroup、nested tree、`self`、globのすべてへ同じように適用する。再エクスポートは元のsymbol identityを再利用し、canonical public pathの候補だけを追加する。IR LowererとUASM Assemblerへuse treeを渡さない。

### 名前衝突と優先順位

ADR-0018の名前検索順位を維持し、明示的なleaf importはglob importより優先する。したがって、次では`bar.Thing`を選択する。

```sobakasu
use foo.*;
use bar.Thing;
```

複数のglobが異なるsymbolを同じ名前で導入した場合は曖昧として診断し、source orderで一方を正常な解決結果として採用しない。同一symbolが複数経路から到達した場合はidentityを複製しない。現在のmodule内の宣言と子、明示alias、明示import、glob importはいずれもPreludeより優先される。

### Preludeとenum variant

通常のgeneric enumを次のように再エクスポートできる。

```sobakasu
pub use maybe.Maybe.{self, Nothing, Just};
pub use core.option.Option.{self, None, Some};
pub use core.result.Result.{self, Ok, Err};
```

importされたunit、tuple、struct variantは、修飾付きvariantと同じ`EnumVariantSymbol`およびexpected-type inferenceを使用して構築できる。`Maybe`、`Option`、`Result`や個々のvariant名をcompiler built-inにはしない。

この決定は、ADR-0018のgrouped import／glob importを導入しないという部分だけを更新する。階層モジュール、`pub use`、Prelude、canonical public path、module visibility、`.`区切り、外部APIとの`extern`境界は維持する。またADR-0026の「variantを自動的なbare shorthandとして注入しない」という決定を維持しつつ、通常の`use`またはPreludeの公開exportによって明示的に導入されたvariantはbare nameで利用できるようにする。

## Alternatives

### Grouped importだけを導入する

`use foo.{A, B};`だけを導入し、`self`、glob、nested treeを見送る案。構文拡張が中途半端になり、後からrecursive ASTを作り直す可能性が高いため採用しない。

### OptionやMaybeだけを特殊扱いする

`Some`／`None`や`Just`／`Nothing`をcompilerまたはPrelude専用処理で注入する案。一般化できず、`Result`やmodule APIに再利用できず、標準ライブラリとcompilerの境界を悪化させるため採用しない。

### Rustと同じ`::`を導入する

`use foo::{A, B};`とする案。Sobakasuは既存設計として`.`を名前空間区切りに使用しており、同じ意味の区切りを追加すると一貫性を失うため採用しない。

### 複数の単純useを書き続ける

`use foo.A; use foo.B; use foo.C;`だけを使う案。機能上は可能だが、Preludeや大きなAPIの再エクスポートで冗長になるため採用しない。

## Rationale

Rustで実績のあるuse-tree modelは、既存の`use`／`pub use`を自然に一般化できる。`Option.{self, None, Some}`のようなPrelude設計を簡潔にし、特定の標準型をcompilerへ組み込まずに済む。private implementation moduleを隠しながら必要なAPIだけを再構成しやすく、`.`区切りというSobakasuの設計も維持できる。

syntax treeを最初から再帰構造にし、意味処理では通常のimport requestへ平坦化することで、正確なsource spanとparser recoveryを保ちながら、既存のmodule graphとBinderの責務を再利用できる。

## Consequences

### Positive

* import／re-exportの記述量が減る。
* Preludeの構成を簡潔にできる。
* enum variantを通常の名前としてimport／re-exportできる。
* nested module APIを公開面へ再構成しやすい。
* Rustのuse treeを知る利用者が理解しやすい。
* declaration identity、visibility、canonical public path、IR／UASMの責務分離を維持できる。

### Negative

* Parserとsyntax treeが再帰構造になり複雑になる。
* globによる名前の曖昧性が増える。
* globを含む再エクスポートgraphには固定点処理が必要になる。
* malformed groupとleafごとの診断、source spanを維持する必要がある。
* bare enum variant patternは既存のpattern grammarが要求する`Enum.Variant`形式のままであり、この決定だけでは変更しない。
