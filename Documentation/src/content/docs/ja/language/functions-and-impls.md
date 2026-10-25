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

  function increment(mutable self) {
    self.value += 1;
  }

  function zero() -> Counter {
    Counter { value: 0 }
  }
}
```

先頭の引数に `self` または `mutable self` を持つ関数は、その型の値に対して呼び出す**メソッド**です。

```sobakasu
let value = counter.get();
```

`self` を持たない関数は、型そのものに関連付けられた**関連関数**です。

```sobakasu
let counter = Counter::zero();
```

Sobakasu では、このために `static` キーワードは使用しません。

`Self` は現在の `implementation` の対象型を表します。

### `self` と `mutable self`

`self` は、呼び出し元の値を変更しないメソッドに使います。上の `get` は変更できない変数からも呼び出せます。`self` 自身や、その値に属するフィールドへの代入・複合代入はできません。

`mutable self` は、呼び出し元の値を変更するメソッドに使います。上の `increment` は、正常に処理を終えると変更後の値を呼び出し元へ書き戻します。途中の `return` で終了する場合も同様です。戻り値を使うかどうかには影響されません。

```sobakasu
let counter = Counter::zero();
let value = counter.get();
counter.increment(); // コンパイルエラー: counter は変更できない

let mutable changing = Counter::zero();
changing.increment();
```

書き戻し先には、変更可能な変数、`mutable self`、変更可能な値のフィールド、配列の要素などを使えます。一時値のように書き戻し先がない値からは呼び出せません。配列や添字など、書き戻す場所を決める式は引数より先に一度だけ評価され、呼び出し後も同じ場所へ書き戻します。

`self` / `mutable self` は `implementation` 内の先頭の引数として一つだけ指定し、型注釈は付けません。可変性の違いだけでオーバーロードすることはできません。通常の引数に `mutable` を付けることもできません。

参照先オブジェクトの状態を変更することと、変数が保持する参照値そのものを変更することは別です。例えば、次のメソッドは参照先の有効状態を変えるだけなので `self` を使えます。

```sobakasu
function set_active(self, active: bool)
  = extern self.SetActive(active)
```

`mutable self` は一般的な副作用を表す指定ではなく、所有権や借用、有効期間を管理する仕組みも導入しません。

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
