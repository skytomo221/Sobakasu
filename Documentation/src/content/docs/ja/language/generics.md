---
title: ジェネリクス
description: 型や関数を複数の型で再利用するためのジェネリクスを説明します。
sidebar:
  order: 10
---

**ジェネリクス**は、型を一つに固定せず、型をパラメーターとして受け取れるようにする仕組みです。同じ型や関数を、複数の具体的な型で再利用できます。

## 型パラメーター

たとえば、値を一つ保持する `Box` を型ごとに別々に定義する代わりに、型パラメーター `T` を使って次のように宣言できます。

```sobakasu
struct Box<T> {
  value: T,
}
```

利用するときに具体的な型を指定します。

```sobakasu
let number_box: Box<i32>;
let text_box: Box<string>;
```

複数の型パラメーターを持つこともできます。

```sobakasu
enum Result<T, E> {
  Ok(T),
  Error(E),
}
```

## ジェネリック関数

関数も型パラメーターを持てます。

```sobakasu
function identity<T>(value: T) -> T {
  value
}
```

呼び出すときは型引数を明示できます。

```sobakasu
let value = identity<i32>(42);
```

型引数を省略しても、渡した値などから `T` を一つに決められる場合は、その型が使われます。型を決められない場合はコンパイルエラーになります。

## ジェネリックな `implementation`

ジェネリック型に対する `implementation` では、対象となる型パラメーターを宣言します。

```sobakasu
implementation<T> Box<T> {
  function get(self) -> T {
    self.value
  }
}
```

## 型引数の数

指定する型引数の数は、宣言された型パラメーターの数と一致しなければなりません。

```sobakasu
Box<i32>             // Box<T>
Result<i32, string>  // Result<T, E>
```

数が足りない場合や多すぎる場合はコンパイルエラーになります。

## コンパイル時の具体化

Sobakasu のジェネリクスは**単相化**によってコンパイルされます。単相化とは、実際に使用された型ごとに具体的な版をコンパイル時に作る方法です。

たとえば `identity<i32>` と `identity<string>` を使うと、それぞれの型に対応した処理がコンパイル時に用意されます。実行時に型パラメーターを切り替える仕組みではありません。

そのため、最終的に使用する具体的な型は Udon で扱える必要があります。

## 外部 API のジェネリクス

Unity や VRChat の外部 API にあるジェネリック機能は、Sobakasu で定義したジェネリック型や関数と同じ条件で利用できるとは限りません。利用可否は Udon が公開している API にも依存します。

## 実装を見る

- [TypeParser.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Parser/TypeParser.cs)
- [DeclarationParser.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Parser/DeclarationParser.cs)
- [TypeResolver.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Binder/Resolution/TypeResolver.cs)
