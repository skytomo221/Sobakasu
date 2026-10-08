---
title: 複合型
description: 配列、タプル、struct、enumの書き方と動作を説明します。
sidebar:
  order: 9
---

複数の値を一つのまとまりとして扱いたい場合は、配列、タプル、`struct`、`enum` を利用できます。

## 配列

配列は、同じ型の値を順番に並べて扱うための型です。配列型は `[T]` と書きます。

```sobakasu
let values: [i32] = [1, 2, 3];
let names: [string] = [];
```

同じ値で指定した個数の配列を作る場合は `[値; 個数]` と書きます。

```sobakasu
let zeros = [0; 4];
```

配列の要素は 0 から始まる番号で指定します。

```sobakasu
let first = values[0];
values[0] = 10;
```

配列を Unity や VRChat の外部 API、公開状態、同期状態などで使う場合は、Udon 側でもその配列型を扱える必要があります。

## タプル

**タプル**は、複数の値を順番付きで一組にした値です。配列とは異なり、要素ごとに異なる型を持てます。

```sobakasu
let unit: () = ();
let one: (i32,) = (42,);
let pair: (i32, string) = (42, "hello");
```

要素は 0 から始まる位置で取り出します。

```sobakasu
let number = pair.0;
let text = pair.1;
```

タプルを分解して、複数の変数へ一度に値を受け取ることもできます。

```sobakasu
let (number, text) = pair;
```

この場合、左辺と右辺の要素数は同じでなければなりません。要素数が異なるとコンパイルエラーになります。

## `struct`

`struct` は、名前の付いた複数のフィールドを一つの値としてまとめる型です。

```sobakasu
struct Position {
  x: f32,
  y: f32,
}
```

値を作るときは、各フィールドの値を指定します。

```sobakasu
let p = Position {
  x: 1.0,
  y: 2.0,
};
```

フィールドは `.` でアクセスします。

```sobakasu
let x = p.x;
```

`struct` は型パラメーターを持つこともできます。

## `enum`

`enum` は、値が取り得る形をあらかじめ列挙する**列挙型**です。それぞれの要素を**バリアント**と呼びます。

バリアントは追加の値を持たない形、位置で値を持つ形、名前付きフィールドを持つ形を作れます。

```sobakasu
enum Result<T> {
  Pending,
  Ok(T),
  Error { message: string },
}
```

バリアントは `::` を使って参照します。

```sobakasu
let a = Result::Pending;
let b = Result::Ok(42);
let c = Result::Error { message: "failed" };
```

バリアントが持つ値は `match` で取り出せます。詳しくは [パターン](../patterns/) を参照してください。

## 外部の構造体・列挙型

Unity や VRChat などが提供する外部型に対応する `struct` / `enum` も宣言できます。詳しくは [extern](../extern/) を参照してください。

## 実装を見る

- [DeclarationParser.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Parser/DeclarationParser.cs)
- [ExpressionParser.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Parser/ExpressionParser.cs)
- [PatternParser.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Parser/PatternParser.cs)
- [ArrayCompilerTests.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Tests/Editor/Compiler/Arrays/ArrayCompilerTests.cs)
- [ADR-0031: Tuples and extern ref/out value returns](/Sobakasu/ja/adr/adr-0031-introduce-tuples-and-adapt-extern-ref-out-parameters-to-value-returns/)
