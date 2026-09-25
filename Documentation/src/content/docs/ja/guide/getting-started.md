---
title: Getting Started
description: SobakasuをVRChatプロジェクトで使い始める手順です。
---

Sobakasuパッケージを導入していない場合は、まず[インストール](../installation/)を完了してください。

## 1. プログラムを書く

UnityのProjectウィンドウで `.sobakasu` ファイルを作成し、次のコードを書いて保存します。

```sobakasu
on interact {
  log("Hello, world!");
}
```

Unityはファイルを自動的にimport・compileします。

## 2. Udon Behaviourへ割り当てる

必要なGameObjectへUdon Behaviourをアタッチし、Projectウィンドウの `.sobakasu` ファイルをProgram Sourceに割り当てます。VRChat上で対象をinteractするとログを出力します。

詳細は[Unity / VRChatでの使用](../unity-vrchat/)を参照してください。
