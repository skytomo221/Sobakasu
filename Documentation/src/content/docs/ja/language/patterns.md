---
title: パターン
description: letによる値の分解とmatchで利用できるパターンを説明します。
sidebar:
  order: 14
---

値の形を調べたり、値を分解して名前へ受け取ったりするための書き方を**パターン**と呼びます。

Sobakasu では、`let` でタプルを分解するときと、`match` で値に応じて処理を選ぶときにパターンを使います。ただし、現在この二つで使えるパターンは同じではありません。

## `let` で値を分解する

`let` では、通常の変数名、値を受け取らない `_`、タプルの分解を使用できます。

```sobakasu
let value = 1;
let _ = ignored;
let (x, y) = pair;
let ((x,), _) = nested;
```

タプルを分解して値を受け取る場合、左辺と右辺の要素数は同じでなければなりません。要素数が異なるとコンパイルエラーになります。

`_` の位置の値は変数へ受け取られません。

タプルの中にさらにタプルがある場合も、入れ子にして分解できます。

```sobakasu
let ((x, y), z) = value;
```

## `match`

`match` は、値の内容に応じて評価する処理を選ぶ式です。

```sobakasu
match value {
  Maybe::Nothing => 0,
  Maybe::Just(x) => x,
}
```

`=>` の左側にパターン、右側にそのパターンに一致した場合の式を書きます。右側にはブロックも使用できます。

```sobakasu
match value {
  Result::Ok(x) => {
    log(x);
    x
  },
  Result::Error(message) => 0,
}
```

## `match` で使用できるパターン

現在は次のパターンを使用できます。

- 整数、文字、文字列、真偽値のリテラル
- `_` — どの値にも一致するワイルドカード
- 値を持たない列挙型バリアント — `Type::Variant`
- 位置付きの値を持つバリアント — `Type::Variant(a, b)`
- 名前付きフィールドを持つバリアント — `Type::Variant { field, ... }`

```sobakasu
match result {
  Result::Ok(value) => value,
  Result::Error { message } => 0,
}
```

バリアントの中で受け取る値には、現在は単純な識別子を指定します。

そのため、次のような入れ子のパターンは現在使用できません。

```sobakasu
// 現在は使用できない
match value {
  Outer::Value(Inner::Value(x)) => x,
}
```

また、`let` ではタプルを分解できますが、`match` の一番外側へ直接タプルパターンを書くことは現在できません。

```sobakasu
// let では使用できる
let (x, y) = pair;

// match では現在使用できない
match pair {
  (x, y) => x,
}
```

浮動小数点数のリテラルをパターンとして使用することも現在できません。これらの未対応な書き方はコンパイルエラーになります。

## `let` と `match` の違い

| パターン | `let` | `match` |
| --- | ---: | ---: |
| 名前 `x` | 使用できる | 単独では使用できない |
| `_` | 使用できる | 使用できる |
| タプル `(x, y)` | 使用できる | 一番外側では使用できない |
| 入れ子のタプル | 使用できる | 使用できない |
| リテラル `42` | 使用できない | 使用できる |
| 列挙型 `Foo::A` | 使用できない | 使用できる |
| 列挙型 `Foo::A(x)` | 使用できない | 使用できる |

## バリアントの修飾名

列挙型のバリアントは `::` を使って参照します。

```sobakasu
Maybe::Just(value)
```

`Maybe.Just(value)` のように `.` を修飾名の区切りとして使用することはできません。

## 網羅性

列挙型などを `match` する場合、必要な場合にはすべての可能性を処理できるかがコンパイル時に確認されます。これを**網羅性**の確認と呼びます。

各分岐で返す値を `match` 式の結果として使用する場合は、それぞれの結果の型も一致している必要があります。

## 実装を見る

- [PatternParser.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Parser/PatternParser.cs)
- [LocalDeclarationBinder.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Binder/Statements/LocalDeclarationBinder.cs)
- [AggregateTupleTests.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Tests/Editor/Compiler/Aggregates/AggregateTupleTests.cs)
- [ADR-0023: Match Expressions and Enum Pattern Matching](/Sobakasu/ja/adr/adr-0023-match-expressions-and-enum-pattern-matching/)
