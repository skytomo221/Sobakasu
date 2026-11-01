# Maybe nullability 調査の最終成果物

reference-like output は保守的な既定値として `Maybe<T>` にします。API contract と対象配布物の実装から、正常に戻るすべての経路で外側の出力が non-null と積極的に確認できたものだけを、curated metadata として nonMaybe 化しました。分類基準は `semantics.json`、設計判断は [ADR-0065](../../src/content/docs/ja/adr/adr-0065-curated-api-output-nullability.md) を参照してください。

## 対象環境と範囲

- Unity 2022.3.22f1
- VRChat SDK Base / Worlds 3.10.4

環境と証拠 assembly の identity・SHA-256 は `source-manifest.json` に記録しています。Unity の配布 Mono/BCL・managed assembly と SDK の配布 assembly を使用し、host .NET の実装を代替証拠にはしていません。証拠のパスは Unity インストールルートを `$UNITY_EDITOR`、リポジトリルートを `$REPOSITORY` として表記します。Task 0 のソースハッシュは統合前の snapshot であり、最終 config のハッシュは `task-f-verification.json` の `generatedAudit.configSha256` にあります。

候補の範囲は、保存済み catalog から投影される physical member の参照型に相当する return / out と、その出力型の例外候補です。生成対象外の候補も含みます。全 Udon API・未モデル化 signature・配列 capability の網羅や、将来の Unity / SDK への適用を保証するものではありません。

判定対象は外側の出力です。CLR null、Unity の overloaded-null / destroyed state、native resource の生存性は区別します。配列の要素や入れ子の値の non-null は保証しません。

## 調査と採用件数

Task 0 の候補抽出後、Worker A〜E による一次分類と、non-null / high の独立レビュー、selector と実際の生成対象の検証を行いました。

| 一次分類 | 件数 |
| --- | ---: |
| non-null | 125（high: 121、medium: 4） |
| nullable | 784 |
| conditional | 378 |
| uncertain | 4,361 |
| 合計 | 5,648 |

| 独立レビュー | 件数 |
| --- | ---: |
| 対象 | 121 |
| accept | 112 |
| reject | 0 |
| conditional | 0 |
| uncertain | 9 |

レビューの accept 112件から、exact selector が複数の physical target に一致する8件と、params により generation target 外だった6件を除き、最終 config には98件を採用しました（112 - 8 - 6 = 98）。review uncertain 9件、Worker medium / low、nullable / conditional / uncertain は採用していません。

最終 config の追加は次のとおりです。既存ルールの削除はありません。

```text
nonMaybeTypes   +0
nonMaybeReturns +98
nonMaybeOuts    +0
```

`accepted.json` の `entries` はこの98件だけです。各 entry に candidate ID、exact selector、physical host / member identity、出力位置・型、Worker 判定・confidence・根拠、独立レビュー結果・反例探索・根拠、採用理由を収録しています。証拠は `sourceManifestRef` から manifest の `evidenceAssemblies` へ対応し、method signature、metadata token、return offset、必要な initializer / field と profile 比較結果を追跡できます。全量 IL dump や一次調査 JSON がなくても、採用理由を読めます。

## 最終検証

- 再生成754ファイル中、21ファイル・98宣言が `Maybe<T>` → `T` に変化。不要な Maybe import 1件を除去。
- unexpected generated diff: 0、unmatched config rules: 0、unintended multi-match: 0。
- physical coverage と out projection は不変。採用ルールは各1宣言へ反映。
- standalone: 80 / 80成功。追加回帰テストの個別実行: 6 / 6成功（80件にも含む）。
- Unity 関連テスト: 204件中202成功・2失敗。追加回帰テスト6件はすべて成功。

Unity の2失敗は既存 primitive operator fixture の宣言ヘッダー期待値の不一致です。両テストは bundled config を読み込まず、空の既定 config と値型 i32 の演算子を使うため、今回の nonMaybe 追加とは独立しています。失敗名、期待値・実際値、関係するソースと確認理由は `task-f-verification.json` に記録しています。失敗を成功扱いにはしていません。

VRChat client、Linux/macOS での実行、Unity 全件テストは未検証です。インストール済み9種類の Mono profile の IL 比較は実行検証を代替しません。公開戻り値の型変更により、Maybe として照合していた利用コードには修正が必要です。

これらは Task F 時点の生成・テスト結果です。今回の成果物整理ではコードや config を変更せず、件数、config 対応、証拠ハッシュ、保存構成を検証しています。

## 保存するファイル

このディレクトリのコミット対象は次の6ファイルだけです。

| ファイル | 内容 |
| --- | --- |
| `README.md` | 調査方法、範囲、集計、成果物と検証結果の説明 |
| `semantics.json` | Task 0 で固定した nullability classification、confidence、evidence policy、保守的な既定値 |
| `source-manifest.json` | 対象環境、ソース metadata、証拠 assembly の identity・hash |
| `accepted.json` | 最終 config 採用98件と、その根拠・正確な metadata identity |
| `summary.json` | 抽出・一次分類・review・integration の集計と Task 0 / F の検証要約 |
| `task-f-verification.json` | config validation、generation diff、テスト結果・失敗理由・未検証範囲 |

Candidate dump、Worker shard、一次調査全文、全量 IL、生成 patch、build/test intermediate artifacts は repository size を抑えるため保存しません。最終採用ルールの根拠は `accepted.json` に集約しています。生テストログのハッシュは由来の識別用に残し、ログ自体は repository に保存しません。調査用スクリプトは固定環境・一時 workspace・今回の証明ケースに依存するため保存対象外です。

## 根拠を再確認する手順

1. `semantics.json` の分類・証拠方針を確認します。
2. 対象 Unity / SDK 配布物を用意し、manifest の SHA-256 と assembly identity を照合します。別バージョンや host .NET で代用しません。
3. `accepted.json` の各 selector・member / output identity と config の `nonMaybeReturns` を照合します。対応 assembly の signature / token と記録された経路・callee / initializer の根拠から判定を再確認します。
4. `task-f-verification.json` の generation コマンドで一時診断出力を取り、generation audit と照合します。同ファイルのコマンドと filter でテストを再実行します。生成は作業コピーの標準ライブラリを更新するため、再検証用の作業コピーを使います。

この保存セットは採用98件の監査・再確認用です。5,648件すべての一次分類を自動再実行するツールや、nullability の一般的な自動証明器は含みません。Unity / SDK 更新時は新しい配布物に対して証拠と selector の適用範囲を改めて調査する必要があります。
