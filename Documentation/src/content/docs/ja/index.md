---
title: Sobakasu ドキュメント
description: VRChatのUdon VM向けに設計されたSobakasuの公式ドキュメントです。
template: splash
hero:
  title: Sobakasu
  tagline: VRChatのUdon VM向けに設計された、Udon-firstの言語とコンパイラ。
  actions:
    - text: VCCに追加
      link: vcc://vpm/addRepo?url=https%3A%2F%2Fskytomo221.com%2FSobakasu%2Findex.json
      variant: primary
      icon: external
    - text: はじめる
      link: ./guide/getting-started/
      variant: secondary
      icon: right-arrow
    - text: GitHubで見る
      link: https://github.com/skytomo221/Sobakasu
      variant: minimal
      icon: external
---

## Sobakasuについて

Sobakasu はVRChatのUdon VM上で動作するプログラムを生成するための、C#に依存しない高級言語およびシステムです。Unity Editorと統合し、`.sobakasu` ファイルをUdon BehaviourのProgram Sourceとして使用できます。

## ドキュメント

- [ガイド](./guide/getting-started/) — 導入からUnity / VRChatでの利用まで
- [言語リファレンス](./language/arrays/) — 構文、型、意味論、言語機能
- [Standard Library Reference](./reference/standard-library/) — 公開APIリファレンスの正式な配置先
- [サンプル](./samples/arrays/) — 実行可能なコード例と解説

## VPM repository

VCCへ追加するrepository URLは [`https://skytomo221.com/Sobakasu/index.json`](https://skytomo221.com/Sobakasu/index.json) です。

## Architecture Decision Records

開発上の設計判断は、リポジトリの [ADR正本](https://github.com/skytomo221/Sobakasu/tree/main/docs/adr) で管理しています。
