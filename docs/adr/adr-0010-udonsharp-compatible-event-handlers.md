# ADR-0010: UdonSharp互換イベントハンドラカタログの採用

## Status

Proposed

## Context

Sobakasu は Udon-first の言語であり、C# 互換そのものを目的にしない。一方で、Udon の実行モデルはイベント駆動であり、UdonSharp 利用者が違和感なく移行できるイベント宣言は重要である。

既存の Sobakasu は top-level `on` イベント宣言を持つが、実装上は事実上 `Interact` だけを扱う前提だった。今後は UdonSharp Events ドキュメントに列挙されている Udon Events / Unity Events を Sobakasu の top-level `on` 宣言として扱う。

この決定は既存 ADR と次のように整合する。

* ADR-0002: Sobakasu は Udon-first であり、UdonSharp 並みの使いやすさを設計制約にする
* ADR-0003: frontend / Binder / IR / backend の責務分離を維持する
* ADR-0005: イベント引数で使う primitive 型は Rust 風組み込み型名で扱う
* ADR-0008: Unity / VRC / System 由来の型や extern を compile-time に解決する方針と整合させる
* ADR-0009: Binder で意味解決を確定し、backend は解決済み情報の emission に専念する

## Decision

Sobakasu は UdonSharp 互換イベントを `EventCatalog` に集約して定義する。Parser は任意識別子の `on` 宣言を構文として受理するだけに留め、Binder が `EventCatalog` を参照してイベント名、戻り値、引数型、引数数、サポート状態を検証する。

採用する source syntax は次の通りとする。

```sobakasu
on Interact() {
  Debug.Log("Hello, world!");
}

on OnPlayerJoined(player: VRCPlayerApi) {
  Debug.Log(player.displayName);
}

on InputJump(value: bool, args: VRC.Udon.Common.UdonInputEventArgs) {
  Debug.Log("jump");
}

on OnOwnershipRequest(requester: VRCPlayerApi, newOwner: VRCPlayerApi): bool {
  return true;
}
```

決定事項は次の通りとする。

* イベント宣言は top-level member とする
* 同一イベントの重複宣言は禁止する
* イベント名は case-sensitive とする
* UdonSharp / Udon が認識するイベント名に一致するものだけをイベントとして許可する
* 未知のイベント名は Binder 診断にする
* イベント署名は Binder で検証する
* `u0` 戻り値イベントでは戻り値注釈を省略でき、省略時は `: u0` と同等に扱う
* 非 `u0` イベントでは戻り値注釈を必須にする
* `OnOwnershipRequest` は `bool` 戻り値イベントとして扱い、`return true;` / `return false;` を許可する
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
  UdonName,
  Category,
  ReturnType,
  Parameters,
  Requirement,
  SupportLevel)
```

Udon Events は署名付き `Supported` として登録する。Unity Events は catalog に含め、`Start`、`Update`、`FixedUpdate`、`LateUpdate`、`OnEnable`、`OnDisable`、`OnDestroy` のように SDK runtime の no-arg entry point が明確なものだけ v1 で `Supported` とし、その他は `PendingSignature` にする。

component requirement は compiler core ではエラーにしない。`OnDrop` などの `VRC_Pickup` 必須イベント、`OnStationEntered` などの `VRC_Station` 必須イベントは warning / info 診断に留める。Unity シーン内のコンポーネント検査は Unity Editor 統合側の責務とする。

## Alternatives

1. `Interact` だけを特別扱いし続ける
   短期実装は簡単だが、UdonSharp 並みのイベント駆動モデルに到達できず、イベント追加のたびに ad-hoc 実装が増えるため却下する。
2. Parser でイベント名を全部固定キーワード化する
   構文エラーの検出は早いが、SDK のイベント追加に弱く、`SyntaxKind` が不要に膨らむため却下する。
3. UdonSharp の C# メソッド構文をそのまま採用する
   UdonSharp 利用者には馴染みがあるが、Sobakasu は C# 互換を目的にしないため却下する。
4. イベント名を文字列で登録する
   静的診断と補完に不利であり、通常のイベント宣言として読みにくいため却下する。
5. 括弧なし `on Interact { ... }` を採用する
   パラメータなしイベントだけなら簡潔だが、引数ありイベントとの統一性が弱くなるため v1 では採用しない。将来 sugar として再検討可能とする。

## Rationale

Sobakasu は Udon-first であり、イベント駆動は Udon の基本実行モデルである。UdonSharp 互換イベント名を扱えることは移行容易性に直結する。

Parser ではなく Binder にイベント意味解決を置くことで、ADR-0003 の frontend / Binder / IR / backend 責務分離と整合する。backend は解決済み event symbol の emission に専念でき、`Interact` のような個別イベント特例を持たずに済む。

`EventCatalog` により SDK 追従、診断、補完、テストデータを一箇所に集約できる。`OnOwnershipRequest` のような戻り値ありイベントを最初から設計に含めることで、`void` 前提のイベントモデルに閉じない。

## Consequences

### Positive

* `Interact` 以外の UdonSharp 互換イベントを段階的に扱える
* イベント名、引数、戻り値の診断が Binder で可能になる
* `EventCatalog` が補完、ドキュメント生成、テストデータに再利用できる
* Unity / VRChat SDK 由来のイベント差分に追従しやすくなる

### Negative

* `EventCatalog` の保守コストが発生する
* Unity Events の一部は有効署名の確認が必要で、初期実装が大きくなる
* 引数付きイベントと戻り値ありイベントにより Binder / IR / backend の設計面積が増える
* SDK 更新時にカタログとテストを更新する必要がある
