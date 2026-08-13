# ADR-0010: UdonSharp互換イベントハンドラカタログの採用

## Status

Proposed

## Context

Sobakasu は Udon-first の言語であり、C# 互換そのものを目的にしない。一方で、Udon の実行モデルはイベント駆動であり、UdonSharp 利用者が違和感なく移行できるイベント宣言は重要である。

既存の Sobakasu は top-level `on` イベント宣言を持つが、実装上は事実上 `Interact` だけを扱う前提だった。今後は UdonSharp Events ドキュメントに列挙されている Udon Events / Unity Events と同じイベント集合および署名を Sobakasu の top-level `on` 宣言として扱う。ただし、Sobakasu は C# の source spelling をそのまま公開せず、言語自身の命名規則を採用する。

この決定は既存 ADR と次のように整合する。

* ADR-0002: Sobakasu は Udon-first であり、UdonSharp 並みの使いやすさを設計制約にする
* ADR-0003: frontend / Binder / IR / backend の責務分離を維持する
* ADR-0005: イベント引数で使う primitive 型は Rust 風組み込み型名で扱う
* ADR-0008: Unity / VRC / System 由来の型や extern を compile-time に解決する方針と整合させる
* ADR-0009: Binder で意味解決を確定し、backend は解決済み情報の emission に専念する

## Decision

Sobakasu は UdonSharp / Udon 互換イベントカタログを `EventCatalog` に集約して定義する。Parser は任意識別子の `on` 宣言を構文として受理するだけに留め、Binder が `EventCatalog` を参照してイベント名、戻り値、引数型、引数数、サポート状態を検証する。

組み込みイベントの source-level name は `lower_snake_case` とする。Udon / Unity の canonical event name が先頭に `On` を持つ場合、その `On` は `on` 構文と重複するため source-level name から除去する。先頭以外に現れる `On` は意味のある語として保持する。

```text
Udon / Unity canonical name  Sobakasu source name
Start                        start
PostLateUpdate               post_late_update
OnPlayerJoined               player_joined
OnOwnershipRequest           ownership_request
OnEnable                     enable
OnVideoStart                 video_start
MidiNoteOn                   midi_note_on
```

イベント名の lookup は case-sensitive な完全一致とし、PascalCase からの入力時正規化や旧名 alias は設けない。したがって `start`、`interact`、`player_joined` は有効だが、`Start`、`Interact`、`OnPlayerJoined`、`PlayerJoined` は未知イベントとして拒否する。

採用する source syntax は次の通りとする。

```sobakasu
on interact() {
  Debug.Log("Hello, world!");
}

on player_joined(player: VRCPlayerApi) {
  Debug.Log(player.displayName);
}

on input_jump(value: bool, args: VRC.Udon.Common.UdonInputEventArgs) {
  Debug.Log("jump");
}

on ownership_request(requester: VRCPlayerApi, newOwner: VRCPlayerApi): bool {
  return true;
}
```

後続の ADR-0015 により、引数 0 個のイベントでは `on interact { ... }` のように `()` を省略できる。`on interact() { ... }` も引き続き有効であり、引数が 1 個以上あるイベントでは括弧を必須とする。

決定事項は次の通りとする。

* イベント宣言は top-level member とする
* 同一イベントの重複宣言は禁止する
* イベント名は case-sensitive とする
* Sobakasu が `lower_snake_case` で定義した組み込みイベント名だけを許可し、`EventCatalog` で対応する Udon / Unity canonical event へ明示的に解決する
* 未知のイベント名は Binder 診断にする
* イベント署名は Binder で検証する
* `u0` 戻り値イベントでは戻り値注釈を省略でき、省略時は `: u0` と同等に扱う
* 非 `u0` イベントでは戻り値注釈を必須にする
* `ownership_request`（canonical event `OnOwnershipRequest`）は `bool` 戻り値イベントとして扱い、`return true;` / `return false;` を許可する
* backend は Binder が確定した event symbol の Udon entry point / exported method を出力する
* イベント名や署名の解決を backend の ad-hoc 特例にしない
* イベント一覧は単発の switch 文ではなく `EventCatalog` に集約する
* Unity Events のうち正確なシグネチャ未確認のものは `PendingSignature` とし、無条件にコンパイル成功させない

型名は Sobakasu の組み込み型名に合わせる。

```text
void -> u0
bool -> bool
int -> i32
float -> f32
```

`EventCatalog` は少なくとも次を管理する。

```csharp
EventDefinition(
  SourceName,
  CanonicalName,
  UdonName,
  Category,
  ReturnType,
  Parameters,
  Requirement,
  SupportLevel)
```

`SourceName` は Sobakasu source で使用する正式な `lower_snake_case` 名、`CanonicalName` は Udon / Unity 側のイベント名、`UdonName` は実際の Udon entry point として明確に区別する。event entry point、parameter storage、return value storage、exported symbol は canonical event を基準に構築し、source spelling の変更によって Udon ABI を変更しない。例えば `player_joined` は canonical event `OnPlayerJoined` と Udon entry point `_onPlayerJoined` に解決し、その引数 storage は `onPlayerJoinedPlayer` のままとする。

Udon Events は署名付き `Supported` として登録する。Unity Events は catalog に含め、canonical event が `Start`、`Update`、`FixedUpdate`、`LateUpdate`、`OnEnable`、`OnDisable`、`OnDestroy` のように SDK runtime の no-arg entry point が明確なものだけ v1 で `Supported` とし、その他は `PendingSignature` にする。`PendingSignature` の source-level name にも同じ命名規則を適用する。

component requirement は compiler core ではエラーにしない。`drop`（canonical event `OnDrop`）などの `VRC_Pickup` 必須イベント、`station_entered`（canonical event `OnStationEntered`）などの `VRC_Station` 必須イベントは warning / info 診断に留める。Unity シーン内のコンポーネント検査は Unity Editor 統合側の責務とする。

## Alternatives

1. `Interact` だけを特別扱いし続ける
   短期実装は簡単だが、UdonSharp 並みのイベント駆動モデルに到達できず、イベント追加のたびに ad-hoc 実装が増えるため却下する。
2. Parser でイベント名を全部固定キーワード化する
   構文エラーの検出は早いが、SDK のイベント追加に弱く、`SyntaxKind` が不要に膨らむため却下する。
3. UdonSharp の C# メソッド構文をそのまま採用する
   UdonSharp 利用者には馴染みがあるが、Sobakasu は C# 互換を目的にしないため却下する。
4. イベント名を文字列で登録する
   静的診断と補完に不利であり、通常のイベント宣言として読みにくいため却下する。
5. 括弧なし `on interact { ... }` を採用する
   本 ADR では v1 に採用しなかったが、後続の ADR-0015 がゼロ引数イベントに限ってこの判断を更新した。引数付きイベントの括弧は必須のままである。
6. UdonSharp / C# と同じ PascalCase source name を採用する
   イベントだけが Sobakasu の通常の関数と異なる命名規則を露出し、`on OnPlayerJoined` のように `on` と `On` が重複するため却下する。イベント集合と署名の互換性は catalog の canonical event mapping で維持する。
7. PascalCase と `lower_snake_case` の両方を alias として受理する
   正式な source spelling が曖昧になり、case-sensitive な名前解決の一貫性も損なうため却下する。

## Rationale

Sobakasu は Udon-first であり、イベント駆動は Udon の基本実行モデルである。UdonSharp / Udon と互換なイベント集合および署名を扱えることは移行容易性に直結する一方、source-level spelling は Sobakasu の通常の関数と同じ `lower_snake_case` に揃える方が言語として一貫する。

Parser ではなく Binder にイベント意味解決を置くことで、ADR-0003 の frontend / Binder / IR / backend 責務分離と整合する。backend は解決済み event symbol の emission に専念でき、`interact` のような個別イベント特例や名前変換を持たずに済む。

`EventCatalog` により source name、canonical event、Udon ABI、SDK 追従、診断、補完、テストデータを一箇所に集約できる。`ownership_request` / `OnOwnershipRequest` のような戻り値ありイベントを最初から設計に含めることで、`void` 前提のイベントモデルに閉じない。

## Consequences

### Positive

* `interact` 以外の UdonSharp / Udon 互換イベントを段階的に扱える
* 組み込みイベント名が通常の Sobakasu 関数と同じ `lower_snake_case` に統一される
* source name を変更しても既存の Udon entry point と storage ABI を維持できる
* イベント名、引数、戻り値の診断が Binder で可能になる
* `EventCatalog` が補完、ドキュメント生成、テストデータに再利用できる
* Unity / VRChat SDK 由来のイベント差分に追従しやすくなる

### Negative

* `EventCatalog` の保守コストが発生する
* Sobakasu source と Udon / Unity canonical event の名称対応を保守する必要がある
* 旧 PascalCase source spelling との後方互換性はない
* Unity Events の一部は有効署名の確認が必要で、初期実装が大きくなる
* 引数付きイベントと戻り値ありイベントにより Binder / IR / backend の設計面積が増える
* SDK 更新時にカタログとテストを更新する必要がある
