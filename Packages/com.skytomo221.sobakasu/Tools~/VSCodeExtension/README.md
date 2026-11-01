# Sobakasu Language

Visual Studio Code language support for **Sobakasu** and **Udon Assembly (UASM)**.

Sobakasu is a Udon-first programming language and compiler designed for developing [VRChat](https://vrchat.com/) worlds without depending on C# or UdonSharp.

## Features

### Sobakasu

Language support for `.sobakasu` files:

* Syntax highlighting
* `//` line comments
* `/* ... */` block comments
* Bracket matching
* Automatic closing of brackets and quotes
* Automatic indentation

```sobakasu
state {
  count: i32 = 0;
}

behavior {
  on interact(state) {
    state.count += 1;
  }
}
```

### Udon Assembly

Syntax highlighting for `.uasm` files is also included.

```uasm
.data_start

.data_end

.code_start

    PUSH, 0x00000000
    EXTERN, "UnityEngineDebug.__Log__SystemObject__SystemVoid"

.code_end
```

## About Sobakasu

Sobakasu is a programming language and toolchain designed specifically for the VRChat Udon VM.

Its main goals are:

* **Udon-first** — designed around Udon rather than adapting an existing general-purpose language
* **C#-independent** — not constrained by C# syntax or language semantics
* **Unity integration** — intended to integrate directly with the VRChat world development workflow
* **Higher-level language features** — providing a more expressive language while compiling to Udon Assembly

Sobakasu is currently under active development and is not yet recommended for production use.

## Installation

Install **Sobakasu Language** from the Visual Studio Code Marketplace.

For local development, the extension can also be installed from a `.vsix` package:

```sh
code --install-extension sobakasu-<version>.vsix
```

## Supported File Types

| Language      | Extension   |
| ------------- | ----------- |
| Sobakasu      | `.sobakasu` |
| Udon Assembly | `.uasm`     |

## Compiler and Unity Package

This extension provides Visual Studio Code language support.

The Sobakasu compiler and Unity integration are distributed as part of the main Sobakasu project.

See the main repository for installation instructions and documentation:

[github.com/skytomo221/Sobakasu](https://github.com/skytomo221/Sobakasu)

## Documentation

* [Sobakasu website](https://skytomo221.com/Sobakasu/)
* [GitHub repository](https://github.com/skytomo221/Sobakasu)
* [Issue tracker](https://github.com/skytomo221/Sobakasu/issues)

## Development Status

Run the TextMate tokenization regression tests with Node.js and npm from this directory:

```sh
npm ci
npm test
npm run package:check
```

`npm run package` creates a local VSIX. Test and packaging tools are development dependencies only.

The tests use `vscode-textmate` and `vscode-oniguruma`, check the compiler's hard keyword vocabulary, and verify identifier coverage against its Unicode 16.0 XID tables. Identifier patterns use Oniguruma's `XID_Start` / `XID_Continue` properties with `_` and an unassigned-character (`Cn`) fallback: older engine Unicode tables can then keep newer Sobakasu identifiers intact. Four newer XID continuation characters (U+200C, U+200D, U+30FB, U+FF65) are also included explicitly. This intentionally permits some names that the compiler rejects; highlighting does not validate source code.

`maybe` is highlighted inside function extern bindings, including `maybe out` ABI parameters. Ordinary identifiers named `maybe` keep their identifier scope. Synchronization modes are highlighted only inside `sync(...)`.

Sobakasu and this extension are still under development.

Language syntax and editor support may change as the language evolves.

## License

MIT License
