# ADR-0054: Separate Type Implementation from Udon Script Behavior

## Status

Proposed

2026-10-05 に追補: behavior function の呼び出し構文は、その後の修正で `behavior::` による associated call と `state.` による receiver call を区別する形に整理された。以下の当初の決定本文は、当時の判断記録として保持する。

## Context

Sobakasu では、ユーザー定義型と、Udon 上で実行されるスクリプトの双方を扱う。

ユーザー定義型については、データ構造を `struct`、その型に属する操作を `impl` で表現する。

```sobakasu
struct Counter {
    value: i32,
}

impl Counter {
    fn increment(self) {
        self.value += 1;
    }
}
```

一方、Udon スクリプトには通常の値型とは異なる性質がある。

- UdonBehaviour として保持される状態を持つ。
- Unity / Udon からイベントを受け取る。
- VRChat のネットワークイベントを送受信する。
- Inspector から設定されるフィールドを持つ。
- スクリプトインスタンスの状態に対して副作用を発生させる。

これらを通常の `struct` / `impl` と同じモデルで扱うと、「値型に属する操作」と「Udon スクリプトとしての振る舞い」の境界が曖昧になる。

また、従来のトップレベル状態変数では、状態変数が通常の名前として直接参照できるため、関数がスクリプト状態に依存しているかをシグネチャから判断しにくい。

Sobakasu は Udon-first の言語として、通常の型の世界と Udon スクリプトの実行環境を明確に分離する必要がある。

## Decision

Sobakasu では、次の2組の概念を明確に分離する。

```text
struct ↔ impl
state  ↔ behavior
```

- `struct` は値の構造を定義する。
- `impl T` は型 `T` に属する操作を定義する。
- `state { ... }` は現在の Udon スクリプトインスタンスが保持する状態を定義する。
- `behavior { ... }` は現在の Udon スクリプトの実行上の振る舞いを定義する。

典型的なスクリプトは次の形になる。

```sobakasu
struct Counter {
    value: i32,
}

impl Counter {
    fn increment(self) {
        self.value += 1;
    }
}

state {
    counter: Counter = Counter { value: 0 };
    pub target: GameObject = field;
}

behavior {
    fn reset(state) {
        state.counter.value = 0;
    }

    on interact(state) {
        state.counter.increment();
        send changed to others;
    }

    receive changed(state) {
        debug::info(state.counter.value);
    }
}
```

### `struct` / `impl`

`struct` は通常のユーザー定義値型を定義する。

`impl T` は型 `T` に属する通常の操作を定義する。

```sobakasu
struct Counter {
    value: i32,
}

impl Counter {
    fn increment(self) {
        self.value += 1;
    }
}
```

`impl` は Udon スクリプトの実行コンテキストを持たない。

そのため、`impl` 内では以下を使用できない。

- script `state` capability
- `on`
- `receive`
- `send`

例えば次は不正とする。

```sobakasu
impl Counter {
    fn increment(self) {
        self.value += 1;
        send changed to others; // error
    }
}
```

`behavior Counter { ... }` のように `behavior` を型へ適用する構文は導入しない。

型に属する振る舞いは引き続き `impl T` で表現する。

### `state`

スクリプトが保持する状態は `state` ブロック内に宣言する。

```sobakasu
state {
    count: i32 = 0;
    enabled: bool = true;
}
```

`state` ブロックは `State` というユーザー可視の型を生成しない。

各メンバーは現在の Udon スクリプトインスタンスに属する状態であり、既存の Udon state storage へ lowering される。

通常の state member には initializer が必要である。

```sobakasu
state {
    count: i32 = 0; // OK
    value: i32;     // error
}
```

この決定によって default initialization は導入しない。

initializer を省略した通常の state member を、その型の既定値で暗黙初期化してはならない。

### `field` initializer

Inspector から値を供給する state member は、特殊な initializer `field` を使用して宣言する。

```sobakasu
state {
    pub target: GameObject = field;
}
```

`field` は state の別の宣言種別ではない。

次の共通した state declaration の中で、値の供給方法を指定する特殊な initializer である。

```text
name: Type = initializer;
```

例えば、

```sobakasu
state {
    count: i32 = 0;
    pub target: GameObject = field;
}
```

では、

- `count` は Sobakasu ソースの式 `0` によって初期化される。
- `target` は Sobakasu ソースでは初期化されず、Inspector から値を供給される。

`field` は一般式ではない。

したがって、次のような利用は認めない。

```sobakasu
behavior {
    fn example() {
        let x = field; // error
    }
}
```

```sobakasu
state {
    value: i32 = field + 1; // error
}
```

`field` は state declaration の完全な initializer としてのみ利用できる。

### `field` と `pub`

`= field` を使用する state member には `pub` を必須とする。

```sobakasu
state {
    pub target: GameObject = field; // OK
    other: GameObject = field;      // error
}
```

`pub` と `field` は別の責務を持つ。

- `pub` は Udon 上の public / exported state として公開することを表す。
- `field` は値を Sobakasu の initializer expression ではなく Inspector から供給することを表す。

Inspector へ公開するためには Udon 上でも公開された state とする必要があるため、Sobakasu では「Inspector から設定可能だが public ではない field」という概念を導入しない。

したがって、

```text
field ⇒ pub
```

を言語規則とする。

ただし `field` 自体が暗黙に `pub` を付与することはしない。

公開性はソース上で明示する。

```sobakasu
pub target: GameObject = field;
```

これにより、`pub` と `field` の意味をそれぞれ独立して読み取れる。

### `field` の型

`= field` を使用する場合、型注釈を必須とする。

```sobakasu
state {
    pub target: GameObject = field; // OK
    pub target = field;             // error
}
```

`field` 自体は値や型を持つ式ではなく、型推論の根拠にはならない。

### `behavior`

`behavior` は現在の Udon スクリプトの実行環境に属する振る舞いを定義する。

```sobakasu
behavior {
    fn reset(state) {
        state.count = 0;
    }

    on interact(state) {
        reset(state);
    }

    receive changed(state) {
        debug::info(state.count);
    }
}
```

`behavior` には少なくとも以下を置くことができる。

- behavior function (`fn`)
- Udon event handler (`on`)
- network receive handler (`receive`)

また、`behavior` の実行コンテキストでは `send` statement を使用できる。

`behavior` は型を対象にしないため、次のような構文は認めない。

```sobakasu
behavior Counter {
    // error
}
```

### `state` capability

`behavior` 内の callable が script state にアクセスする場合、その依存性をシグネチャで明示する。

```sobakasu
behavior {
    fn reset(state) {
        state.count = 0;
    }
}
```

ここで記述される `state` は通常の値引数ではない。

`state` は、現在のスクリプトインスタンスの状態へアクセスする権限を表す compile-time capability である。

したがって、

```sobakasu
behavior {
    fn calculate(x: i32) -> i32 {
        x * 2
    }
}
```

のように `state` capability を要求しない callable は script state にアクセスできない。

状態へのアクセスは必ず `state.<name>` と明示する。

```sobakasu
behavior {
    fn reset(state) {
        state.count = 0;
    }
}
```

従来のような状態変数の暗黙参照は認めない。

```sobakasu
behavior {
    fn reset(state) {
        count = 0; // error
    }
}
```

また、`state` capability を要求していない callable から `state` を使用することもできない。

```sobakasu
behavior {
    fn reset() {
        state.count = 0; // error
    }
}
```

`state` capability はユーザー定義型ではなく、通常の first-class value でもない。

以下のような用途には使用できない。

- state variable や local variable への保存
- 戻り値としての返却
- 通常のデータ構造への格納
- `impl` method への通常の値としての受け渡し
- ユーザーによる生成

一方、state capability を必要とする behavior function へは明示的に伝播できる。

```sobakasu
behavior {
    fn reset(state) {
        state.count = 0;
    }

    on interact(state) {
        reset(state);
    }
}
```

この `state` の受け渡しも compile-time capability の伝播であり、UASM 上の通常の引数として生成してはならない。

### `on`

`on` は `behavior` 内でのみ宣言できる。

```sobakasu
behavior {
    on interact(state) {
        state.count += 1;
    }
}
```

`on interact(state)` の `state` は Udon event のランタイム引数ではない。

Udon event 本来のパラメータと `state` capability は別々に扱う。

したがって `state` capability を追加しても、Udon event signature や UASM 上の parameter storage を変更しない。

### `receive`

`receive` は `behavior` 内でのみ宣言できる。

```sobakasu
behavior {
    receive changed(state, value: i32) {
        state.count = value;
    }
}
```

ここでも `state` は network payload に含まれない。

上記の `receive` でネットワーク経由で送受信される引数は `value` のみである。

state を使用しない receiver では capability を要求する必要はない。

```sobakasu
behavior {
    receive ping() {
        debug::info("ping");
    }
}
```

### `send`

`send` は `behavior` の実行コンテキストからのみ使用できる。

```sobakasu
behavior {
    fn notify() {
        send changed to others;
    }

    on interact() {
        notify();
    }
}
```

`send` を利用するために `state` capability 自体は必要ない。

`state` capability は script state へのアクセス権限であり、network send capability ではない。

一方、`impl` method や通常の module function から `send` を実行することはできない。

これにより、VRChat networking に対する副作用を script `behavior` 内へ限定する。

### 通常の module function

`behavior` の導入は、通常の module-level function を `behavior` function に置き換えるものではない。

通常の module function は、script state や Udon behavior context に依存しない処理として引き続き利用できる。

```sobakasu
fn double(x: i32) -> i32 {
    x * 2
}
```

通常の module function は `state` capability を持たず、`send` も使用できない。

これにより、

```text
module / impl
    通常の型・値・計算の世界

state / behavior
    現在のUdonスクリプトインスタンスの世界
```

という境界を維持する。

### Runtime representation

`state` capability のために runtime object を生成しない。

特に、以下のような実装モデルは採用しない。

- synthetic `State` struct を生成する。
- `state` 用の heap slot を作る。
- `state` を Udon event parameter に追加する。
- `state` を network payload に追加する。
- behavior function の UASM 引数として `state` を渡す。

`state.foo` は compile-time に現在の script state symbol `foo` へ解決し、既存の Udon state storage へ lowering する。

したがって、

```sobakasu
state.foo
```

は通常の object field access を意味しない。

これは script state への明示的かつ静的に検査されるアクセスである。

## Alternatives

### トップレベルの state / function / event を維持する

例えば次の形式を維持する案。

```sobakasu
state count: i32 = 0;

on interact() {
    count += 1;
}
```

記述量は少ない。

しかし、script state への依存が暗黙になり、通常の module function と Udon script behavior の境界も不明確になる。

採用しない。

### 匿名 `impl { ... }` を Udon script behavior として利用する

```sobakasu
state {
    count: i32 = 0;
}

impl {
    on interact(state) {
        state.count += 1;
    }
}
```

`impl T` と形を合わせられるが、通常の型 implementation と Udon script execution context という異なる概念を同一のキーワードへ統合することになる。

採用しない。

### `behavior T { ... }` で型の操作も表す

```sobakasu
behavior Counter {
    fn increment(self) {
        self.value += 1;
    }
}
```

`state` / `behavior` の語彙へ統一できる。

しかし、通常の値型の操作と Udon script behavior の意味的境界が失われる。

型には `impl T`、script には `behavior` を使用する。

採用しない。

### state を通常の `State` 型として表現する

```sobakasu
fn reset(state: State) {
    state.count = 0;
}
```

通常の型システムや関数引数として扱いやすい。

しかし、実際には `State` object が runtime に存在するわけではなく、UdonBehaviour の data storage を抽象化するためだけの偽の runtime object を導入することになる。

Sobakasu のソースモデルと Udon の実行モデルの差が不必要に大きくなるため採用しない。

### state へ暗黙アクセス可能にする

```sobakasu
behavior {
    fn reset() {
        count = 0;
    }
}
```

簡潔だが、関数シグネチャから state dependency を判断できない。

また、通常の module function と behavior function の違いが弱くなる。

採用しない。

### `field name: Type;` という専用宣言構文を使う

```sobakasu
state {
    field target: GameObject;
}
```

`field` が宣言種別であることは明確になる。

一方、state member の宣言構造が、

```text
field name: Type;
name: Type = expression;
```

の2系統に分かれる。

`field` は「別種の状態」ではなく「値をどこから供給するか」の違いとして扱う方が、state declaration の意味を統一できる。

そのため、

```sobakasu
pub target: GameObject = field;
```

を採用する。

### `field` によって暗黙に `pub` を付与する

```sobakasu
target: GameObject = field;
```

と書くだけで Inspector に公開する案。

記述量は減る。

しかし、`field` が値の供給方法だけでなく visibility まで暗黙に変更することになり、構文上の責務が混在する。

Sobakasu では公開性を `pub` で明示するため採用しない。

### private な Inspector field を提供する

Inspector には表示するが、Sobakasu/Udon 上では public ではない field を導入する案。

Inspector への公開には Udon 側で公開された data symbol が必要となるため、言語上 private と表現しても runtime 上の強い private 性とは一致しない。

また `pub` の意味も複雑になるため、この概念は導入しない。

## Rationale

`struct` / `impl` と `state` / `behavior` を分離することで、Sobakasu のコードは次の2つの領域へ明確に分かれる。

```text
struct / impl
    通常の値と、その値に属する操作

state / behavior
    UdonBehaviour として存在する
    現在のスクリプトインスタンスの状態と振る舞い
```

この境界は、単なる構文上の整理ではない。

`behavior` に Udon event、network receive、network send を閉じ込めることで、Udon runtime に依存する副作用の発生場所をソース上で識別できる。

また state access を `state` capability と `state.<name>` で明示することで、ある callable が script state に依存するかをシグネチャから判断できる。

`state` capability を compile-time only とすることで、その明示性を得ながら、Udon runtime に存在しない synthetic object や余計な storage を導入せずに済む。

`field` についても、

```text
pub   = visibility / Udon export
field = value source
```

と責務を分離する。

これにより、

```sobakasu
pub target: GameObject = field;
```

は、

> `target` は外部公開され、値は Sobakasu の式ではなく Inspector から供給される

と構文だけから読み取れる。

この設計は、C# の class / field model を模倣するのではなく、Udon の実行モデルを Sobakasu 独自の言語概念として明示的に表現するという Udon-first の方針に沿う。

## Consequences

### Positive

- 通常のユーザー定義型と Udon script execution context の境界が明確になる。
- `struct` / `impl` と `state` / `behavior` の対応関係が明確になる。
- UdonBehaviour を表現するためだけの `class` が不要になる。
- script state への依存性を callable signature から判断できる。
- `state.<name>` により state access が明示される。
- Udon event と network receive を `behavior` に集約できる。
- `send` による networking side effect を `behavior` 内へ限定できる。
- `impl` を通常の型操作として保つことができる。
- `field` と通常の initializer を同一の state declaration syntax で表現できる。
- `pub` と `field` の責務が分離される。
- state capability のための runtime object や storage が不要である。
- Binder が state dependency や不正な execution context を静的に検査できる。

### Negative

- 従来のトップレベル state / event / receive 構文との互換性が失われる。
- state access のたびに `state.` を記述する必要がある。
- state-dependent callable では `state` capability を明示する必要がある。
- `state` という Sobakasu 固有の capability concept を理解する必要がある。
- `behavior` 内外で利用可能な機能が異なるため、Compiler が execution context を追跡する必要がある。
- `on` / `receive` では capability parameter と runtime parameter を区別する必要がある。
- behavior function call でも capability argument と runtime argument を区別する必要がある。
- `field` は必ず `pub` となるため、Inspector だけに公開された private field という概念は提供できない。
- Parser、Binder、diagnostics、tests、および既存コードの移行に広範な変更が必要になる。

## Success Criteria

以下をすべて満たした場合、この設計が実装されたとみなす。

1. `state { ... }` に script state を宣言できる。
2. 通常の state member は initializer を必要とし、default initialization は行われない。
3. `pub name: Type = field;` により Inspector から供給される state を宣言できる。
4. `= field` には `pub` と明示型が必須である。
5. `field` を通常の expression として使用できない。
6. state-dependent callable は `state` capability を明示できる。
7. state member は `state.<name>` でのみアクセスできる。
8. state capability のない callable から script state へアクセスすると compile error になる。
9. `behavior { ... }` に behavior function、`on`、`receive` を宣言できる。
10. `send` は `behavior` execution context でのみ使用できる。
11. `impl` から `state` capability、`on`、`receive`、`send` を使用できない。
12. `behavior T { ... }` は受理しない。
13. `state` capability は Udon event parameter として生成されない。
14. `state` capability は network payload に含まれない。
15. `state` capability は behavior function の runtime argument として生成されない。
16. `state` capability のための synthetic runtime object または heap slotを生成しない。
17. `state.<name>` は既存の Udon state storage へ正しく lowering される。
18. `struct` / `impl` と `state` / `behavior` を同一スクリプト内で組み合わせて利用できる。
