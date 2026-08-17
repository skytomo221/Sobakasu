# ADR-0031: Introduce Tuples and Adapt Extern Ref/Out Parameters to Value Returns

## Status

Accepted

## Context

Sobakasu は Udon VM を対象とし、struct と enum を Udon の複数の leaf slot へ flatten する aggregate 基盤をすでに持っている。一方、複数の値を名前付きデータ型なしで受け渡す product type はなく、Unity / CLR API の `ref` / `out` parameter を通常の Sobakasu 関数として公開できなかった。

CLR の `ref` は入力と出力、`out` は出力として Udon extern の slot に現れる。しかし、これらを通常の Sobakasu 関数へ導入すると aliasing、lifetime、writable reference value など、Udon-first の言語に不要な reference semantics が必要になる。extern 境界だけで ABI 情報として保持し、利用者には通常の入力値と戻り値として見せる必要がある。

また、従来の zero-sized return type `u0` は product type と独立した特殊型だった。0 要素 tuple を unit type とすれば、値、関数戻り値、control-flow の型、extern の 0 出力を同じ規則で表現できる。

本 ADR は ADR-0004、ADR-0010、ADR-0011、ADR-0012、ADR-0016、ADR-0019、ADR-0020、ADR-0024、ADR-0030 の unit type と extern parameter に関する記述を、矛盾する範囲で置き換える。Parser、Binder、IR、UASM backend の責務分離は維持する。

## Decision

### Tuple を第一級の structural type とする

次の tuple type と tuple value を導入する。

```sobakasu
let unit: () = ();
let one: (i32,) = (42,);
let pair: (i32, string) = (42, "hello");
let nested: ((i32, string), bool) = (pair, true);
```

`(T)` と `(value)` は従来どおり parenthesized type / expression であり、1 要素 tuple ではない。

```text
(T)  == T
(T,) != T
```

Tuple identity は要素型と順序だけで決まる。Tuple と struct は同一型にせず、名前付きデータには引き続き struct を使う。

```text
(i32, string) == (i32, string)
(i32, string) != (string, i32)
i32 != (i32,)
```

位置 access は `.0`、`.1` の形式とし、範囲外 index は compile error とする。`let` binding pattern は 0 要素、1 要素、複数要素、nest、discard を扱う。

```sobakasu
let ((number, _), flag) = ((42, "ignored"), true);
```

Initializer は一度だけ評価し、`_` は値を評価するが local symbol を宣言しない。Pattern の型と arity は Binder が検証する。

### 0 要素 tuple を unit type とする

`()` は tuple type と unit type の同一の型であり、compiler internal representation では `TupleType([])` とする。戻り値型を省略した関数と `-> ()` は同じ意味を持つ。`()` は値として記述できるが、Udon storage は持たない。

従来の `u0` keyword、型 symbol、互換 alias は廃止する。compiler source、standard library、sample、test、diagnostic、documentation は `()` に移行する。旧表記を使用した source は unknown type として明確に失敗する。

### Compiler internal representation と Udon lowering

Tuple は `TypeSymbol.Tuple(elements)` と aggregate field `0`、`1`、…で表現する。Binder で型解決と structural identity を確定し、IR Lowerer は既存の aggregate layout を利用して再帰的に leaf value へ投影する。Backend は tuple の意味解析や型解決を行わない。

```text
Sobakasu type              Udon physical representation

()                         0 slots
(T,)                       flatten(T)
(T, U)                     flatten(T) + flatten(U)
((i32, string), bool)      i32 slot + string slot + bool slot
```

`System.ValueTuple`、CLR tuple object、独自 runtime tuple object、array は生成しない。`i32` と `(i32,)` はどちらも物理的には 1 個の `SystemInt32` slot を使えるが、Sobakasu の型としては区別を保つ。

### `ref` / `out` は extern ABI signature に限定する

通常の Sobakasu 関数 parameter には `ref` / `out` を導入しない。ADR-0030 の declarative extern binding の右辺に限り、physical ABI signature を明示できる。

```sobakasu
fn foo(a: A, b: B) -> (R, B)
  = extern External.Api.Foo(A a, ref B b);

fn try_read(source: A) -> (bool, B)
  = extern External.Api.TryRead(A source, out B value);
```

右辺の modifier は Sobakasu の reference type ではなく、選択する extern overload の parameter passing mode である。Catalog は reflection から `normal`、`ref`、`out` を保持する。CLR `in` は必要な metadata を保持しつつ readonly の通常入力として扱い、独自の Sobakasu 構文は追加しない。Pointer は引き続き unsupported とする。

Logical input は次の順で構成する。

```text
normal parameter -> input
ref parameter    -> input + output
out parameter    -> output only
```

Logical output は通常 return value を先頭に置き、その後へ external parameter declaration order のまま `ref` / `out` の更新値を追加する。種類別に並べ替えない。

```csharp
R Foo(A a, ref B b, C c, out D d, ref E e)
```

```text
Sobakasu inputs  = A, B, C, E
Sobakasu outputs = R, B, D, E
```

Output type は個数で正規化する。

```text
0 outputs -> ()
1 output  -> T
2 outputs -> (T0, T1)
n outputs -> (T0, T1, ..., Tn-1)
```

この 1 出力規則は extern adapter だけに適用する。通常の Sobakasu 関数が宣言した `(T,)` を `T` へ正規化してはならない。Binder は左辺の logical input、logical output、型、個数、順序と、右辺の physical ABI signature を照合し、不一致を宣言 diagnostic とする。

### `Vector3.SmoothDamp` の例

Unity API の代表例を次のように公開する。

```sobakasu
pub impl Vector3 = extern UnityEngine.Vector3 {
  pub static fn smooth_damp(
      current: Self,
      target: Self,
      current_velocity: Self,
      smooth_time: f32,
      max_speed: f32,
      delta_time: f32,
  ) -> (Self, Self)
    = extern UnityEngine.Vector3.SmoothDamp(
        Self current,
        Self target,
        ref Self current_velocity,
        f32 smooth_time,
        f32 max_speed,
        f32 delta_time,
      );
}
```

利用側は reference semantics を意識しない。

```sobakasu
let (position, velocity) = Vector3.smooth_damp(
    current, target, velocity, smooth_time, max_speed, delta_time);
```

### Extern adapter の IR / UASM lowering

`ref` parameter は入力を一時 slot へ copy してから PUSH する。`out` parameter は一時 slot を確保するが、入力として初期化しない。通常 return slot は physical ABI の最後へ PUSH する。

`SmoothDamp` は概念的に次へ lower する。

```text
COPY current_velocity -> __ref_current_velocity

PUSH current
PUSH target
PUSH __ref_current_velocity
PUSH smooth_time
PUSH max_speed
PUSH delta_time
PUSH __return_value
EXTERN UnityEngine.Vector3.SmoothDamp

logical result = (__return_value, __ref_current_velocity)
```

`bool Foo(A a, out B value)` は次のように lower する。

```text
PUSH a
PUSH __out_value
PUSH __return_value
EXTERN External.Api.Foo

logical result = (__return_value, __out_value)
```

IR Lowerer は各 logical argument を一度だけ評価し、physical call slot と logical aggregate result を構成する。UASM Assembler は確定済みの physical argument / result slot を順に PUSH し、tuple object を生成しない。

### Catalog と stub generator

Reflection extern catalog は by-ref element type と passing mode を失わず保持し、logical signature を生成する。Stub generator は `ref` / `out` method を除外せず、左辺へ logical signature、右辺へ明示的 ABI signature を出力する。Optional CLR parameter は Sobakasu 側で通常の必須入力として公開し、default argument 構文は追加しない。

## Alternatives

### 通常関数へ reference parameter を導入する

Aliasing、lifetime、mutation の言語仕様が必要になり、extern 境界の適応という目的を大きく超えるため採用しない。

### `ref` / `out` ごとに手書き wrapper を要求する

Udon slot を source language から直接操作する仕組みが必要になり、catalog と生成 stub の完全性も失われるため採用しない。

### 複数出力専用の特殊型を導入する

通常コードで再利用できず、product type と aggregate flattening を二重実装するため採用しない。

### 1 出力も `(T,)` にする

ABI wrapper の利用者が単一値を毎回 destructure することになり、通常の値 API として不自然になるため採用しない。第一級 tuple の `(T,)` 自体は維持する。

### Runtime tuple object を生成する

Udon heap、GC、extern compatibility に不要な runtime 表現を持ち込み、既存の leaf-slot aggregate 方針に反するため採用しない。

## Rationale

1. Tuple を一般的な product type とすることで、extern adapter 以外の関数、local、引数、戻り値にも同じ型規則を使える。
2. `()` を 0 要素 tuple とすることで、unit、0 output、zero storage を一つの規則へ統合できる。
3. `ref` / `out` を ABI metadata に限定することで、Sobakasu の値モデルを単純に保てる。
4. Binder で logical / physical signature を確定し、IR Lowerer で slot adaptation を行うことで、backend に型解決を押し込まない。
5. 既存の aggregate layout を tuple に一般化することで、struct / enum の実績ある leaf flattening を再利用できる。
6. Catalog と stub generator が同じ metadata を使うため、手書き declaration と自動生成 declaration の規則が一致する。

## Consequences

### Positive

* `()`、`(T,)`、`(T, U, ...)` を通常の型と値として利用できる。
* Nested tuple、位置 access、`let` destructuring、discard を利用できる。
* Unit value は Udon storage を消費しない。
* Unity / CLR の `ref` / `out` API を通常の input / return API として公開できる。
* `Vector3.SmoothDamp` の return と更新 velocity を `(Vector3, Vector3)` として受け取れる。
* Tuple は既存 aggregate と同じ leaf-slot infrastructure へ lower され、runtime object を必要としない。
* Public compiler metadata から physical parameter type、passing mode、return type、extern signature を取得できる。

### Negative

* Parenthesized expression と 1 要素 tuple を trailing comma で区別する必要がある。
* Function call の Sobakasu signature と extern の physical ABI signature が異なるため、Binder と IR Lowerer の adapter logic が増える。
* `ref` / `out` overload の declaration は右辺に parameter type と modifier を重ねて記述する必要がある。
* 1 要素 tuple と裸の値が同じ 1 slot representation を共有するため、compiler は source-level type identity を lowering 後も誤って混同しないようにする必要がある。

## Non-goals

* 通常の Sobakasu 関数の `ref` / `out`
* Reference value、pointer、borrow、lifetime、ownership、aliasing control
* CLR `System.ValueTuple` または独自 runtime tuple object
* C# default argument expression の再現
* C# と完全互換の reference semantics
