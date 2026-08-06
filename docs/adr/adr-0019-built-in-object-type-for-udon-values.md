# ADR-0019: Built-in `object` Type for Udon Values

## Status

Accepted (partially superseded by ADR-0020)

## Context

SobakasuはUdon-firstの言語であり、Udon externには`System.Object`を引数に取るAPIがある。従来のコンパイラには`TypeSymbol.Object`、CLRの`System.Object`からそのシンボルへの対応、extern候補選択時の大きな変換距離が部分的に存在していた。しかし、`object`は組み込み型名として解決できず、変換はextern候補選択だけの特例だったため、ローカル、通常関数、戻り値、トップレベル状態を一貫して扱えなかった。

## Decision

### 組み込み型としての位置付け

`object`をSobakasuの正式な組み込み参照型とする。ソース名は`object`、runtime／ABI名は`System.Object`、UASMデータ型は`SystemObject`である。現在の型等価性、CLR型マッピング、外部バインディングとの区別を維持できるため、専用の`TypeKind.Object`は追加せず、`TypeKind.Named`かつ`IsBuiltIn == true`の単一の`TypeSymbol.Object`として表現する。

`object`はUdonで表現可能な値を保持し、受け渡すための汎用値型である。C#互換の完全な型階層、`dynamic`、型検査の無効化、runtime reflection、実行時型に基づく動的メンバー解決ではない。

`object`と`UnityEngine.Object`は異なる型である。

```text
object
  -> System.Objectに対応するSobakasu組み込み型

UnityEngine.Object
  -> Unityランタイムオブジェクトの基底型
```

### 暗黙のboxing変換

期待型が`object`であるとき、次の具体型からの暗黙boxing変換を許可する。

* `bool`、`char`
* すべての組み込み整数型と浮動小数点型
* `string`
* `null`
* Udonへ公開された外部バインディング型
* コンパイラがUdonストレージとしてloweringできるその他の具体的なnamed型

`u0`、内部`Never`、エラー型、namespace／module／method group疑似型、値としてloweringできない内部型はboxing対象にしない。`Never`とエラー型に既存の到達不能・エラー回復規則が適用される場合も、それをboxingとは扱わない。

Binderは通常の代入・引数・戻り値の変換判定でboxingを確定する。IR Lowererは解決済みの対象型から`SystemObject`のローカル、引数、戻り値、一時領域、状態スロットを作り、既存の`COPY`で値を格納する。Udonのheap変数は初期型を持つ一方で実行時に値の型を保持でき、`COPY`はheap値を宛先へ複製するため、新しいboxing専用IR命令は導入しない。extern呼び出しでは、Binderが選択済みの`System.Object`シグネチャへUdonのheap値を渡す。UASM Assemblerはソース型やCLR型を再解決しない。

変換距離は完全一致や既存の具体的変換より大きくし、具体的な型一致を優先する。ただし、現時点のSobakasuにはユーザー向けオーバーロード機能を導入しない。

### 逆方向の変換と型推論

`object`から具体型への暗黙変換は禁止する。このADRでは、明示的unboxing、`as`、`is`、型パターン、checked cast、runtime type testを導入しない。

期待型のない式を`object`へ変換しない。`let value = 123;`は従来どおり`i32`である。`if`分岐、`loop`の`break`型、配列を含む通常の型統一で、異なる型を共通の`object`へまとめない。boxingは型注釈、引数、戻り値など、期待型が明示された境界だけで適用する。

### 利用位置、状態、同期

`object`はローカル変数の型注釈、単純代入、関数と`impl`メソッドの引数・戻り値、トップレベル状態、extern引数・戻り値との照合で利用できる。

トップレベル状態では`let mut value: object = null;`を許可し、実行時の代入では通常のboxing規則を使用する。現在のpost-assemble heap patch manifestは具体的な`TypeKind`だけを永続化し、boxed値の元の型情報を保持できない。そのため、非`null`の`object`状態初期値は安全に復元できる形式が決まるまで診断`SBK2090`で拒否する。誤ったUASMや復元不能なpatchは生成しない。

`object`はUdon同期可能型へ追加しない。`sync let mut value: object = null;`は同期非対応型として拒否する。同期しない`pub`状態は既存規則に従う。

### 静的メンバー解決

メンバーとexternは引き続きBinderで静的に解決する。`object`へ格納された値の実行時型を使って、新しいメンバーやextern候補を探索しない。静的な`System.Object`の型情報から解決可能なメンバーがある場合だけ、その通常の静的解決を適用できる。

### 標準ライブラリ

Preludeは`log`、`warning`、`error`を暗黙に公開し、それぞれ`object`引数を対応する`UnityEngine.Debug` externへ渡す。戻り値は`u0`である。標準ライブラリのモジュール構造と規約ベースの読み込み方式は変更しない。

### 対象外

このADR単独では配列と`object[]`を対象外とする。この制限はADR-0020により部分的にsupersedeされ、期待型付き`[object]`のboxingと、boxing元型を保持する配列heap patchが導入される。期待型のない異種配列推論、`object`からのunboxing、単独の非`null` `object`状態初期値の制限は維持する。

明示的unboxing、runtime type test、dynamic dispatchも対象外であり、必要になった場合は別ADRで決定する。

### 既存ADRとの関係

* ADR-0003: Binderで型と変換を確定し、IR／UASMは解決済み情報を出力する責務分離を維持する。
* ADR-0005: 既存の組み込み型集合へ`object`を追加する。数値型の仕様は変更しない。
* ADR-0007: ローカル`let`の型注釈と推論済み変換先として`object`を利用可能にする。`let`と`mut`の意味は変更しない。
* ADR-0011: 関数の引数と戻り値で`object`を利用可能にする。関数呼び出しモデルは変更しない。
* ADR-0013: `object`を共有可能な参照として扱い、Rust風所有権は導入しない。
* ADR-0014: トップレベル状態では利用可能だが、Udon同期型には含めない。
* ADR-0016: ABI型を`System.Object`とし、extern解決はBinderで完了させる。runtime reflectionとdynamic dispatchは導入しない。
* ADR-0017: Sobakasu標準ライブラリから`object`を使ったUdon APIラッパーを提供する。

これらのADRをSupersededにはしない。本ADRは組み込み型集合、変換規則、extern境界を必要な範囲で拡張する。

## Alternatives

1. extern候補選択だけで任意の値を`System.Object`へ適合させる。通常関数、ローカル、戻り値で一貫せず、backendが異なる型の意味を推測することになるため却下した。
2. boxing専用のBound／IR命令を追加する。Udonの既存`COPY`とextern引数渡しでheap値を安全に扱えるため、現時点では追加の命令を持つ利点がない。
3. `TypeKind.Object`を追加する。現行のnamed型等価性、ABI名生成、extern catalogと自然に統合でき、heap patchの非`null` boxed値には別途元型の永続化が必要なため、専用enumだけを追加しても問題を解決しない。
4. `object`をすべての型推論の共通型にする。暗黙の型拡大が増え、配列や制御フローの既存の完全一致中心の型統一を変更するため却下した。
5. `object`から具体型への暗黙unboxingを許可する。実行時検査と失敗規則が必要で、静的型安全性を損なうため却下した。

## Rationale

単一のbuilt-inシンボルと期待型境界でのboxingにより、Sobakasuの静的型検査を維持しながらUdonの`System.Object` APIを利用できる。Binderで変換を確定し、既存の型付きストレージと`COPY`へloweringすることで、ADR-0003の責務分離を保てる。型推論と配列の統一規則を変えないため、`object`の導入が既存プログラムの推論結果を変えない。

## Consequences

### Positive

* Sobakasuソースで`object`を通常の組み込み型として記述できる。
* primitive、文字列、外部バインディング値を通常関数とexternの両方へ渡せる。
* 標準ライブラリのログ関数が値の具体型ごとの重複なしに利用できる。
* 逆方向変換、同期、動的メンバー解決は静的に拒否される。
* 配列と通常の型推論の既存挙動を維持できる。

### Negative

* `object`から値を取り出す言語機能はまだない。
* 非`null`の`object`状態初期値は、heap patch manifestがboxed値の元型を保持できるようになるまで利用できない。
* `object`はUdon同期変数として利用できない。
* boxingには大きな変換距離を割り当てる必要があり、将来オーバーロードを正式導入する際には選択規則を改めて仕様化する必要がある。
