# Value-type receiver mutability 調査の最終統合

対象は Unity 2022.3.22f1（887be4894c44）、VRChat SDK Base / Worlds 3.10.4。設計判断は [ADR-0064](../../src/content/docs/ja/adr/adr-0064-curated-value-type-receiver-mutability.md) に記録する。

## 入力と対応

Task 0 の `Temp/readonly/0.txt` が指定した [固定コミットの対象一覧](https://github.com/skytomo221/Sobakasu/tree/a86442bb8565e59e22ec6b8550a834c050bc8057/receiver-mutability-targets) を GitHub の読み取り API で取得した。Git 操作は行っていない。

| Worker | 対象 |
| --- | ---: |
| A | 412 |
| B | 459 |
| C | 448 |
| D | 468 |
| E | 469 |
| 合計 | 2,256 |

Worker の回答は `Temp/readonly/a.json`〜`e.json` と、それぞれの説明用 `.txt`。各回答の配列位置を元の担当 JSON の `types[].members[]` と照合し、件数、member、明示されている kind / parameters、および D の index が一致することを確認した。省略された overload・accessor 情報は元の JSON から保持し、名前や分類から復元していない。2,256件の structured selector はすべて一意で、現在の Generator physical model の実在 member に一対一で一致した。

Task 0 のメモは件数とファイルへの参照を示す。抽出プログラムや全抽出条件の記述は含まれないため、今回の固定された対象一覧を超える網羅性は主張しない。既存の `Vector3.GetHashCode()` と引数なし `Vector3.ToString()` は対象一覧に含まれない。

## 成果物

- [review.json](review.json): readonly/high 807件の反証レビュー。各候補を1回だけ列挙する JSON 配列。`worker` と1始まりの `sourceIndex` が入力位置を示す。
- [evidence.json](evidence.json): 対象環境で実配布された managed assembly の宣言、SHA-256、method token、IL。review の `evidence[].id` がこのオブジェクトのキーを参照する。重複する証拠は共有する。
- [integrated.json](integrated.json): 全2,256件の元の分類・confidence・理由・証拠記述と最終分類。元の簡略表記による証拠は Worker の主張として保持しており、独立レビューの証明とは区別する。
- [summary.json](summary.json): 入力ハッシュ、件数、catalog ハッシュ、config に追加した全563 selector、保持した既存2件の再確認根拠。削除は0件。

## 独立レビューの方法

一次調査の reason / evidence を readonly の証明には使わず、対象インストールの managed assembly を別途 IL 解析した。Unity の DLL は `D:/Unity/Editor/2022.3.22f1/Editor/Data/Managed/UnityEngine/`、runtime は同 Editor の `MonoBleedingEdge/lib/mono/unityjit-win32/`、SDK はプロジェクトの配布 package の DLL を使用した。依存解決もこの runtime とその Facades に固定し、監査プログラムを実行する .NET 8 の実装に置き換えていない。

対象の宣言は実際の host 型から base type をたどって解決した。Catalog が表す宣言元だけを信用して inherited member の実装を決めていない。型、accessor、parameter type / passing、generic arity の一致を確認した。

一時監査プログラムで制御フローと receiver のアドレスを追跡し、receiver の内部フィールドを含む書き込み先、アドレスの流出、間接呼び出し、native への委譲を調べた。分岐の合流ではアドレスを保持する可能性を残し、receiver のアドレスを受け取る managed callee は、その callee の処理まで再確認した。値コピー・参照先オブジェクト・元の struct の格納領域を区別する。receiver の格納領域に対する書き込みと未検証のアドレス流出がないことを確認できた候補だけ accept とした。これは調査を補助する解析であり、Generator に自動推測処理を導入したものではない。

例として `Bounds.IntersectRay` は native 呼び出しを含むが、元の receiver は `ldobj` でコピーされてから値引数として渡されるため accept できる。`PhysicsScene2D.Simulate` も scene の整数 handle を読み出して渡すだけであり、物理シーンに副作用があっても struct 自身を書き換えない。名前や副作用の有無で判定していない。

既存の `Vector3.GetHashCode()` と `Vector3.ToString()` も、実配布の `Single` メソッドを含む呼び出し先まで確認して保持した。[同じ Unity タグの公式 Vector3 実装](https://github.com/Unity-Technologies/UnityCsReference/blob/2022.3.22f1/Runtime/Export/Math/Vector3.cs) も参照した。

## 結果と保留

```text
Reviewed: 807
accept: 563
reject: 0
uncertain: 244
```

Worker の元の分類は readonly 1,146件（high 807、medium 339）、mutable 270件、uncertain 840件。readonly/medium 339件は今回の採用条件外で、Task R の対象にも含めていない。最終的な対象内の分類は readonly 563件、mutable 270件、uncertain 1,423件。mutable 270件は Worker 判定の維持であり、Task R が再調査した件数には含まない。

244件の uncertain には `Object.GetType` や `ValueType` の継承メソッド、`DataToken.Equals` / `GetHashCode` の一部が含まれる。対象 runtime の native 本体、または receiver 内部のアドレスを渡す virtual / constrained dispatch の非変更性を、この調査では保証しきれなかった。一般的な API 説明や override の不在だけを非変更 contract とみなしていない。mutable という反証を得たわけではないので reject にはしていない。個別の保留理由と宣言は review にすべて記録する。

config は563件を追加し、既存2件を保持して計565件とする。削除はない。元の全候補と現在の physical model との identity の矛盾はなかった。Unity / SDK バージョンが変わった場合は、実在 match のテストに加えて API 実装の根拠を再確認する必要がある。

## 検証

- `Scripts/run-standard-library-generator.ps1` による再生成は成功。754ファイル、696生成型。変更内容のある生成物は63ファイルで、旧configの一時再生成結果と比較して `mutable self` → `self` 以外の変更がないことを確認した。既存ファイルの LF / CRLF は変更前のハッシュと照合して維持した。
- generation report は全631 rule が match、unmatched rule は0。receiver の565 rule についても個別に単一 match と実際の `self` 描画をテストした。未指定の値型 instance member は `mutable self` のまま維持される。
- `dotnet test Sobakasu.StandardLibraryGenerator.Standalone.Tests.csproj` は74/74成功。入力とレビューの一対一対応、accept と既存保持分だけが config に入ること、全ruleの実在性、receiver 描画、および生成済みライブラリとの一致を含む。
- `Scripts/run-unity-tests.ps1 -TestFilter 'StandardLibraryGeneratorTests;UdonBindingMemberSelectorTests;UdonBindingGenerator' -BatchMode -NoGraphics -UseRtk` は198件中196成功、2失敗。結果は `Temp/readonly/unity-verified.xml`、log は `Temp/readonly/unity-verified.log`。

Unity の失敗は `UdonBindingGeneratorOperatorTests.Generator_DiscoversUdonOnlyPrimitiveOperators`（117行）と `UdonBindingGeneratorRenderingTests.Generator_DiscoversUdonOnlyPrimitiveOperators`（356行）。双方とも `public implementation i32 = extern System.Int32` を期待するが、現行の renderer は `implementation i32 { ... }` を出力するため失敗する。operator 本体と receiver の期待は一致している。

この2件は変更していない既存テストであり、`UdonBindingGeneratorTestSupport.CreateGenerator` → migration adapter の constructor から、`UdonBindingGenerationConfig.CreateDefault()` の空の既定設定を使用する。今回更新した bundled config、調査記録、生成済みファイルは読み込まない。production renderer / policy も今回変更していないため、今回の変更とは独立した fixture の構文期待不一致と判断し、無関係な修正を加えていない。

Compiler Core の変更はなく、専用の Compiler Core 全テストと VRChat クライアント上での実機試験は実施していない。生成 CLI による全bindingの検証と、Unity の関連テストに含まれる生成ライブラリの解析・コンパイルは実施した。
