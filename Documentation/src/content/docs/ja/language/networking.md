---
title: ネットワーク
description: Custom Network Eventの受信、送信、送信先を説明します。
sidebar:
  order: 16
---

VRChat の **Custom Network Event** は、ネットワークを通して別のプレイヤー側の Udon プログラムへ処理を実行させる仕組みです。

Sobakasu では受信する処理を `receive`、送信を修飾した呼び出しと `to` で表します。

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
send behavior::ping() to all;
send behavior::set_value(42) to owner;
```

基本的な形は次のとおりです。

```text
send behavior::受信処理名(引数...) to 送信先;
send state.受信処理名(引数...) to 送信先;
```

`send` には通常の behavior function 呼び出しと同じ修飾が必要です。状態に依存しない受信処理には `behavior::名前(...)` を使います。状態に依存する受信処理には `state.名前(...)` または `behavior::名前(state, ...)` を使います。後者の `state` は状態へのアクセス権を示すもので、ネットワークで送る引数には含まれません。

受信処理の呼び出しには括弧が必要です。`send ping to all;` と `send ping() to all;` はどちらも使用できません。前者には呼び出しと括弧がなく、後者には `behavior::` または `state.` の修飾がありません。

送信時の引数の個数や型は、対応する `receive` の宣言と一致している必要があります。一致しない場合はコンパイルエラーになります。

## 送信先

`to` の後ろには送信先を指定します。よく使う送信先として次の名前があります。

- `all` — 対象となる全員
- `others` — 自分以外
- `owner` — オーナー
- `self` — 自分自身

```sobakasu
send behavior::ping() to all;
```

これらの名前は `to` の後ろで送信先として使われますが、言語全体の予約語ではありません。そのため、他の場所では通常の識別子として使えます。

```sobakasu
let all = 10;
send behavior::ping() to all;
```

`NetworkEventTarget` 型の値を持っている場合は、その式を送信先として指定することもできます。

```sobakasu
send behavior::ping() to target;
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

送信時も同じ規則を使います。次の2つは、どちらも `increment` に `amount` だけを送ります。

```sobakasu
behavior {
  on interact(state) {
    send state.increment(1) to all;
    send behavior::increment(state, 2) to others;
  }
}
```

`state` capability を持たないイベントから状態依存の受信処理を送ることはできません。

## 実装を見る

- [StatementParser.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Parser/StatementParser.cs)
- [NetworkSendBinder.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Binder/Statements/NetworkSendBinder.cs)
- [NetworkEventTests.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Tests/Editor/Compiler/Events/NetworkEventTests.cs)
- [ADR-0024: Custom Network Event Receivers and Send Syntax](/Sobakasu/ja/adr/adr-0024-custom-network-event-receivers-and-send-syntax/)
- [ADR-0057: Behavior Callable Qualification and Optional State Capability](/Sobakasu/ja/adr/adr-0057-behavior-callable-qualification-and-optional-state-capability/)
