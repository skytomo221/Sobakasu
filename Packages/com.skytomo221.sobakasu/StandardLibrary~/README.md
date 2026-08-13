# Sobakasu standard-library sources

This directory contains Sobakasu sources loaded directly by the compiler. It
is not imported as a collection of individual `SobakasuProgramAsset` files.

Logical module names map directly to paths below this directory. Replace each
`.` with `/` and append `.sobakasu`:

```text
example.math -> example/math.sobakasu
```

`prelude.sobakasu` is the one compiler-known special path. When present, its
public exports form the implicit Prelude for entry sources.

`Maybe<T>` is the standard representation for a value that may be absent. It
is exported by the Prelude and uses the ordinary generic enum, monomorphized
aggregate, and `match` machinery:

```sobakasu
let value: Maybe<i32> = Maybe.Just(42);
let empty: Maybe<i32> = Maybe.Nothing;
```

Reference-returning wrappers validate raw Udon references before constructing
`Maybe<T>`. For example, `use unity.GameObject;` exposes `GameObject.find`,
which calls `VRC.SDKBase.Utilities.IsValid` and returns `Maybe<GameObject>`.
Direct `extern` access remains available as the low-level escape hatch.

`example.math` is an internal verification module for the initial module
loader. It does not establish the permanent public standard-library root name.
Only convention paths reached through `use`, `mod`, or the built-in Prelude
path are compiler inputs. The compiler does not enumerate every source file.
