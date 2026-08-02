# AIエージェント向け作業指示

## 適用範囲

このファイルが置かれたリポジトリルート以下のすべてのファイルに適用する。

より深いディレクトリに `AGENTS.md` が存在する場合、そのディレクトリ以下では、より深い `AGENTS.md` の指示を優先する。

## リポジトリ構成

Sobakasu本体のUnityパッケージは、次のディレクトリにある。

```text
Packages/com.skytomo221.sobakasu
```

特に指示がない限り、Sobakasuのコンパイラ、Unity Editor統合、テスト、パッケージ設定を調査・変更する場合は、最初にこのディレクトリを確認すること。

リポジトリ全体を無差別に探索する前に、次の順序で関連ファイルを探すこと。

1. `Packages/com.skytomo221.sobakasu`
2. `docs`
3. `ProjectSettings`
4. リポジトリルートの設定ファイル

次のUnity生成ディレクトリは、明示的に必要な場合を除いて検索・編集しないこと。

```text
Library
Temp
Logs
obj
UserSettings
```

## プロジェクト方針

Sobakasuは、VRChatのUdon VMを対象とするUdon-firstのプログラミング言語およびコンパイラである。

設計・実装では次を前提とすること。

* C#完全互換を目的としない
* Udonのために設計された言語である
* Unity Editorとの統合を重視する
* 既存のADRに記録された決定を尊重する
* 新しい設計判断が既存ADRと矛盾しないか確認する
* Parser、Binder、IR、UASM backendの責務を安易に混在させない
* backendへ意味解析や型解決を押し込まない

## 作業開始時

変更を始める前に、関連する実装、テスト、ADRを確認すること。

ファイル名だけで判断せず、既存コードの責務と呼び出し関係を確認してから変更すること。

新しい構文または言語機能を追加する場合は、必要に応じて次の層を確認すること。

```text
Lexer
Parser
Binder
Desugar
IR Lowerer
Optimizer
UASM Assembler
Unity Editor integration
Tests
```

一部の層だけを変更して完了と判断しないこと。

## 変更方針

* 要求と無関係なリファクタリングを行わない
* 既存の命名規則とコードスタイルを維持する
* 公開APIやシリアライズ形式を変更する場合は互換性を確認する
* 一時的なハードコードを追加する前に、既存のcatalog、symbol、型解決機構を再利用できないか確認する
* エラーを握りつぶさず、既存の診断機構を使用する
* 生成物やUnityキャッシュをコミットしない
* タスクと無関係なファイルを変更しない

## ADR

アーキテクチャまたは言語仕様に関する判断を変更する場合は、`docs` 内の既存ADRを検索すること。

既存ADRと異なる設計を採用する場合は、次のいずれかを行うこと。

* 既存ADRを更新する
* 既存ADRをSupersededにする新しいADRを追加する
* 変更がADRを必要としない理由を最終報告に記載する

ADRだけを作成するタスクでない限り、ADRの作成だけで終了せず、要求されている実装とテストも行うこと。

## テストと検証

変更した機能に対応する既存テストを実行すること。

新しい構文、型規則、lowering、UASM出力を追加した場合は、該当する層のテストを追加または更新すること。

実行可能なテストが存在する場合、変更後に実行すること。

テストを実行できなかった場合は、次を最終報告に明記すること。

* 実行できなかったテスト
* 実行できなかった理由
* 代わりに行った検証

テスト失敗を、変更と無関係であるという根拠なしに無視しないこと。

## PowerShell 5.1

この節は、実行環境のシェルがWindows PowerShell 5.1の場合にのみ適用する。

PowerShellはプロファイルを読み込まずに実行すること。

```powershell
powershell.exe -NoProfile
```

日本語を含むコマンド出力を扱う場合は、コマンドの先頭でUTF-8を設定すること。

```powershell
[Console]::InputEncoding = [Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$OutputEncoding = [Text.UTF8Encoding]::new($false)
chcp 65001 > $null
```

単一コマンドとして実行する必要がある場合は、次の形式を使用すること。

```powershell
[Console]::InputEncoding=[Text.UTF8Encoding]::new($false); [Console]::OutputEncoding=[Text.UTF8Encoding]::new($false); $OutputEncoding=[Text.UTF8Encoding]::new($false); chcp 65001 > $null; & { <COMMAND> }
```

## ファイルエンコーディング

既存ファイルの改行コードとエンコーディングを可能な限り維持すること。

新しいテキストファイルは、特に指定がない限りUTF-8 BOMなしで保存すること。

Windows PowerShell 5.1では、`-Encoding utf8` がUTF-8 BOM付きになるため、BOMなしで保存する場合は次を使用すること。

```powershell
$encoding = [Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText($path, $content, $encoding)
```

既存ファイルを部分編集できる場合は、ファイル全体を不必要に再生成しないこと。

## 最終報告

作業完了時は簡潔に次を報告すること。

* 変更した内容
* 主要な変更ファイル
* 実行したテストと結果
* 残っている制約または未検証事項
