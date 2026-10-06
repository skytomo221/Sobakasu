---
title: 言語リファレンス
description: 現行Sobakasu言語の書き方と動作をまとめたリファレンスです。
sidebar:
  order: 1
---

このリファレンスは、現在の Sobakasu で**何が書けるか**、**コンパイル時にどのような規則があるか**、**実行するとどのように動くか**を調べるための文書です。

Sobakasu を初めて知る人でも読めるように、言語固有の概念はできるだけ最初に説明します。分からない用語がある場合は [用語集](./terminology/) も参照してください。

設計上の理由や、採用しなかった案については [ADR](../adr/) に分けています。

:::note[開発中の言語です]
Sobakasu は現在も開発中です。このリファレンスでは、現在の実装とテストで使用できる書き方を現行仕様として説明します。過去の ADR や README に以前の書き方が残っている場合は、このリファレンスの現行構文を優先してください。
:::

## 最小構成

状態変数は `state` ブロックに、VRChat のイベント処理は `behavior` ブロックに記述します。

```sobakasu
state {
  count: i32 = 0;
}

function increment(value: i32) -> i32 {
  value + 1
}

behavior {
  on interact(state) {
    state.count = increment(state.count);
  }
}
```

**状態変数**は、イベント処理が終わった後も値を保持する変数です。上の例では、`interact` イベントが発生するたびに `count` が 1 増えます。

`state.count` の `.` は値が持つメンバーへのアクセスです。一方、`foo::Bar` の `::` はモジュールや型などを含む**修飾名**に使います。

## 章構成

- [用語集](./terminology/): リファレンスで使う主な用語
- [字句構造](./lexical-structure/): コメント、識別子、リテラル、キーワード
- [名前とモジュール](./names-and-modules/): `module`、`use`、修飾名、公開と再公開
- [宣言](./declarations/): 宣言の種類と記述できる場所
- [変数・定数・状態](./variables-and-state/): `let`、`const`、`state`、`public`、`sync`
- [関数と implementation](./functions-and-impls/): `function`、メソッド、関連関数
- [型システム](./type-system/): 組み込み型、型注釈、`Maybe<T>`
- [複合型](./aggregate-types/): 配列、タプル、`struct`、`enum`
- [ジェネリクス](./generics/): 型パラメーターと単相化
- [式](./expressions/): 値の計算、呼び出し、アクセス、初期化
- [演算子](./operators/): 演算子、優先順位、短絡評価
- [制御フロー](./control-flow/): `if`、`while`、`loop`、`break` など
- [パターン](./patterns/): `let` による分解と `match`
- [behavior とイベント](./behaviors-and-events/): Udon イベントと状態変数へのアクセス
- [ネットワーク](./networking/): `receive` と `send`
- [extern](./extern/): Unity・VRChat などの外部 API との接続
- [構文一覧](./grammar/): 主要構文の簡易文法

## 現在使うキーワード

現在は `function`、`implementation`、`mutable`、`public`、`module`、`language item` を使用します。以前使われていた `fn`、`impl`、`mut`、`pub`、`mod`、`lang` は現行構文ではありません。

`static` も現在の関数・メソッド宣言では使用しません。型に関連付ける関数は `implementation` の中へ記述します。

## 実装を見る

言語処理系そのものを確認したい場合は、次のソースコードを参照できます。

- [Parser](https://github.com/skytomo221/Sobakasu/tree/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Parser)
- [Binder](https://github.com/skytomo221/Sobakasu/tree/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Binder)
- [Compiler tests](https://github.com/skytomo221/Sobakasu/tree/main/Packages/com.skytomo221.sobakasu/Tests/Editor/Compiler)
- [ADR-0055: Sobakasu Source Keyword Vocabulary](../adr/adr-0055-sobakasu-source-keyword-vocabulary/)
