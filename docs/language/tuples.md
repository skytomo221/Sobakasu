# タプルと unit

Sobakasu のタプルは、名前を持たない structural product type です。0 要素、1 要素、複数要素を同じ型機構で扱います。

```sobakasu
let unit: () = ();
let one: (i32,) = (42,);
let pair: (i32, string) = (42, "hello");
```

`(42)` は parenthesized expression であり `i32` のままです。1 要素タプルには trailing comma が必要です。

```text
(T)  == T
(T,) != T
```

## Access と destructuring

要素は 0 始まりの位置で参照できます。

```sobakasu
let pair = (42, "hello");
let number = pair.0;
let text = pair.1;
```

`let` では nested pattern と discard `_` を利用できます。

```sobakasu
let ((number,), _) = ((42,), "ignored");
```

右辺は一度だけ評価され、`_` に対応する値は local variable として宣言されません。型または要素数が一致しない pattern と、範囲外の位置 access は compile error です。

## Function

Tuple は通常の関数引数と戻り値に利用できます。戻り値型を省略した関数と `-> ()` は同じ unit return です。

```sobakasu
fn split(value: i32) -> (i32, string) {
  (value, "value")
}

fn finish() -> () {
  ()
}
```

## Extern の複数出力

Extern ABI の `ref` は入力と出力、`out` は出力として通常の戻り値へ変換されます。出力が 0 個なら `()`、1 個なら裸の `T`、2 個以上なら tuple です。

```sobakasu
fn update(value: i32) -> (bool, i32, string)
  = extern External.Api.Update(ref i32 value, out string message);
```

`ref` / `out` は extern binding の右辺だけに記述でき、通常の Sobakasu 関数 parameter には使えません。

## Udon representation

Tuple は Udon 上で object になりません。要素を再帰的に leaf slot へ flatten します。

```text
()                    -> 0 slots
(i32,)                -> 1 x SystemInt32 slot
((i32, string), bool) -> SystemInt32 + SystemString + SystemBoolean slots
```

詳細な型規則、extern output order、`ref` / `out` lowering は [ADR-0031](../adr/adr-0031-introduce-tuples-and-adapt-extern-ref-out-parameters-to-value-returns.md) を参照してください。

