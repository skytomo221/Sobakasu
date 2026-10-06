---
title: ネットワーク
description: Custom Network Eventの受信、送信、送信先を説明します。
sidebar:
  order: 16
---

VRChat の **Custom Network Event** は、ネットワークを通して別のプレイヤー側の Udon プログラムへ処理を実行させる仕組みです。

Sobakasu では受信する処理を `receive`、送信を `send ... to ...;` で表します。

## `receive`

ネットワークイベントの受信処理は `behavior` の中に宣言します。

```sobakasu
behavior {
  receive ping {
    log("ping");
  }
}
```

値を受け取る場合は引数を宣言します。

```sobakasu
behavior {
  receive set_value(value: i32) {
    log(value);
  }
}
```

引数がない場合は、`receive ping { ... }` と `receive ping() { ... }` のどちらも使用できます。引数がある場合は括弧が必要です。

`receive` は値を返す処理ではないため、戻り値型は指定できません。

## `send`

受信処理をネットワークへ送るには `send` を使います。

```sobakasu
send ping to all;
send set_value(42) to owner;
```

基本的な形は次のとおりです。

```text
send 受信処理名(引数...) to 送信先;
```

引数がない場合だけ `()` を省略できます。

送信時の引数の個数や型は、対応する `receive` の宣言と一致している必要があります。一致しない場合はコンパイルエラーになります。

## 送信先

`to` の後ろには送信先を指定します。よく使う送信先として次の名前があります。

- `all` — 対象となる全員
- `others` — 自分以外
- `owner` — オーナー
- `self` — 自分自身

```sobakasu
send ping to all;
```

これらの名前は `to` の後ろで送信先として使われますが、言語全体の予約語ではありません。そのため、他の場所では通常の識別子として使えます。

```sobakasu
let all = 10;
send ping to all;
```

`NetworkEventTarget` 型の値を持っている場合は、その式を送信先として指定することもできます。

```sobakasu
send ping to target;
```

## `send` が呼び出すもの

`send` は `receive` で宣言したネットワーク受信処理を指定します。同じ名前の通常の `function` があっても、それをネットワークイベントとして送信するわけではありません。

## `public receive`

`receive` に `public` を付けると、Udon 側から公開された受信処理として扱われます。

```sobakasu
behavior {
  public receive refresh {
    // ...
  }
}
```

## 状態変数を使う受信処理

受信処理から状態変数を使う場合は、引数一覧の先頭に `state` と書きます。

```sobakasu
behavior {
  receive increment(state, amount: i32) {
    state.count += amount;
  }
}
```

この `state` はネットワークで送受信される引数には含まれません。状態変数へアクセスすることをソース上で明示するためのものです。

## 実装を見る

- [StatementParser.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Parser/StatementParser.cs)
- [NetworkSendBinder.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Binder/Statements/NetworkSendBinder.cs)
- [NetworkEventTests.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Tests/Editor/Compiler/Events/NetworkEventTests.cs)
- [ADR-0024: Custom Network Event Receivers and Send Syntax](/Sobakasu/ja/adr/adr-0024-custom-network-event-receivers-and-send-syntax/)
