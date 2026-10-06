---
title: 宣言
description: Sobakasuで利用できる宣言と記述できる場所を説明します。
sidebar:
  order: 5
---

名前、型、関数などを新しく定義する記述を**宣言**と呼びます。Sobakasu では宣言の種類ごとに、記述できる場所が決まっています。

## 主なトップレベル宣言

ソースの最上位には、主に次の宣言を置けます。

```text
module ...;
use ...;
public use ...;

const ... = ...;
state { ... }

function ... { ... }
struct ... { ... }
enum ... { ... }
type ... = extern ...;
implementation ... { ... }
behavior { ... }
```

## `state` と `behavior`

イベント処理が終わった後も保持する値は、`state` ブロックの中へまとめて宣言します。

```sobakasu
state {
  count: i32 = 0;
  public label: string = field;
}
```

以前の `state count = 0;` のように、`state` を変数ごとに付ける書き方は現在使用できません。使用するとコンパイルエラーになります。

VRChat のイベント処理とネットワークイベントの受信処理は `behavior` ブロックへ記述します。

```sobakasu
behavior {
  on start {
    log("start");
  }

  receive ping {
    log("ping");
  }
}
```

`on` や `receive` を `behavior` の外へ直接書くことはできません。

## `public`

`public` は、その宣言を外部から利用できるようにするときに付けます。何に対して公開されるかは宣言の種類によって異なります。

- `public use` — 取り込んだ名前を別のモジュールにも再公開する
- `public const` / `public function` / `public type` など — モジュール外から利用できるようにする
- `state` 内の `public` — Unity の Inspector などから設定できる状態変数として公開する
- `public receive` — Udon から呼び出せるネットワーク受信処理として公開する

`public` を付けられない宣言に使用するとコンパイルエラーになります。

## `language item`

`language item` は、コンパイラが特別な役割を持つものとして認識する型や `implementation` を宣言するための仕組みです。主に Prelude や標準ライブラリの実装で使用します。

```sobakasu
language item "maybe"
enum Maybe<T> {
  Nothing,
  Just(T),
}
```

通常のアプリケーションで独自の型を宣言するだけなら、`language item` は必要ありません。

## ドキュメントコメント

宣言の直前に `///` を書くと、その宣言に対するドキュメントコメントになります。

```sobakasu
/// 2つの値を加算します。
function add(a: i32, b: i32) -> i32 {
  a + b
}
```

`///` の直後に対応する宣言がない場合はコンパイルエラーになります。

## 実装を見る

- [DeclarationParser.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Parser/DeclarationParser.cs)
- [ParserDiagnosticExtensions.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Parser/ParserDiagnosticExtensions.cs)
- [ADR-0054: Separate Type Implementation from Udon Script Behavior](/Sobakasu/ja/adr/adr-0054-separate-type-implementation-from-udon-script-behavior/)
