# ADR-0003: Sobakasu Compiler Pipeline Architecture

## Status

Accepted

## Context

Sobakasu は VRChat 向けに UdonAssembly (UASM) を生成する独自言語である。

UdonAssembly は以下の制約を持つ：

* 型制約が強い（例：型の制限や変換挙動の制約）
* スタックベース命令体系
* EXTERN 呼び出し署名が厳密
* 制御フロー表現が低レベル

AST から直接 UASM を生成する構成では、

* ターゲット制約がフロントエンド層へ漏れる
* 最適化が困難
* デバッグが難しい
* 将来的な拡張が制限される

そのため、段階的なコンパイル構造を明確に分離する必要があった。

## Decision

Sobakasu コンパイラは以下のパイプライン構成を採用する。

```txt
Lexer
↓ Token[]
Parser
↓ CST
Binder
↓ Typed AST
Desugar
↓ Normalized AST
IrLowerer
↓ IR (CFG + Three-Address Code)
Optimizer
↓ IR
UasmAssembler
↓ UASM (List<UasmInstruction>)
```

各段の責務は以下の通り。

### Lexer

* 文字列からトークン列を生成
* エラー回復を行う

### Parser

* トークン列から CST を生成

### Binder

* 名前解決
* 型付け
* 暗黙変換の明示化
* シンボル解決

### Desugar

* 構文糖衣の除去
* 複合構文の正規化

### IrLowerer

* AST をターゲット非依存 IR へ変換
* Control Flow Graph (CFG) 構築
* Three-Address Code 生成

IR は基本ブロック単位の Control Flow Graph を持ち、
各命令は Three-Address Code 形式で表現される。
SSA 形式は初期段階では採用しない。

### Optimizer

* IR レベルの最適化
* ターゲット非依存変換のみ実施

### UasmAssembler

* IR を UASM 命令列へ変換
* ヒープスロット割当
* ラベル解決
* EXTERN 署名確定
* 命令列を `List<UasmInstruction>` として返却

UasmAssembler はバックエンド変換専用とし、意味変換や最適化は行わない。

## Alternatives

### 1. AST から直接 UASM を生成

概要：

* Binder 後に直接 UASM 出力を行う。

問題点：

* ターゲット制約が上流へ波及する。
* 最適化が困難。
* 保守性が低い。
* デバッグが難しい。

### 2. Stack ベース IR のみを使用

概要：

* AST → Stack IR → UASM

問題点：

* CFG 操作が扱いづらい。
* 高度な最適化が困難。
* 意味追跡が困難。

### 3. UASM 互換 IR を最初から採用

概要：

* IR を UASM とほぼ同一構造にする。

問題点：

* ソース言語設計が UASM に強く拘束される。
* 将来的な別ターゲット対応が困難。

## Rationale

本構成を採用した理由は以下の通り。

1. **責務分離**

   * フロントエンドとバックエンドを明確に分離できる。
   * ターゲット依存ロジックを UasmAssembler に隔離できる。

2. **拡張性**

   * IR レベルでの最適化を段階的に拡張可能。
   * 将来的に別バックエンドを追加可能。

3. **デバッグ容易性**

   * AST → IR → UASM の対応が明確。
   * 段階ごとに診断可能。

4. **設計の一貫性**

   * Roslyn 等の実績あるコンパイラ構造に近い。
   * Lowerer / Optimizer / Assembler の責務が明確。

## Consequences

### Positive

* 明確な段階分離による保守性向上
* IR ベース最適化の導入が容易
* 将来的な拡張性の確保
* デバッグのしやすさ

### Negative

* 実装量が増加する
* 初期設計コストが高い
* 小規模機能追加でも段を跨ぐ修正が必要になる可能性がある

この構成を Sobakasu コンパイラの基盤設計として正式採用する。
