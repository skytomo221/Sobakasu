# ADR-0036: Treat .sobakasu Files as Udon Program Sources

## Status

Accepted

## Context

Sobakasu の Unity Editor 統合には、次の二段階のアセットモデルが存在していた。

```text
.sobakasu
→ SobakasuSourceAsset
→ SobakasuProgramAsset
```

`SobakasuSourceAsset` は `.sobakasu` の内容を serialized field に複製し、ユーザーが別途作成した `SobakasuProgramAsset` がその Source Asset を参照していた。このため、ソースファイルとは別に Program Asset が存在し、両者の対応関係を管理する必要があった。Inspector に Source Asset を差し替える UI も必要となり、`.sobakasu` そのものを `UdonBehaviour` の Program Source として扱えなかった。

UdonSharp が利用する `.cs` は Unity の標準 C# import pipeline に入り、imported main object は `MonoScript` になる。この制約下では、C# source、`MonoScript`、Udon 向け program representation を単一の imported object にすることはできない。一方、Sobakasu は独自拡張子 `.sobakasu` とその `ScriptedImporter` を所有しており、main imported object を `MonoScript` にする制約がない。UdonSharp が必要とする可能性のある Source と Program の分離を Sobakasu に持ち込む必要はない。

また、VRChat SDK の `UdonProgramAsset.SerializedProgramAsset` は、値が未設定の場合に `Assets/SerializedUdonPrograms` への asset 作成、`AssetDatabase.SaveAssets`、`AssetDatabase.Refresh`、program refresh を行う。これを `ScriptedImporter.OnImportAsset` から呼ぶと、import 中の AssetDatabase 操作と recursive import を招くおそれがある。

## Decision

A `.sobakasu` file is itself a Sobakasu Udon program source.

概念モデルを次の形に統一する。

```text
Foo.sobakasu
    =
Sobakasu Program
    =
Udon Program Source
```

`.sobakasu` の `ScriptedImporter` は `SobakasuProgramAsset` を imported main object として生成する。ユーザー向けの別概念として `SobakasuSourceAsset` や、ユーザーが作成する `SobakasuProgramAsset.asset` は設けない。`SobakasuProgramAsset` という C# クラス名は Unity/VRChat SDK 統合の内部実装名としてのみ使用する。

`.sobakasu` ファイルを source text の唯一の source of truth とする。Importer は import 時にファイルを読み、source text を compiler に渡す。source text や source path を imported object に永続化しない。診断で source path や source text が必要な場合は、imported asset 自身から path を導出し、ファイルを読み直す。

Importer は次の固定identifierを持つ object を同一 import context に追加する。

```text
SobakasuProgram          (main object)
SerializedUdonProgram    (hidden internal sub-asset)
```

内部 `SerializedUdonProgramAsset` は `SobakasuProgramAsset.SerializedProgramAsset` として直接返す。compile、UASM assemble、heap patch、network metadata の適用後、`SerializedUdonProgramAsset.StoreProgram` で同じ import artifact 内へ program を保存する。Importer 内から `AssetDatabase.CreateAsset`、`SaveAssets`、`Refresh` は呼ばない。

compiler core は Unity Editor API を参照しない。Unity integration が source text を読み、既存の `SobakasuCompiler` へ渡し、返された compile result を `SobakasuProgramAsset` と内部 serialized program に適用する。

compile、assembly、heap patch のいずれかが失敗した場合も main object は生成する。diagnostic は `SobakasuProgramAsset` に保持し、内部 serialized program は null program で更新して、以前の有効な program を実行し続けない。

## Alternatives

- `SobakasuSourceAsset` と独立した Program Asset を維持する。
  Source と Program の対応管理、source text の複製、差し替え UI が残り、1 file = 1 Program Source というモデルを満たさないため採用しない。

- `.sobakasu` の main object を Source Asset とし、Program Asset を sub-asset にする。
  Project window から `.sobakasu` を Program Source として直接扱えず、ユーザー向けに二つの概念が残るため採用しない。

- import 後に独立した `SerializedUdonProgramAsset` を遅延生成する。
  VRChat SDK の標準動作に近いが、別 asset の作成と AssetDatabase refresh が必要になり、import/serialization の再入と孤立 asset の管理が増えるため採用しない。

- `EditorApplication.delayCall` で compile または serialization を遅延する。
  callback の順序に依存する状態を増やし、import artifact 内で完結できる処理を不必要に分割するため採用しない。

- UdonSharp の Source と Program の分離を踏襲する。
  `.cs` は Unity が `MonoScript` として import する一方、`.sobakasu` は Sobakasu が main object を決定できるため、同じ制約を前提にする理由がない。

## Rationale

1 `.sobakasu` = 1 Program Source とすることで、編集、import、compile、Program Source 設定、play の導線が最短になる。固定した main object identifier と asset GUID により、reimport で object が再生成されても `UdonBehaviour` の Program Source 参照を維持できる。

serialized Udon program を同じ import artifact の内部状態にすると、ユーザーから見える概念を増やさず、VRChat runtime が必要とする `AbstractSerializedUdonProgramAsset` も提供できる。`StoreProgram` 自体は serialized state の更新だけを行うため、AssetDatabase を再入させずに import 内で安全に完結できる。

この構成は ADR-0002 の Udon-first な Unity Editor integration と、ADR-0003 の compiler pipeline の責務分離を維持する。

## Consequences

### Positive

- `.sobakasu` を Project window から `UdonBehaviour` の Program Source に直接設定できる。
- Source Asset の作成、source text の複製、Source Asset 割り当て UI が不要になる。
- 独立した Sobakasu Program Asset の作成と対応関係の管理が不要になる。
- import 中に `AssetDatabase.CreateAsset`、`SaveAssets`、`Refresh` を呼ばずに Udon program を保存できる。
- main object と内部 serialized program の参照は reimport 後も安定する。
- compile error 時にも Program Source 参照と diagnostic を保ちながら、古い program を無効化できる。

### Negative

- `.sobakasu` の imported artifact は、main object に加えて非表示の serialized program sub-asset を内部に持つ。
- VRChat SDK の `UdonProgramAsset` が将来 internal serialized program を前提としない lifecycle に変更された場合、統合を再検証する必要がある。
- 旧 `SobakasuSourceAsset`、独立 `.asset`、既存 scene/prefab の参照は移行しない。
