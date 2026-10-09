---
title: behavior とイベント
description: behavior、on、状態変数へのアクセス、Udonイベントの関係を説明します。
sidebar:
  order: 15
---

**`behavior` ブロック**は、VRChat から届くイベントやネットワークイベントを処理する部分をまとめるための宣言です。

ユーザー定義型にメソッドを追加する `implementation` とは役割が異なります。

## `behavior`

```sobakasu
behavior {
  on start {
    log("start");
  }
}
```

`behavior` の中には、次のものを宣言できます。

- `function` — `behavior` 内で使う関数
- `on` — VRChat/Udon のイベント処理
- `receive` — Custom Network Event の受信処理

`on` や `receive` を `behavior` の外へ直接書くことはできません。

## `on`

`on` は VRChat/Udon から届くイベントを処理します。

```sobakasu
behavior {
  on interact {
    log("interact");
  }
}
```

イベントに値が渡される場合は、引数として受け取れます。イベント名や引数は、VRChat/Udon が提供する対応するイベントと一致している必要があります。一致しない場合はコンパイルエラーになります。

イベント処理で状態変数を使わない場合は、`state` を書く必要はありません。`interact` も他のイベントと同じ規則です。

## 状態変数へアクセスするイベント

イベント処理から状態変数を読み書きする場合は、引数一覧の先頭に `state` と書きます。`state` を省略したイベント処理から状態変数を参照すると、コンパイルエラーになります。

```sobakasu
state {
  count: i32 = 0;
}

behavior {
  on interact(state) {
    state.count += 1;
  }
}
```

この `state` は VRChat から渡される通常の引数ではありません。そのイベント処理が状態変数を利用することを明示するものです。

## `behavior` 内の関数

`behavior` の中には、イベント処理から使う補助関数も宣言できます。

```sobakasu
behavior {
  function message() -> string {
    "hello"
  }

  on start {
    log(message());
  }
}
```

特定の型に対するメソッドは `behavior` ではなく `implementation` に宣言します。

## `receive`

VRChat の Custom Network Event を受け取る処理も `behavior` の中に宣言します。

```sobakasu
behavior {
  receive ping {
    log("ping");
  }
}
```

詳しくは [ネットワーク](../networking/) を参照してください。

## 実装を見る

- [DeclarationParser.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Parser/DeclarationParser.cs)
- [Event catalog](https://github.com/skytomo221/Sobakasu/tree/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Semantics/Events)
- [ADR-0054: Separate Type Implementation from Udon Script Behavior](/Sobakasu/ja/adr/adr-0054-separate-type-implementation-from-udon-script-behavior/)
