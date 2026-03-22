# Sobakasu

Sobakasu は、VRChat の UdonAssembly（UASM）を生成するために設計された、Udonファーストの高級言語およびコンパイラです。

## 概要

Sobakasu は、Udon ロジックの開発体験を改善するために設計されています。

* 可読性の高い高級構文
* 段階的に分離されたコンパイラ構造
* Unity Editor との密接な統合

UASM を直接記述することによる保守性・可読性の問題を解決しつつ、
Udon に最適化された言語設計を提供します。

## 例

```sobakasu
on Interact() {
  Debug.Log("Hello, world!");
}
```

このコードは UASM にコンパイルされ、VRChat 上で実行されます。

## 機能（現状）

* イベント駆動スクリプト（`on Interact()`）
* メンバーアクセス（`.`）
* 関数呼び出し
* 文字列リテラル
* 基本的な式解析
* 診断（エラー報告）

## アーキテクチャ

Sobakasu は段階的なコンパイラパイプラインを採用しています。

```txt
Lexer
↓
Parser
↓
Binder
↓
Desugar
↓
IR (CFG + Three-Address Code)
↓
Optimizer
↓
UASM
```

この構造により：

* Udon の制約がフロントエンドに漏れない
* 最適化の余地を確保できる
* デバッグがしやすい

## 設計思想

* **最初から最後まで Udon ファースト**
* **C#互換を目的としない**
* **Unity ワークフローを前提とする**
* **シンプルさを優先（過剰な機能は後回し）**

Sobakasu は「既存言語をUdonに適応する」のではなく、
**Udonのために最初から設計された言語**です。

## はじめ方

1. <https://skytomo221.com/Sobakasu> からVCCを追加する
2. プロジェクトに Sobakasu を追加する
3. `.sobakasu` ファイルを作成
4. コードを書く
5. Unity Project ウィンドウで右クリックして Create -> VRChat -> Udon -> Sobakasu Program Asset を選択
6. Sobakasu Program Asset に作成した `.sobakasu` ファイルを割り当てる
7. Sobakasu Program Asset のインスペクターにある「Compile (Sobakasu -> UASM)」ボタンを押して、UASM が生成させる
8. UdonSharpと同様に、必要なオブジェクトに Udon Behaviour をアタッチし、Sobakasu Program Asset を割り当てる

## ステータス

🚧 開発初期段階

* Parser: 一部実装済み
* Binder: 未実装 / 設計中
* IR: 未実装
* UASM生成: 開発中
* Unity統合: 実験段階

## ロードマップ

* [ ] 式の完全サポート
* [ ] 型システム（Binder）
* [ ] IR / CFG 実装
* [ ] UASMコード生成
* [ ] LSP（エディタ補完）

## 非目標

* C#完全互換
* 汎用プログラミング言語
