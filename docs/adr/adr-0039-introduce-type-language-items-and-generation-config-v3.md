# ADR-0039: Introduce type language items and generation config v3

## Status

Accepted

## Context

Sobakasu compiler の一部は、標準ライブラリの意味を source declaration の名前や CLR type identity から推測していた。nullable extern output projection は visible scope から `Maybe` という名前を探索し、network send は `VRC.Udon.Common.Interfaces.NetworkEventTarget` を CLR catalog から直接取得していた。このため import、prelude、rename と compiler semantic identity が混在し、標準ライブラリ上の型名を安全に変更できない。

ADR-0026 は当時の要件に基づいて `Maybe<T>` を lang item にしないと決定し、ADR-0032 も visible `Maybe<T>` を projection の前提としていた。ADR-0035 の generation configuration version 2 は責務を renames、prelude、maybe、excludes に固定している。自動生成型にも同じ semantic identity を付与するには、これらの決定を明示的に更新する必要がある。

## Decision

`lang "item"` を type declaration 専用 metadata として導入する。付与できる対象は `struct`、`enum`、external type binding の `impl` とし、function、method、const、state、event、receive、module、use および通常の `impl` には一般化しない。`lang` は keyword とし、Parser は optional な `LanguageItemSyntax` を対象 declaration に保持する。対象外 declaration と malformed metadata は Parser diagnostic とする。

Binder は canonical item 名を一か所に定義し、compilation / standard-library graph ごとの language item registry に item から `TypeSymbol` への対応を保持する。`ModuleBindingPhase` と `TypeDeclarationBindingPhase` の後、`CallableDeclarationBindingPhase` の前に独立した `LanguageItemBindingPhase` を実行する。aggregate は既存の syntax mapping を使い、external binding には最小の `ImplDeclarationSyntax` から `TypeSymbol` への mapping を追加する。unknown item、duplicate item、external binding でない `impl` を診断する。metadata は use、prelude、visibility による通常の名前探索へ参加しない。

今回の canonical item は `maybe` と `network_event_target` とする。extern Maybe projection は registry の `maybe` type を取得した後、generic definition、1 type parameter、enum、unit variant、single-value tuple variant という既存 shape validation を維持する。network send は registry の `network_event_target` type を使い、contextual target、explicit target、custom expression の既存挙動を維持する。`SendCustomNetworkEvent` の物理 Udon ABI signature は language item にせず、backend に metadata 文字列を流さない。

Standard Library Generator configuration は version 3 とし、top-level に次を追加する。

```json
"lang": [
  {
    "from": "Example.Type",
    "item": "example_type"
  }
]
```

`from` は canonical CLR type identity に exact match し、`item` は生成される Sobakasu type declaration の直前へ出力する。null rule、空の `from` / `item`、duplicate `from` / `item`、stale `from`、type declaration を生成しない static-class module や excluded / skipped type を configuration error とする。language item は namespace facade や `pub use` へ付けない。

手書き `Maybe<T>` と `NetworkEventTarget` に metadata を直接記述する。`NetworkEventTarget` は引き続き generator production config の excludes に残し、enum generation と自動生成への移行は行わない。production config の `lang` は空とし、generator 機能は独立した test config で検証する。

この決定は ADR-0026 の「`Maybe<T>` は lang item ではない」という部分と、ADR-0032 の visible name `Maybe` への依存を置き換える。通常 source code が `Maybe<T>` を名前解決する際の import / visibility 規則、raw extern escape hatch、generic enum layout は変更しない。ADR-0035 の version 2 schema は version 3 に更新し、それ以外の generation policy と report invariant は維持する。

## Alternatives

1. `Maybe` と CLR `NetworkEventTarget` のハードコードを維持する案は、declaration rename を semantic change にしてしまうため採用しない。
2. language item を use / prelude 経由で探索する案は、compiler metadata が source visibility に左右されるため採用しない。
3. 汎用 attribute system として function や module に一般化する案は、今回必要な type identity を越えて Parser / Binder の責務を拡大するため採用しない。
4. `NetworkEventTarget` を同時に自動生成する案は、generator が enum declaration を生成できないため採用しない。
5. version 2 schema に `lang` を追加する案は、ADR-0035 が固定した schema 責務を黙って変更するため採用しない。

## Rationale

type symbol が確定した後の専用 Binder phase で登録すれば、名前探索と semantic identity を分離しつつ、aggregate shape を consumer が検証できる。Generator は CLR discovery と生成 declaration の対応を既に所有するため、exact-match policy と renderer だけに `lang` を追加するのが責務上自然である。意味解決は Binder までで完了し、IR、Lowerer、UASM backend の既存境界を維持できる。

## Consequences

### Positive

* 標準ライブラリの type 名を変更しても canonical semantic identity を維持できる。
* Maybe projection と network send が source visibility や CLR type lookup に依存しない。
* duplicate、typo、stale generator rule を早期に診断できる。
* 手書き declaration と将来の自動生成 declaration が同じ metadata mechanism を使用できる。
* IR と backend に language item 固有処理を追加しない。

### Negative

* 標準ライブラリ graph は必要な canonical item を一意に提供しなければならない。
* Generator configuration version 2 は version 3 へ移行が必要になる。
* item 固有の shape requirement は registry 登録だけでは完結せず、各 consumer の validation が必要になる。
* enum generator が実装されるまで `NetworkEventTarget` は手書き・exclude のまま残る。
