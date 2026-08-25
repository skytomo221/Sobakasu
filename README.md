# Sobakasu

Sobakasu は、VRChat の Udon VM 上で動作するプログラムを生成するために設計された、C#に依存しない高級言語及びシステムです。

![Thumbnail](./docs/images/thumbnail.png)

## 設計思想

* VRChat の Udon VM 上で動作するプログラムを生成するために設計されている (Udon-first)
* C# 文法の制約にとらわれず、Udon の特性に最適化された言語設計 (C#-independent)
* Unity Editor との密接な統合

### 非目標

* C#完全互換
* 汎用プログラミング言語

## はじめ方

1. <https://skytomo221.com/Sobakasu> からVCCを追加する
2. プロジェクトに Sobakasu を追加する
3. `.sobakasu` ファイルを作成
4. コードを書く
5. Unity Project ウィンドウで右クリックして Create -> VRChat -> Udon -> Sobakasu Program Asset を選択
6. Sobakasu Program Asset に作成した `.sobakasu` ファイルを割り当てる
7. Sobakasu Program Asset のインスペクターにある「Compile (Sobakasu -> UASM)」ボタンを押して、UASM が生成させる
8. UdonSharpと同様に、必要なオブジェクトに Udon Behaviour をアタッチし、Sobakasu Program Asset を割り当てる

## 例

### Hello World

```sobakasu
on interact {
  log("Hello, world!");
}
```

このコードはコンパイル後に VRChat 上で実行されます。
以下のUdonSharpコードと同等の機能を提供します。

```csharp
using UdonSharp;
using UnityEngine;

public class HelloWorld : UdonSharpBehaviour
{
    public override void Interact()
    {
        Debug.Log("Hello, world!");
    }
}
```

### Fizz Buzz

```sobakasu
state count = 1;

on interact {
  if count % 3 == 0 && count % 5 == 0 {
    log("FizzBuzz");
  } else if count % 3 == 0 {
    log("Fizz");
  } else if count % 5 == 0 {
    log("Buzz");
  } else {
    log(count);
  }
  count += 1;
}
```

このコードはコンパイル後に VRChat 上で実行されます。
以下のUdonSharpコードと同等の機能を提供します。

```csharp
using UdonSharp;
using UnityEngine;

public class FizzBuzz : UdonSharpBehaviour
{
    private int count = 1;

    public override void Interact()
    {
        count++;
        if (count % 3 == 0 && count % 5 == 0)
        {
            Debug.Log("FizzBuzz");
        }
        else if (count % 3 == 0)
        {
            Debug.Log("Fizz");
        }
        else if (count % 5 == 0)
        {
            Debug.Log("Buzz");
        }
        else
        {
            Debug.Log(count);
        }
    }
}
```

## 機能（現状）

> [!NOTE]
> Sobakasuは現在開発中のため、実用的な利用にはまだ向いていません。

* `extern` キーワードによる外部APIアクセス
  * static／instance method
  * property／field getter・setter
  * constructor
  * 外部単項・二項演算子
  * Udon公開候補だけを対象にしたoverload解決
  * 戻り値を持つ呼び出しと `()` 呼び出し
* `impl` と外部型binding
  * 暗黙の `self` と包含型を表す `Self`
  * `static fn` associated function
  * 引数0の場合の `()` 省略
  * メソッドoverload
  * 二項演算子と `@` 付き単項演算子のoverload
  * 組み込み型への追加 `impl`
* `StandardLibrary~` のSobakasu標準ライブラリ
  * manifestによる論理モジュール解決
  * `use some.module.declaration [as alias];`
  * 公開性、循環依存、alias衝突、import曖昧性の検査
  * 必要なモジュールを単一Udon Programへコンパイル時結合
  * `use` はSobakasuモジュール専用、外部APIは `extern` 専用
* `on interact()` イベントハンドラのサポート
* UdonSharp互換イベントハンドラのサポート
  * 引数付きイベント
  * 戻り値付きイベント（`OnOwnershipRequest`など）
  * イベントカタログによるイベント名・型・引数の検証
* `let x = 1;` のような変数宣言
  * `let mut x = 1;` のようなミュータブル変数宣言もサポート済み
  * トップレベル `let` によるイベント間で永続する状態変数
  * `pub` によるUdon公開変数
  * `sync`、`sync(linear)`、`sync(smooth)` によるUdon変数同期
* Rust風の関数宣言
  * `fn add(x: i32, y: i32) -> i32 { ... }`
  * `return` 文と末尾式による戻り値
  * 関数の前方参照と関数呼び出し
* 基本的な演算子
  * 算術、比較、論理、bitwise演算子
  * 代入・複合代入演算子
  * `&&` / `||` の短絡評価
* 式指向の制御フロー
  * `if`、`while`、`loop`
  * 値を返す `break`
  * `continue`、`redo`
  * loop labelによるネストしたloopの制御
* 複数のプリミティブ型
  * `bool`
  * `i8`
  * `u8`
  * `i16`
  * `u16`
  * `i32`
  * `f32`
  * `i64`
  * `u64`
  * `f64`
  * `char`
  * `string`

## ロードマップ

今後優先的に追加される機能は以下の通りです。

* Custom Eventの宣言・呼び出し
* 配列の完全サポート
* 高度なgeneric binding
* 標準ライブラリのAPI拡充と正式な公開ルート名の決定
* ユーザーモジュールの登録・解決
* 型エイリアス、コンパイル時定数、ユーザー定義値型
* indexerの呼び出し
* extension methodの呼び出し

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

Sobakasu は「既存言語をUdonに適応する」のではなく、
**Udonのために最初から設計された言語**です。
