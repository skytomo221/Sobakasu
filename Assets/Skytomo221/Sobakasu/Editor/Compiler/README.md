# Sobakasu Compiler の流れ

`SobakasuCompiler.CompileToUasm(string sourceText)` は、入力ソースを次の順で処理して Udon Assembly (`uasm`) を生成します。

1. `Lexer`（字句解析）
   - `SobakasuLexer` が文字列を `Token` 列に分解します。
   - 不正文字や閉じていない文字列などは `DiagnosticBag` に記録されます。

2. `Parser`（構文解析）
   - `SobakasuParser` が `Token` 列から構文木（`CompilationUnitSyntax`）を作ります。
   - トップレベルは `on` / `func` 宣言を読み取り、式は Pratt Parser で解析します。

3. `Binder`（意味解析）
   - `Binder` が構文木を型付きの `BoundProgram` に変換します。
   - ここで主に以下をチェックします。
   - 変数/関数の解決、型整合性、引数個数、`on` イベント名のマッピング、再帰呼び出し検出。

4. `Lowerer`（IR への変換）
   - `Lowerer` が `BoundProgram` を `IrProgram` に落とします。
   - `if` / `while` はラベルとジャンプ（`IrGoto*`）中心の形に変換されます。
   - `&&` / `||` の短絡評価もジャンプに展開されます。

5. `GenCode`（Udon Assembly 生成）
   - `UdonAssemblyCodeGen` が `IrProgram` から最終的な Udon Assembly テキストを出力します。
   - `.data_start/.data_end` と `.code_start/.code_end` を組み立て、`PUSH` / `COPY` / `JUMP` / `EXTERN` などを生成します。

## 補足（現状）

- コンパイル入口は `Assets/Skytomo221/Sobakasu/Editor/Compiler/SobakasuCompiler.cs` です。
- 現在の `CompileToUasm` は Diagnostics を返却していないため、エラーがあっても `CompileResult.Ok(...)` を返す実装になっています。
