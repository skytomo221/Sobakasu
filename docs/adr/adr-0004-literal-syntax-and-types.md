# ADR-0004: Literal Syntax and Built-in Literal Types

## Status

Accepted

## Context

Sobakasu は VRChat Udon 向けの Udon-first 言語として設計されており、C# 互換ではなく、Udon の制約と実用性を前提とした独自の型体系と構文を採用する。

ADR-0001（アーキテクチャ決定の記録方針）、ADR-0002（Sobakasu 開発理由）、ADR-0003（コンパイラパイプライン設計）に基づき、言語仕様は段階的に明確化される。

リテラル仕様は以下の理由から早期に確定する必要がある。

* Lexer / Parser / Binder の基盤となる
* 型推論および型検査の前提となる
* 定数畳み込みや IR 生成に影響する
* エラーメッセージ品質に直結する

また、設計上の要求は以下の通りである。

* Rust 風の読みやすい数値リテラルを採用する
* ただし Rust 互換ではなく Sobakasu 独自仕様とする
* Udon / Unity の特性上、既定の数値型は `f32` とする
* 実装簡素化のため、当面は 32 ビット型に限定する
* 配列・文字列・真偽値・null・void 相当を明確に定義する
* 型命名の一貫性を保つ

## Decision

Sobakasu は以下の組み込みリテラル型を定義する。

* `i32`
* `u32`
* `f32`
* `string`
* `[]`
* `bool`
* `null`
* `u0`

また、リテラル構文を以下のように定義する。

### 1. Integer literal (`i32`, `u32`)

整数リテラルは `i32` または `u32` を表す。

#### Integer literalの表記

* 10進数: `42`, `1_000`
* 2進数: `0b1010`
* 8進数: `0o755`
* 16進数: `0xFF`

#### Integer literalのサフィックス

```sobakasu
123i32
123u32
0xFFu32
```

#### Integer literalの規則

* サフィックスなし整数は `i32`
* `-` は単項演算子として扱う（リテラルではない）
* `_` は桁区切りとして使用可能
* 以下は禁止

  * `0x_FF`
  * `123_`
  * `123_i32`
* 範囲外はコンパイルエラー
* `-1u32` は型エラー

### 2. Floating-point literal (`f32`)

浮動小数点リテラルは `f32`。

#### Floating-point literalの表記

```sobakasu
3.14
0.5
1_000.25
```

#### Floating-point literalの指数表記

```sobakasu
1e3
1.5e2
2.5e-4
3.0E+2
```

#### Floating-point literalの規則

* 小数点または指数を含む場合 `f32`
* `10f32` のような明示指定も可能
* `_` 使用可能（ただし区切りルールあり）
* 無効な位置での `_` はエラー
* `f32` に変換できない値はエラー

### 3. String literal (`string`)

```sobakasu
"hello"
"Hello, world!"
"こんにちは"
```

#### String literalの規則

* `"` で囲む
* 改行は未対応
* エスケープ:

  * `\"`
  * `\\`
  * `\n`
  * `\r`
  * `\t`
* 未終端はエラー

### 4. Array literal (`[]`)

```sobakasu
[]
[1, 2, 3]
["a", "b"]
[true, false]
```

#### Array literalの規則

* `[expr, ...]`
* 要素型は単一
* 空配列は型推論できない場合エラー
* 多次元配列は未対応

### 5. Boolean literal (`bool`)

```sobakasu
true
false
```

* 予約語
* 型は `bool`

### 6. Null literal (`null`)

```sobakasu
null
```

#### Null literalの規則

* 参照的値の欠如を表す
* 使用可能:

  * `string`
  * 配列
  * 将来の参照型
* 使用不可:

  * `i32`
  * `u32`
  * `f32`
  * `bool`
  * `u0`

### 7. Void-like type (`u0`)

Sobakasu は `void` の代わりに `u0` を採用する。

#### 定義

* `u0` は値を返さない処理の型
* 命名は `i32` / `u32` / `f32` との一貫性のため

#### 重要な性質

* 整数型ではない
* 数値演算不可
* 変換不可

#### 使用例

```sobakasu
func hello(): u0 {
  Debug.Log("Hello")
}
```

## Alternatives

### C# 型をそのまま使う

* `int`, `uint`, `float`, `void`

問題:

* 型幅が不明確
* Udon 向け設計と不一致

### Rust に完全準拠

問題:

* Udon / Unity と相性が悪い
* `f32` を既定にしにくい

### `void` を採用

問題:

* 型命名の一貫性が崩れる

### `u0` を整数型として扱う

問題:

* 意味論が破綻する
* 利用者に誤解を与える

## Rationale

* 型幅明示（`i32`, `u32`, `f32`）は Udon と相性が良い
* `f32` を既定とすることで Unity 実用性が高い
* Rust 風リテラルは可読性が高い
* `u32` によりビット操作・16進数と親和性が高い
* 指数表記により数値表現の実用性が向上
* `u0` により型体系の一貫性を維持できる

## Consequences

### Positive

* 一貫した型命名
* 実装の単純化（32bit限定）
* 高い可読性
* Udon/Unity最適化

### Negative

* 学習コスト（C# と異なる）
* 型推論ルールの追加設計が必要
* `u0` の理解コスト

## Notes

本 ADR は以下を定義する。

* リテラル構文
* 型との対応
* 既定数値型
* `u0` の採用

以下は未確定。

* 暗黙型変換
* 配列内部表現
* null 許容ルール詳細
* IR / UASM への lowering
