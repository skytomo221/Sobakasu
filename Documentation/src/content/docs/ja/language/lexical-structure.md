---
title: 字句構造
description: Sobakasuのコメント、識別子、リテラル、キーワードを説明します。
sidebar:
  order: 3
---

このページでは、Sobakasu のソースコードを構成する最小の要素であるコメント、識別子、キーワード、リテラルを説明します。

## コメント

行コメントは `//`、ブロックコメントは `/* ... */` で書きます。ブロックコメントは入れ子にできます。

```sobakasu
// 1 行のコメント

/* 外側のコメント
   /* 内側のコメント */
*/
```

`///` で始まる行は**ドキュメントコメント**です。直後の宣言に対する説明として扱われます。

```sobakasu
/// 2つの値を加算します。
function add(a: i32, b: i32) -> i32 {
  a + b
}
```

## 識別子

変数名、関数名、型名などに使う名前を**識別子**と呼びます。通常の識別子は Unicode XID の規則に従うため、ASCII 以外の文字も使用できます。

```sobakasu
let count = 1;
let 速度 = 2.0;
```

通常の識別子として書けない名前は、バッククォート `` ` `` で囲めます。

```sobakasu
let `a-b` = 1;
let `😀` = 2;
```

バッククォートで囲んだ識別子を改行の途中まで続けることはできません。

:::note
公開される名前など、一部の名前には追加の制約があります。外部へ公開する名前を特殊な文字で付けたい場合は、対応する宣言の規則も確認してください。
:::

## キーワード

Sobakasu の文法で特別な意味を持つ単語を**キーワード**と呼びます。主なキーワードは次のとおりです。

```text
on use module as function receive send to
language item type struct enum implementation extern
self Self new public sync const state behavior field
let mutable return match if else while loop break continue redo
ref out true false
```

`static` は現在の関数・メソッド宣言では使用できません。以前のコードに `static` が残っている場合はコンパイルエラーになります。

`null` も現在は使用しません。値が存在するかもしれないことは `Maybe<T>` で表します。

## リテラル

ソースコードに値を直接書いたものを**リテラル**と呼びます。Sobakasu では整数、浮動小数点数、文字、文字列、真偽値を直接書けます。

```sobakasu
let a = 42;
let b = 3.14;
let c = 'A';
let d = "hello\nworld";
let e = true;
```

整数は 2 進・8 進・10 進・16 進で表記できます。数値の末尾には、`i32` や `f32` など型を指定するための**接尾辞**を付けられます。

数値型には `i8`、`u8`、`i16`、`u16`、`i32`、`u32`、`i64`、`u64`、`f32`、`f64` があります。

文字列では `\"`、`\\`、`\n`、`\r`、`\t` などのエスケープ表記を使用できます。

## ループラベル

入れ子になったループを指定して `break` などを行うために、`'name` 形式のラベルを使用できます。

```sobakasu
'outer: loop {
  break 'outer;
}
```

## 実装を見る

- [SobakasuLexer.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Lexer/SobakasuLexer.cs)
- [SobakasuIdentifierFacts.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Syntax/SobakasuIdentifierFacts.cs)
- [IdentifierSyntaxTests.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Tests/Editor/Compiler/Syntax/IdentifierSyntaxTests.cs)
- [KeywordVocabularyTests.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Tests/Editor/Compiler/KeywordVocabularyTests.cs)
- [ADR-0046: Unicode Identifiers and the UASM Symbol Boundary](/Sobakasu/ja/adr/adr-0046-unicode-identifiers-and-uasm-symbol-boundary/)
- [ADR-0055: Sobakasu Source Keyword Vocabulary](/Sobakasu/ja/adr/adr-0055-sobakasu-source-keyword-vocabulary/)
