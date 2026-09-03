# ADR-0043: Lazy Standard-Library Module and Re-export Resolution

## Status

Accepted

## Context

標準ライブラリの root module は、多数の private child module と、それらの宣言を公開する多数の `pub use` を持つ。従来の resolver は、module を読み込むと全 `mod` child と全 `use` target を再帰的に parse して module graph へ追加していた。このため `use unity;` や `use system;` のような broad import だけで、利用しない API module まで Binder の全 phase を通っていた。

ADR-0017 と ADR-0018 は到達可能な標準ライブラリだけを統合する責務と module/re-export semantics を定めたが、到達可能性を module 単位で eager に閉包する初期実装は、大規模な公開 API に対して十分でない。

## Decision

標準ライブラリ resolver は、module を読み込んだ時点ではその source だけを parse し、`mod` declaration と `pub use` を metadata として索引化する。`mod` は child の論理名、宣言 syntax、visibility を保持し、child source を直ちに読み込まない。`pub use` は公開名、target module、target declaration path、alias、glob、元の syntax を未解決 descriptor として保持し、target module を直ちに読み込まない。

Binder phase は動的な module graph を扱わない。Resolver は Binder を開始する前に、既に parse した syntax tree の module-qualified expression、qualified type、明示的 declaration import、および必要な Prelude 名を調べ、参照された child/re-export target だけを materialize する。この処理で追加された module についても同じ索引化と参照収集を行い、必要な dependency closure を固定してから既存 Binder pipeline へ渡す。

名前付き re-export は公開名が要求されたときだけ target を辿り、chain の終端までに必要な module を読み込む。解決済み descriptor と module は再利用する。`pub use target.*` は要求名が現れたときに target module とその名前に必要な export chain を materialize する。明示的な `use target.*` は既存 glob semantics により target の export 集合を必要とするため、その集合を materialize する。

Dependency edge と解決中の re-export path を記録し、import cycle と re-export cycle を診断する。Private child、`pub mod`、public re-export、alias、canonical public path の semantics は変更しない。

この決定は、ADR-0017 と ADR-0018 の module dependency を eager に再帰展開する実装方針を上記の範囲で更新する。Parser、Binder の phase machinery、IR Lowerer、UASM backend は module materialization を行わない。

## Alternatives

### Broad import を生成時に narrow import へ書き換える

利用者が broad module import を使える言語仕様を損ない、公開 API の増加に対する一般解にならないため採用しない。

### Binder の名前検索中に module を追加する

追加 module が既に終了した type/callable declaration phase を通らない状態を生む。全 Binder phase を動的 graph 対応へ変更する必要があるため採用しない。

### 全 module を parse して Binder だけ遅延する

不要な file I/O と parse を残し、module materialization 自体を利用箇所へ限定する要件を満たさないため採用しない。

## Rationale

Syntax index と Binder 前 closure の二段階に分けることで、module system の可視性と re-export semantics を維持しながら、未使用 API を既存 Binder workload から除外できる。既存の固定 graph 型 Binder pipeline を維持できるため、変更範囲も Resolver と import metadata に限定できる。

## Consequences

### Positive

* Broad import のコストは root module の parse/index と実際に参照した API dependency に限定される。
* 大量の `pub use` target と private child は、未使用なら parse も bind もされない。
* Re-export chain、alias、glob、Prelude に同じ一般的な規則を適用できる。
* Binder が処理する module graph は引き続き phase 開始前に固定される。

### Negative

* Resolver は syntax tree から module になり得る修飾参照と単純名を保守的に収集する必要がある。
* 明示的 glob import は export 集合を必要とするため、名前付き import より広い materialization を行う。
* 未使用 child 内だけに存在する dependency cycle は、その child が materialize されるまで診断されない。
