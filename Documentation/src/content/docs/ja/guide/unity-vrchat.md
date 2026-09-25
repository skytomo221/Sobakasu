---
title: Unity / VRChatでの使用
description: Unity EditorとVRChat Worlds SDKでSobakasuを使用する方法です。
---

SobakasuはUnity Editorとの統合を前提にしています。`.sobakasu` を保存するとUnityがimportし、生成されたProgram AssetをUdon BehaviourのProgram Sourceとして利用できます。

## 必要な環境

- VRChat Creator Companion (VCC) またはALCOM
- Unity 2022.3.22f1
- VRChat Worlds SDK 3.10.4以上

## 開発時の注意

Sobakasuは現在開発中です。利用できるUdon APIは、インストール済みSDKが公開するbindingに依存します。特に配列などのABI制約は、[言語リファレンス](../../language/arrays/)を確認してください。

Unityプロジェクト内でのコンパイルエラーは、対象の `.sobakasu` ファイルを修正して保存すると更新されます。
