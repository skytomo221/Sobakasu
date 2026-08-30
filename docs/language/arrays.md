# 配列

Sobakasu の `[T]` は Udon／CLR の一次元 `T[]` に対応する共有可能な可変参照です。長さは型に含まれません。

```sobakasu
let values: [i32] = [1, 2, 3];
let names: [string] = [];
let objects: [object] = [1, "text", true];
```

空配列には期待型が必要です。期待型のない異種配列を自動的に `[object]` へ推論することはありません。

## 生成

```sobakasu
let zeros = [i32; 4];       // CLRの既定値で生成
let repeated = [next(); 4]; // next()を要素ごとに再評価
```

`[T; length]` は既定値で初期化された配列を生成します。`[expression; length]` は length を一度評価し、添字0から順に expression を一度ずつ評価します。長さ0では expression を評価しません。

`[[T]]` は `T[][]` というジャグ配列です。矩形多次元配列ではありません。ジャグ配列を含む各 ABI 型は、インストール済み VRChat SDK が実際に公開する場合だけ利用できます。このリポジトリの VRChat SDK 3.10.4 は `System.Int32[][]` を公開していないため、`[[i32]]` は現在は ABI 利用不可診断になります。

## 添字と長さ

```sobakasu
let values = [1, 2, 3];
values[0] = 10;
values[1] += 5;
log(values.length);
log(values.length());
```

添字と length の型は `i32` です。コンパイラは独自の境界検査を追加せず、範囲外アクセスは Udon 配列 extern の挙動に従います。

## `let`、`mut`、共有

`let` は配列参照の差し替えを禁止しますが、参照先要素の変更を禁止しません。

```sobakasu
let original = [1, 2, 3];
let shared = original;
shared[0] = 100;       // 有効。original[0]からも100が見える
original = [4, 5, 6]; // エラー
```

参照を差し替える場合だけ `let mut` を使います。代入や引数、戻り値で暗黙 clone や move は発生しません。

## 状態、public、同期

```sobakasu
state values: [i32] = [1, 2, 3];
pub state names: [string];
sync state scores: [i32] = [];
```

private stateの定数配列initializerは型付きheap patch manifestに保存され、ProgramAssetのrefresh後にも復元されます。public stateはsource initializerを持たず、値はUdonBehaviour／Inspectorから提供されるため、explicit type annotationが必要です。publicはSDKが公開可能な配列ABI型だけを許可します。

同期配列は `sync`／`sync(none)` の一次元対応型だけです。`sync(linear)`、`sync(smooth)`、`[object]`、Unity object 参照配列、ジャグ配列は同期できません。利用可能なローカル配列型と同期可能な配列型は別々に検査されます。

## 対象外

矩形多次元配列、slice、range indexing、可変長配列、`List<T>`、暗黙 deep clone、`object` からの unboxing、安全添字アクセス、Rust風 ownership は対象外です。
