# Sobakasu standard-library sources

This directory contains Sobakasu sources loaded directly by the compiler. It
is not imported as a collection of individual `SobakasuProgramAsset` files.

`manifest.json` maps logical module names to source paths:

```json
{
  "modules": [
    {
      "name": "example.math",
      "path": "example/math.sobakasu"
    }
  ]
}
```

`example.math` is an internal verification module for the initial module
loader. It does not establish the permanent public standard-library root name.
Only manifest-registered source files are compiler inputs.
