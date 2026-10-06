---
title: 型システム
description: Sobakasuの組み込み型、型注釈、Self、Maybeを説明します。
sidebar:
  order: 8
---

Sobakasu は**静的型付け言語**です。変数、式、関数の引数や戻り値などの型をコンパイル時に確認します。型が合わない値を渡したり代入したりするとコンパイルエラーになります。

## 組み込み型

現在の主な組み込み型は次のとおりです。

| Sobakasu | 意味 |
| --- | --- |
| `i8` / `u8` | 8 ビット整数 |
| `i16` / `u16` | 16 ビット整数 |
| `i32` / `u32` | 32 ビット整数 |
| `i64` / `u64` | 64 ビット整数 |
| `f32` | 32 ビット浮動小数点数 |
| `f64` | 64 ビット浮動小数点数 |
| `char` | 文字 |
| `string` | 文字列 |
| `bool` | 真偽値 |
| `object` | オブジェクト |

このほかに、ユニット型 `()`、配列 `[T]`、タプル `(T, U)`、ユーザー定義の `struct` / `enum`、外部型、ジェネリック型などがあります。

## 型注釈

変数などの型を明示するときは `:` の後ろに型を書きます。

```sobakasu
let count: i32 = 0;
```

関数の戻り値型は `->` の後ろに書きます。

```sobakasu
function length() -> i32 {
  1
}
```

型を省略できる場所では、初期値や引数などから型が判断されます。必要な型を一つに決められない場合はコンパイルエラーになります。

## ユニット型 `()`

`()` は「返す値がない」ことを表す**ユニット型**です。`()` 自体はユニット型の値でもあります。

```sobakasu
let unit: () = ();
```

戻り値型を省略した関数はユニット型を返します。

## 配列とタプル

配列型は `[T]`、タプル型は `(T1, T2, ...)` と書きます。

```sobakasu
let values: [i32] = [1, 2, 3];
let pair: (i32, string) = (1, "one");
```

1 要素のタプルには末尾のカンマが必要です。

```sobakasu
let one: (i32,) = (1,);
```

`(i32)` は 1 要素タプルではなく、括弧で囲んだ `i32` と同じ型です。

## `Self`

`Self` は `implementation` の中で、その `implementation` が対象としている型を表します。

`implementation` の外で `Self` を型として使うことはできず、コンパイルエラーになります。

## ジェネリック型

型に別の型を引数として渡す場合は `<...>` を使います。

```sobakasu
Box<i32>
Maybe<string>
```

詳しくは [ジェネリクス](./generics/) を参照してください。

## `Maybe<T>` と値の不在

Sobakasu では、値が存在しない可能性を通常の `null` では表しません。値が存在するかもしれない場合は `Maybe<T>` を使います。

```sobakasu
let empty: Maybe<i32> = Maybe::Nothing;
let value: Maybe<i32> = Maybe::Just(42);
```

`null` は現行構文では使用できません。

## Udon 側の制約

Sobakasu の型がすべての場所で Udon にそのまま渡せるとは限りません。特に外部 API、`public` な状態変数、同期する状態変数では、VRChat/Udon 側が対応している型である必要があります。

対応していない型をそのような場所で使用するとコンパイルエラーになります。

## 実装を見る

- [TypeParser.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Parser/TypeParser.cs)
- [TypeResolver.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Binder/Resolution/TypeResolver.cs)
- [ADR-0026: Eliminate null in favor of Maybe](/Sobakasu/ja/adr/adr-0026-eliminate-null-in-favor-of-maybe/)
