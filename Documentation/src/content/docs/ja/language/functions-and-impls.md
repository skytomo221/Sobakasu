---
title: 関数と implementation
description: function、メソッド、関連関数、implementationの規則を説明します。
sidebar:
  order: 7
---

処理を名前付きでまとめるには `function` を使います。特定の型に対するメソッドや関連関数は `implementation` ブロックへ記述します。

## 関数

```sobakasu
function add(a: i32, b: i32) -> i32 {
  a + b
}
```

`a` と `b` は引数、`-> i32` は戻り値の型です。

戻り値の型を省略した関数は、値を返さないことを表すユニット型 `()` を返します。

### 末尾の式を返す

関数の最後にセミコロンなしで式を書くと、その式の値が関数の戻り値になります。

```sobakasu
function answer() -> i32 {
  42
}
```

`return` を使って途中で値を返すこともできます。

```sobakasu
function answer() -> i32 {
  return 42;
}
```

返す値の型が宣言した戻り値型と一致しない場合はコンパイルエラーになります。

## オーバーロード

同じ名前の関数を、異なる引数などで複数宣言できます。これを**オーバーロード**と呼びます。

呼び出し時には、渡した引数の個数や型などから使用する関数が決まります。一つに決められない場合はコンパイルエラーになります。

## ジェネリック関数

型をパラメーターとして受け取る関数を宣言できます。

```sobakasu
function identity<T>(value: T) -> T {
  value
}
```

詳しくは [ジェネリクス](../generics/) を参照してください。

## `implementation`

`implementation` は、特定の型に対するメソッドや関連関数をまとめるためのブロックです。

```sobakasu
struct Counter {
  value: i32,
}

implementation Counter {
  function get(self) -> i32 {
    self.value
  }

  function zero() -> Counter {
    Counter { value: 0 }
  }
}
```

先頭の引数に `self` を持つ関数は、その型の値に対して呼び出す**メソッド**です。

```sobakasu
let value = counter.get();
```

`self` を持たない関数は、型そのものに関連付けられた**関連関数**です。

```sobakasu
let counter = Counter::zero();
```

Sobakasu では、このために `static` キーワードは使用しません。

`Self` は現在の `implementation` の対象型を表します。

## ジェネリックな `implementation`

ジェネリック型に対して `implementation` を書く場合は、型パラメーターを宣言できます。

```sobakasu
implementation<T> Box<T> {
  function get(self) -> T {
    self.value
  }
}
```

## 状態変数へアクセスする関数

状態変数へアクセスする関数は、引数一覧の先頭に `state` と書きます。

```sobakasu
function increment(state, value: i32) -> i32 {
  value + state.step
}
```

ここで `state` は通常の値を受け取る引数ではありません。この関数が状態変数を参照することを明示します。

## `?` で終わる関数名

関数など呼び出せる名前は、末尾に `?` を付けられます。`?` は名前の一部です。

変数名や状態変数名など、通常の識別子には `?` を付けられません。

## 外部関数

関数本体の代わりに `extern` を指定すると、Unity や VRChat などが提供する外部 API に対応付けられます。

```sobakasu
function log(value: object)
  = extern UnityEngine.Debug.Log(value);
```

詳しくは [extern](../extern/) を参照してください。

## 実装を見る

- [DeclarationParser.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Parser/DeclarationParser.cs)
- [ExpressionParser.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Parser/ExpressionParser.cs)
- [ADR-0047: Path Resolution and Method Model](/Sobakasu/ja/adr/adr-0047-path-resolution-and-method-model/)
- [ADR-0054: Separate Type Implementation from Udon Script Behavior](/Sobakasu/ja/adr/adr-0054-separate-type-implementation-from-udon-script-behavior/)
