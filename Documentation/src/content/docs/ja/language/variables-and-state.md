---
title: 変数・定数・状態
description: let、const、state、public、syncの規則と動作を説明します。
sidebar:
  order: 6
---

Sobakasu では、関数の中だけで使う変数、変更しない定数、イベントをまたいで保持する状態変数を別の構文で表します。

| 構文 | 用途 |
| --- | --- |
| `let` | 関数などの処理中だけ使うローカル変数 |
| `const` | 再代入しない定数 |
| `state { ... }` | イベント処理が終わった後も値を保持する状態変数 |

## `let`

ローカル変数は `let` で宣言します。

```sobakasu
let count = 1;
let name: string = "Sobakasu";
```

通常の `let` で宣言した変数へ後から別の値を代入することはできません。再代入したい場合は `mutable` を付けます。

```sobakasu
let mutable count = 0;
count += 1;
```

### タプルを分解して受け取る

`let` の左辺では、タプルを分解して複数の変数へ値を受け取れます。

```sobakasu
let (x, y) = pair;
let (_, value) = result;
```

`_` の位置の値は受け取らず、変数も作られません。

タプルを分解して値を受け取る場合、左辺と右辺の要素数は同じでなければなりません。要素数が異なるとコンパイルエラーになります。入れ子になったタプルも同じように分解できます。

```sobakasu
let ((x, y), z) = value;
```

詳しくは [パターン](../patterns/) を参照してください。

### 型を省略する場合

初期値から型を判断できる場合は、型注釈を省略できます。

```sobakasu
let count = 1;
```

型も初期値もなく、変数の型を決められない場合はコンパイルエラーになります。

## `const`

`const` はソースのトップレベルに宣言する定数です。必ず初期値を指定します。

```sobakasu
const MaxCount: i32 = 100;
public const Version = "1.0";
```

`const` は同期する状態ではないため、`sync` を付けることはできません。

## `state`

**状態変数**は、イベント処理が終わった後も値を保持する変数です。`state` ブロックの中へまとめて宣言します。

```sobakasu
state {
  count: i32 = 0;
  name: string = "default";
}
```

実行中のコードからは `state.<名前>` でアクセスします。

```sobakasu
behavior {
  on interact(state) {
    state.count += 1;
  }
}
```

この例では `interact` が発生するたびに `count` が増え、次の `interact` でも前回の値が残っています。

状態変数を読み書きするイベント処理や関数は、引数一覧の先頭に `state` と書きます。この `state` は通常の値を受け取る引数ではなく、状態変数へアクセスすることを明示するものです。

## `public`

`state` 内の変数に `public` を付けると、Unity の Inspector など外部から設定できる状態として公開できます。

```sobakasu
state {
  public target: object = field;
}
```

初期値として `field` を指定した状態変数は、ソースコード内の値ではなく、Unity 側のフィールドから初期値を受け取ります。

## `sync`

VRChat のネットワーク同期を行う状態変数には `sync` を付けます。

```sobakasu
state {
  sync score: i32 = 0;
  public sync(linear) position: f32 = field;
}
```

利用できる同期モードは次のとおりです。

- `sync` / `sync(none)` — 補間しない
- `sync(linear)` — 線形補間する
- `sync(smooth)` — 滑らかに補間する

**補間**は、同期で受け取った値の間を途中の値でつないで、変化を滑らかに見せる処理です。

すべての型がすべての同期モードに対応しているわけではありません。同期できない型や、その型で利用できない補間モードを指定するとコンパイルエラーになります。

修飾子を書く場合は `public sync(...) name ...` の順にします。

## 初期値

通常の状態変数の初期値には、プログラム開始前に確定できる値を指定します。実行時に関数を呼び出さなければ求められない値などは初期値にできません。

## 実装を見る

- [DeclarationParser.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Parser/DeclarationParser.cs)
- [StatementParser.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Parser/StatementParser.cs)
- [StateSyntaxTests.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Tests/Editor/Compiler/State/StateSyntaxTests.cs)
- [StateBindingTests.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Tests/Editor/Compiler/State/StateBindingTests.cs)
- [StateLoweringTests.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Tests/Editor/Compiler/State/StateLoweringTests.cs)
