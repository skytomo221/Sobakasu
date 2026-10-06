---
title: 制御フロー
description: if、while、loop、break、continue、redo、returnの書き方と動作を説明します。
sidebar:
  order: 13
---

条件によって処理を分けたり、処理を繰り返したり、途中で抜けたりする仕組みを**制御フロー**と呼びます。

## `if`

`if` は条件によって評価する処理を選びます。

```sobakasu
if condition {
  a
} else {
  b
}
```

条件は `bool` 型でなければなりません。

条件が `true` の場合は最初のブロックだけが評価され、`false` の場合は `else` 側だけが評価されます。選ばれなかった側の処理は実行されません。

`else if` も使用できます。

```sobakasu
if x < 0 {
  -1
} else if x == 0 {
  0
} else {
  1
}
```

`if` の結果を値として使う場合は、分岐から得られる値の型が一致している必要があります。一致しない場合はコンパイルエラーになります。

## `while`

`while` は、条件が `true` の間だけ処理を繰り返します。

```sobakasu
while count < 10 {
  count += 1;
}
```

各回の開始前に条件を評価します。最初から条件が `false` なら、ブロックは一度も実行されません。

条件は `bool` 型でなければなりません。

## `loop`

`loop` は、`break` や `return` などで抜けるまで繰り返します。

```sobakasu
loop {
  if done {
    break;
  }
}
```

`loop` は `break` に値を渡すことで、ループ全体の結果を返せます。

```sobakasu
let value = loop {
  break 42;
};
```

同じ `loop` から値付きの `break` を使う場合、その値の型は互いに一致している必要があります。

## ループラベル

入れ子になったループのどれを対象にするか指定したい場合は、`'name:` 形式のラベルを付けます。

```sobakasu
'outer: loop {
  loop {
    break 'outer;
  }
}
```

ラベルは `while` と `loop` に付けられます。

## `break`

`break` は対象のループを終了します。

```sobakasu
break;
break value;
break 'outer;
break 'outer value;
```

値を付けた `break` は、`loop` の結果としてその値を返します。

## `continue`

`continue` は現在の繰り返しの残りを飛ばして、対象のループの次の繰り返しへ進みます。

```sobakasu
continue;
continue 'outer;
```

`continue` に値を付けることはできません。

## `redo`

`redo` は対象のループで、現在の繰り返しをやり直すための構文です。

```sobakasu
redo;
redo 'outer;
```

`redo` に値を付けることはできません。

## `return`

`return` は現在の関数を終了し、呼び出し元へ戻ります。

```sobakasu
return;
return value;
```

戻り値を返す場合、その値の型は関数で宣言した戻り値型と一致している必要があります。

## ブロック末尾の式

値を返せるブロックでは、最後の式にセミコロンを付けないと、その式の値がブロック全体の結果になります。

```sobakasu
function answer() -> i32 {
  42
}
```

## 実装を見る

- [ExpressionParser.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Parser/ExpressionParser.cs)
- [StatementParser.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Parser/StatementParser.cs)
- [LoopBinder.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Binder/Statements/LoopBinder.cs)
- [ControlFlowTests.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Tests/Editor/Compiler/ControlFlow/ControlFlowTests.cs)
