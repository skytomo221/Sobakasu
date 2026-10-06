---
title: 式
description: Sobakasuの主な式、呼び出し、アクセス、初期化を説明します。
sidebar:
  order: 11
---

評価すると値になる記述を**式**と呼びます。数値や文字列だけでなく、関数呼び出し、演算、`if`、`match` なども式です。

## リテラルと名前

```sobakasu
42
3.14
"text"
'A'
true
foo
foo::Bar
```

`foo::Bar` のような `::` は修飾名に使用します。値が持つメンバーへアクセスするときは `.` を使います。

## 括弧とタプル

```sobakasu
(value)
(a, b)
(value,)
()
```

`(value)` は式を括弧で囲んだだけで、値の意味は変わりません。

`(value,)` は 1 要素のタプルです。1 要素タプルでは、通常の括弧と区別するためにカンマが必要です。

## 配列

```sobakasu
[1, 2, 3]
[0; 10]
```

`[1, 2, 3]` は各要素を指定した配列、`[0; 10]` は `0` を 10 個持つ配列を作ります。

## 関数やメソッドの呼び出し

```sobakasu
add(1, 2)
object.method()
Type::associated()
generic<i32>(1)
```

`.` を使う呼び出しは値に対するメソッド呼び出しです。`::` を使う呼び出しは型やモジュールに関連付けられた関数などを呼び出します。

## メンバーへのアクセス

```sobakasu
value.member
Type::member
module::Type::member
```

`value.member` は、値が持つフィールドやメソッドなどへアクセスします。

`Type::member` や `module::Type::member` は、修飾名を使って型やモジュールに属する名前を参照します。

## 配列の要素

配列の要素は `[]` で参照します。

```sobakasu
values[index]
```

添字として使用できる型や、読み書きできるかどうかは対象の型によって決まります。使用できない型を添字に指定した場合はコンパイルエラーになります。

## 構造体などの初期化

名前付きフィールドを持つ値は、型名の後ろに `{ ... }` を書いて作れます。

```sobakasu
Position {
  x: 1.0,
  y: 2.0,
}
```

必要なフィールドが不足している場合や、存在しないフィールドを指定した場合はコンパイルエラーになります。

## `new`

コンストラクターを持つ型は、`new` を使って生成できます。主に外部 API のコンストラクターを呼び出す場合に使います。

```sobakasu
new SomeType(arg)
```

## `extern` 式

Unity や VRChat などの外部 API を直接呼び出す場合は `extern` 式を使用できます。

```sobakasu
extern UnityEngine.Debug.Log("hello");
```

何度も使う外部 API は、通常は `function` や `implementation` で Sobakasu の関数としてまとめる方が利用しやすくなります。

## 代入

```sobakasu
x = 1
x += 1
values[0] = 10
```

代入の左辺には、値を書き換えられる場所を指定する必要があります。たとえば `mutable` を付けていないローカル変数へ再代入するとコンパイルエラーになります。

## 制御を行う式

`if`、`match`、`while`、`loop` も式として扱われます。

```text
if ... { ... } else { ... }
match ... { ... }
while ... { ... }
loop { ... }
```

詳しくは [制御フロー](./control-flow/) と [パターン](./patterns/) を参照してください。

## 実装を見る

- [ExpressionParser.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Parser/ExpressionParser.cs)
- [StatementParser.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Parser/StatementParser.cs)
- [TypeResolver.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Binder/Resolution/TypeResolver.cs)
