---
title: extern
description: SobakasuからUnity、VRChat、CLRなどの外部APIを利用する方法を説明します。
sidebar:
  order: 17
---

`extern` は、Unity、VRChat、CLR など **Sobakasu の外側で提供される型や関数を Sobakasu から利用するための仕組み**です。

通常のアプリケーションコードから外部 API を直接呼ぶこともできますが、標準ライブラリでは外部 API を Sobakasu の型や関数としてまとめてから公開します。

## 外部 API の名前

Sobakasu の通常の修飾名と、CLR など外部 API 側の名前では区切り方が異なります。

```text
Sobakasu の修飾名: foo::Bar::baz
CLR の型名:       UnityEngine.GameObject
CLR の入れ子型:   Namespace.Outer+Inner
```

Sobakasu の修飾名では `::` を使いますが、`extern` で CLR の型やメンバーを指定するときは、外部 API 側の名前をそのまま使うため `.` を使用します。

CLR の入れ子型では `+` が使われることがあります。

外部 API 名の中を `::` で区切ることはできず、使用するとコンパイルエラーになります。

## 外部の型を Sobakasu の型として使う

`type ... = extern ...;` を使うと、外部の型に対応する Sobakasu の型を宣言できます。

```sobakasu
public type GameObject = extern UnityEngine.GameObject;
```

この例では、Sobakasu の `GameObject` が外部の `UnityEngine.GameObject` に対応します。

これは別の Sobakasu 型に単なる別名を付ける機能ではありません。外部に実在する型との対応付けです。

## 外部の `struct` と `enum`

外部型が構造体や列挙型として扱われる場合は、`struct` や `enum` として対応付けられます。

```sobakasu
public struct VectorLike = extern Runtime.VectorLike {
  x: f32 = extern x,
  y: f32 = extern y,
}

public enum Direction = extern Runtime.Direction {
  Left = extern Left,
  Right = extern Right,
}
```

外部に存在しない型やメンバー、Udon から利用できない API を指定するとコンパイルエラーになります。

## 外部型の関数

外部型に対する関数は `implementation` にまとめられます。

```sobakasu
public implementation GameObject = extern UnityEngine.GameObject {
  function get_name(self) -> string
    = extern UnityEngine.GameObject.get_name(self);
}
```

実際に指定できる外部名は、VRChat/Udon が公開している API によって決まります。

## 外部関数

関数本体の代わりに `= extern ...;` と書くと、Sobakasu の関数を外部 API に対応付けられます。

```sobakasu
function log(value: object)
  = extern UnityEngine.Debug.Log(value);
```

利用側は `log(...)` という Sobakasu の関数名だけを使えるため、外部 API の長い名前を毎回書く必要がありません。

## `ref` と `out`

外部 API には、一つの戻り値だけでなく `ref` や `out` を使って複数の値を返すものがあります。

- `ref` — 呼び出す前の値を入力として渡し、呼び出し後の値も受け取る
- `out` — 呼び出し後の値を受け取る

Sobakasu では、これらの複数の出力を通常の戻り値として受け取れるようにし、必要な場合はタプルとしてまとめます。

```sobakasu
function update(value: i32) -> (bool, i32, string)
  = extern External.Api.Update(ref i32 value, out string message);
```

## `maybe extern`

外部 API は `null` を返すことがありますが、Sobakasu の通常の値では `null` を使用しません。

`maybe extern` を使う外部関数では、外部 API から返された `null` を `Maybe<T>` の「値なし」として扱えます。

```sobakasu
function find(name: string) -> Maybe<GameObject>
  = maybe extern UnityEngine.GameObject.Find(name);
```

これにより、Sobakasu 側では `null` ではなく `Maybe<T>` として値の有無を扱えます。

外部 API の `out` が `null` になり得る場合には、対応する場所で `maybe out` も使用されます。

## `extern` 式

外部 API をその場で直接呼び出すこともできます。

```sobakasu
extern UnityEngine.Debug.Log("hello");
```

同じ API を複数の場所から利用する場合は、`function` や `implementation` としてまとめると、利用側を外部 API の名前から切り離せます。

## `new`

外部型に利用可能なコンストラクターがある場合は、`new` を使って生成できます。

```sobakasu
new SomeType(arg)
```

そのコンストラクターが Udon から利用できない場合はコンパイルエラーになります。

## Udon 側の制約

`extern` の書き方が正しくても、指定した型や関数が現在の VRChat SDK から Udon 向けに公開されていなければ利用できません。

外部 API との間で値をどの形式で受け渡すかという規約を **ABI** と呼びます。`ref` / `out`、配列、外部型などを使う場合は、Sobakasu 側の宣言と Udon 側の ABI が一致している必要があります。

このページでは `extern` の書き方と意味を説明します。実際に標準ライブラリから利用できる API は [標準ライブラリリファレンス](/Sobakasu/ja/reference/standard-library/) を参照してください。

## 実装を見る

- [ParserUtilities.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Parser/ParserUtilities.cs)
- [DeclarationParser.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Parser/DeclarationParser.cs)
- [ExternDeclarationBinder.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Binder/Declarations/ExternDeclarationBinder.cs)
- [StandardLibraryGenerator](https://github.com/skytomo221/Sobakasu/tree/main/Packages/com.skytomo221.sobakasu/Editor/Tools/StandardLibraryGenerator)
- [ADR-0031: Tuples and extern ref/out value returns](/Sobakasu/ja/adr/adr-0031-introduce-tuples-and-adapt-extern-ref-out-parameters-to-value-returns/)
- [ADR-0045: External Nominal Type Declarations and Udon Exposure Catalog](/Sobakasu/ja/adr/adr-0045-external-nominal-type-declarations-and-udon-exposure-catalog/)
