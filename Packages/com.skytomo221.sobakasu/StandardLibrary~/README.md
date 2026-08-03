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

`example.math` is an internal verification module for the initial module
loader. It does not establish the permanent public standard-library root name.
Only convention paths reached through `use`, `mod`, or the built-in Prelude
path are compiler inputs. The compiler does not enumerate every source file.
