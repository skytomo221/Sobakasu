# ADR-0024: Custom Network Event の受信宣言と送信構文

## Context

Sobakasu には Udon の組み込みイベントを宣言する `on` と、インライン展開される通常関数 `fn` がある。一方、VRChat SDK 3.10.4 の引数付き Custom Network Event は、通常の Udon event export に加えて `NetworkCallable` の entrypoint metadata、0 から 8 個の物理引数、`NetworkCalling.SendCustomNetworkEvent` ABI を必要とする。

通常関数を暗黙に network-callable にすると、ローカル呼び出しとネットワーク送信の意味、overload、公開 entrypoint、レート制限が混在する。また Sobakasu の struct は Udon heap 上では leaf ごとの複数 slot に flatten されるため、ソース上の論理引数数と SDK ABI の物理引数数は一致しないことがある。

## Decision

### 構文と名前解決

トップレベルに `receive` 宣言を追加する。

```sobakasu
receive damage(amount: i32) {
  // body
}

receive ping {
  // zero parameters
}
```

`receive` は常に `()` を返す。`-> Type` は構文診断とし、同名 receiver の重複と overload は認めない。`receive` は `fn` と異なる symbol kind とし、通常の call syntax からは解決しない。`on` は引き続き installed event catalog だけを対象とする。

statement として次の送信構文を追加する。

```sobakasu
send damage(10) to others;
send ping to NetworkEventTarget.All;
send damage(value) to target;
```

Custom Network Event の parameter/argument list は ADR-0015 のゼロ引数 callable の表記規則と整合させる。`receive` と `send` はともにゼロ引数の場合だけ `()` を省略でき、括弧付きの `receive ping() {}` と `send ping() to all;` も後方互換のため引き続き有効とする。parameter/argument が 1 個以上ある場合は括弧を必須とし、`receive ping {}` と `send ping to all;` を canonical / preferred style とする。

`send` は `receive` symbol だけを解決する。`fn` は送信できない。`all`、`others`、`owner`、`self` は `to` の直後に単独で現れた場合だけ `NetworkEventTarget` の値として解釈し、グローバル予約語にはしない。標準ライブラリは同じ型の通常値として `NetworkEventTarget.All`、`.Others`、`.Owner`、`.Self` を公開する。

### 型検査と物理 ABI

Binder は generic monomorphization 後の concrete type に対して network layout を確定する。非 aggregate 引数は 1 leaf、struct は既存の `AggregateLayout` と同じ宣言順・再帰順で leaf に flatten する。payload enum と aggregate array は、安全な相互運用 ABI を定義できるまで拒否する。

各物理 leaf は installed SDK の同期可能型集合と同じ network serialization catalog に含まれなければならない。物理 leaf の総数は SDK overload に合わせて 0 から 8 とする。これらの検査と物理 storage 名の決定は Binder の責務であり、IR lowerer や UASM backend で型解決をやり直さない。

送信時は論理引数をソース順にそれぞれ 1 回だけ評価し、その後に target を 1 回評価する。確定済み layout に従って leaf を並べ、現在の `IUdonEventReceiver`、target、receiver export 名、0 から 8 個の payload を、installed SDK の次の family へ渡す。

```text
VRC.SDK3.UdonNetworkCalling.NetworkCalling.SendCustomNetworkEvent(
  IUdonEventReceiver,
  NetworkEventTarget,
  string,
  object ...)
```

### entrypoint metadata と Unity Editor

各 `receive` はソース名を Udon export 名とする。物理 parameter slot 名は compiler が衝突しない内部名として生成し、同じ名前と runtime type を `NetworkCallingEntrypointMetadata` に保存する。metadata は `SobakasuProgramAsset` 自身に serialize し、通常 compile、commit、`RefreshProgram` のいずれでも `SerializedUdonProgramAsset.StoreProgram` に再供給する。

既定の `NetworkCallableAttribute` rate は installed SDK と同じ 5 events/second とする。rate 指定構文は本 ADR の範囲外とする。

### SDK 制約

引数付き network event を実行する UdonBehaviour は Behaviour Sync Mode が `None` であってはならない。ただし compiler core は scene 上の UdonBehaviour を所有せず、安全に判定できないため、本変更では compile-time error にしない。将来、SobakasuProgramAsset と component の対応を確実に取得できる Editor validation で診断する。

VRChat の network event payload には 16 KB の実行時上限がある。値の実サイズは実行時まで確定しないため、compiler に不正確なサイズ検査は追加せず、言語・Editor ドキュメント上の制約として扱う。

## Alternatives

- `fn` に attribute を付けて network-callable にする案は、通常 call と send の解決規則および公開 ABI を混在させるため採用しない。
- `on CustomEvent` を再利用する案は、installed event catalog による組み込みイベント検証とユーザー定義名を混在させるため採用しない。
- backend で aggregate を flatten する案は、物理引数上限や型診断を遅延させ、Parser/Binder/IR/UASM の責務分離に反するため採用しない。
- legacy の引数なし `SendCustomNetworkEvent` だけを使う案は、型付き parameter metadata と引数付き ABI を利用できないため採用しない。

## Rationale

明示的な `receive` と `send` により、ローカル関数、組み込み Udon event、network-callable entrypoint の意味が構文と symbol の両方で分離される。Binder が logical-to-physical layout と exact SDK operation を固定することで、既存 ADR-0003、ADR-0010、ADR-0011、ADR-0021、ADR-0022 の責務境界と aggregate/generic 方針を維持できる。

## Consequences

### Positive

- 送信可能な対象が宣言から明確になり、`fn` の既存 semantics を変更しない。
- struct parameter と 8 引数制限を backend 前に型付き診断できる。
- SDK metadata と UASM parameter slot が同じ compiler-owned layout から生成される。
- asset refresh 後も引数付き Custom Network Event の metadata が失われない。

### Negative

- payload enum と aggregate array は当面使用できない。
- Behaviour Sync Mode と 16 KB payload 上限は compiler core だけでは保証できない。
- rate を変更する言語構文は別の設計判断が必要になる。
