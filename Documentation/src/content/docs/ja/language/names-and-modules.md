---
title: 名前とモジュール
description: Sobakasuの修飾名、module、use、公開と再公開を説明します。
sidebar:
  order: 4
---

Sobakasu では、モジュールや型を含めて名前を指定する**修飾名**と、値が持つメンバーへのアクセスで異なる記号を使います。

- `::` — モジュール、型、列挙型のバリアント、関連関数などをつなぐ
- `.` — 値が持つフィールドやメソッドへアクセスする

```sobakasu
use std::math::Mathf;
let y = Mathf::Floor(x);
let length = values.length;
```

修飾名の区切りに `.` は使用できません。以前の `foo.Bar` のような書き方を修飾名として使うとコンパイルエラーになります。

## `module`

**モジュール**は、型や関数などの名前をまとめる単位です。モジュール宣言はソースのトップレベルに書きます。

```sobakasu
module example;
```

他のモジュールから公開する場合は `public` を付けます。

```sobakasu
public module utilities;
```

## `use`

`use` を使うと、別のモジュールにある名前を現在のソースから短い名前で参照できます。

```sobakasu
use foo::Bar;
use foo::Bar as Baz;
use foo::{Bar, Baz};
use foo::*;
```

`as` は取り込んだ名前に別名を付けます。`{ ... }` を使うと複数の名前をまとめて取り込めます。`*` はその場所から公開されている名前をまとめて取り込みます。

グループの中では `self` を使って、そのグループ自身も取り込めます。

```sobakasu
use foo::{self, Bar};
```

`public use` は、取り込んだ名前をさらに外部へ公開します。

```sobakasu
public use core::Maybe;
```

## Prelude

**Prelude** は、明示的な `use` を書かなくても最初から利用できる基本的な型や関数などの集まりです。

すべての標準ライブラリ API が Prelude に含まれるわけではありません。Prelude にない名前は `use` で取り込みます。

## `self` と `Self`

`self` はメソッドを呼び出した対象の値を表します。`implementation` 内でメソッドを宣言するときは、先頭の引数として `self` を書きます。

`Self` は、その `implementation` が対象としている型そのものを表します。

```sobakasu
implementation Counter {
  function get(self) -> i32 {
    self.value
  }

  function zero() -> Self {
    Self { value: 0 }
  }
}
```

## 名前解決

コード中の名前がどの変数、関数、型などを指すかを決めることを**名前解決**と呼びます。

Sobakasu では、ローカル変数、型パラメーター、現在のモジュール、`use` で取り込んだ名前、Prelude などを考慮して参照先を決めます。同じ名前が複数の候補を指して一つに決められない場合はコンパイルエラーになります。

## 実装を見る

- [ModuleParser.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Parser/ModuleParser.cs)
- [TypeResolver.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Binder/Resolution/TypeResolver.cs)
- [ADR-0047: Path Resolution and Method Model](/Sobakasu/ja/adr/adr-0047-path-resolution-and-method-model/)
