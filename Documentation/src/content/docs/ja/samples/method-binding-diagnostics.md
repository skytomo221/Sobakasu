---
title: Method Binding Diagnostics
description: メソッドbindingの診断例です。
---

コンパイル可能な確認用サンプルは [`method-binding-smoke.sobakasu`](../method-binding-smoke.sobakasu) です。

## Undefined Name

```sobakasu
on interact() {
  Foo.Bar();
}
```

Expected diagnostic: `SBK2002`

## Undefined Member

```sobakasu
on interact() {
  Debug.NoSuchMethod();
}
```

Expected diagnostic: `SBK2003`

## Invalid Argument Count

```sobakasu
on interact() {
  Debug.Log();
}
```

Expected diagnostic: `SBK2004`

## No Matching Overload

```sobakasu
on interact() {
  Debug.Log([1]);
}
```

Expected diagnostic: `SBK2013`
