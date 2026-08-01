# ADR-0013: Do Not Introduce a Rust-Style Ownership System

## Status

Accepted

## Context

Sobakasu は、VRChat Worlds SDK の Udon を対象とする Udon-first の高級言語である。

Sobakasu では、Rustに由来する次の設計をすでに採用している。

* `let`によるローカル束縛
* 不変を既定とする変数宣言
* 再代入を許可する場合の明示的な`mut`
* `i32`、`f32`などの固定幅型名
* 式指向の制御構文
* Rust風の`fn`やループラベル

このため、配列、コンテナ、将来のユーザー定義型などに対して、Rustと同様の所有権、move、借用、ライフタイム検査を導入する案が検討された。

Rustの所有権システムは、値の所有者を原則として一つに限定し、所有権の移動や借用をコンパイル時に検証する。

```rust
let a = String::from("hello");
let b = a;

// aは以後使用できない
```

しかし、Udonで扱う参照値の多くはSobakasuプログラム自身が所有するメモリではない。

代表的な型には次がある。

```text
GameObject
Transform
Material
Unity Component
VRCPlayerApi
UdonBehaviour
DataList
配列
その他のUnity / VRChatランタイムオブジェクト
```

`GameObject`や`Transform`などはUnityランタイムによって管理される。Sobakasuの変数はこれらへの参照を保持するだけであり、通常、そのメモリの確保、解放、寿命管理について責任を持たない。

```sobakasu
field target: GameObject;
```

この`target`をSobakasuが所有していると解釈することは、実際のランタイムモデルと一致しない。

また、UdonBehaviourはイベントをまたいで状態を保持する。

```sobakasu
field mut values: [i32] = [];

on Start() {
  values = [1, 2, 3];
}

on Interact() {
  Debug.Log(values[0]);
}
```

フィールドに対する所有権移動を許可した場合、あるイベントでフィールドがmoveされた後、別のイベントからそのフィールドを使用できるかを静的に判断する必要がある。

```sobakasu
field mut values: [i32] = [];

on Interact() {
  if condition {
    consume(move values);
  }
}

on Update() {
  use(values);
}
```

この検査には、Unityイベントの呼び出し順、繰り返し実行、ネットワークイベント、条件分岐、再代入などを含むオブジェクト全体の状態遷移解析が必要になる。

さらに、条件によって所有権が失われるフィールドを表現するには、`Option<T>`、typestate、明示的な再初期化などの追加機構が必要になる。

これはUdon向けスクリプト言語として大きな複雑性を持ち込む一方、UdonVM自体の実行安全性やメモリ管理を大きく改善するものではない。

## Decision

Sobakasuは、Rust風の所有権システムを導入しない。

具体的には、次の言語機能を基本仕様には導入しない。

* 値ごとの単一所有者
* 暗黙または明示的な所有権移動
* move後の変数使用禁止
* `&T`および`&mut T`に相当する借用
* 借用期間の静的検査
* ライフタイム注釈
* borrow checker
* フィールドやイベント間のtypestate解析
* 所有権に基づく自動的な破棄またはリソース解放

Sobakasuでは、所有権の代わりに、型の分類と代入時の意味を明示する。

### 値の分類

Sobakasuの値は、少なくとも次のカテゴリに分類する。

#### 1. Copy値

代入時に値そのものをコピーする。

対象には、次のようなプリミティブ型を含む。

```text
bool
char
i8 / i16 / i32 / i64
u8 / u16 / u32 / u64
f32 / f64
```

型カタログ上でCopy可能と定義された値型も、このカテゴリに含められる。

```sobakasu
let a = 10;
let b = a;
```

この場合、`a`と`b`は独立した値を持つ。

#### 2. 共有可能な不変参照

参照は共有されるが、参照先の値を変更しない型をこのカテゴリに含める。

`string`は共有可能な不変参照として扱う。

```sobakasu
let a = "hello";
let b = a;
```

この代入によって所有権は移動せず、`a`も引き続き使用できる。

Rustの`String`と`&str`に相当する区別は導入しない。

#### 3. 共有可能な可変参照

配列や可変コンテナは、代入時に参照を共有する。

```sobakasu
let a = [1, 2, 3];
let b = a;
```

この場合、`a`と`b`は同じ配列を参照する。

```sobakasu
b[0] = 100;
Debug.Log(a[0]);
```

配列要素の変更が許可されている場合、`a[0]`からも変更後の値が観測される。

`DataList`などの可変参照型も、原則として同じ共有参照の意味を持つ。

#### 4. ランタイム所有参照

UnityおよびVRChatランタイムが管理するオブジェクトは、ランタイム所有参照として扱う。

対象には、少なくとも次を含む。

```text
UnityEngine.Object
GameObject
Transform
Material
Unity Component
VRCPlayerApi
UdonBehaviour
```

Sobakasuの変数はこれらを所有せず、参照だけを保持する。

```sobakasu
let a = target;
let b = a;
```

この代入によって所有権は移動しない。`a`と`b`は同じランタイムオブジェクトを参照できる。

オブジェクトの破棄は所有権の終了ではなく、Unity APIへの明示的な操作として扱う。

```sobakasu
Object.Destroy(target);
```

破棄済みUnityオブジェクトの特殊なnull挙動についても、Rust風ライフタイムによる安全性保証は行わない。

### `let`と`mut`の意味

`let`と`mut`は、所有権ではなく束縛の再代入可能性を表す。

```sobakasu
let value = 10;
let mut index = 0;
```

`mut`のない束縛には再代入できない。

```sobakasu
let value = 10;
value = 20; // コンパイルエラー
```

`mut`を付けた束縛には再代入できる。

```sobakasu
let mut value = 10;
value = 20;
```

`mut`は参照先オブジェクトの排他的所有を意味しない。

また、`mut`がないことは、参照先が深い意味で不変であることを保証しない。

```sobakasu
let values = mutable_list;
```

この束縛自体を別の参照へ再代入することは禁止されるが、`mutable_list`が提供する変更APIの呼び出し可否は、その型およびAPIの規則によって決まる。

Sobakasuは、Rustのようなtransitive immutabilityや排他的な可変借用を`mut`に持たせない。

### 参照型の代入

参照型の通常の代入は参照共有を意味する。

```sobakasu
let original = [1, 2, 3];
let shared = original;
```

この代入後も`original`は使用可能である。

次のような暗黙moveは発生しない。

```sobakasu
let shared = original;

// originalは引き続き使用可能
```

### 明示的な`clone`

参照先を共有せず、独立した値またはコンテナを作成する操作には、明示的な`clone`を使用する。

```sobakasu
let original = [1, 2, 3];
let copied = original.clone();
```

配列の`clone`は、新しい配列領域を作成し、各要素を通常の代入規則に従ってコピーする。

したがって、配列の`clone`は配列領域については独立するが、要素が参照型の場合、その参照先まで再帰的に複製するとは限らない。

```sobakasu
let original = [object_a, object_b];
let copied = original.clone();
```

この場合、`original`と`copied`は異なる配列であるが、それぞれの要素は同じランタイムオブジェクトを参照できる。

つまり、配列の標準的な`clone`は要素単位の浅い複製とする。

すべての参照型に共通の`clone`を自動提供しない。

特にUnityオブジェクトの複製は、通常の`clone`ではなく、対応するUnity APIを明示的に使用する。

```sobakasu
let copied = Object.Instantiate(original);
```

型ごとに独立した複製方法が必要な場合、その型が提供するAPIとして定義する。

### 関数呼び出し

関数引数への値の受け渡しも、通常の代入規則に従う。

Copy値はコピーされる。

```sobakasu
fn inspect(value: i32) {
}

let value = 10;
inspect(value);
```

参照型は参照を共有する。

```sobakasu
fn inspect(values: [i32]) {
}

let values = [1, 2, 3];
inspect(values);
```

関数呼び出しによって、呼び出し元の変数が暗黙に使用不能になることはない。

Rustの`own T`、`&T`、`&mut T`に相当する引数モードは、このADRでは導入しない。

参照先を変更できる関数であることを型または構文で示す副作用注釈については、必要になった場合に別ADRで決定する。

### フィールドとイベント

フィールドはイベントをまたいで保持されるランタイム状態であり、所有権移動の対象にしない。

```sobakasu
field mut values: [i32] = [];
```

フィールドの値は、再代入、要素変更、ランタイムAPIによる状態変更など、通常の言語規則に従って変化する。

あるイベントでフィールドを関数へ渡しても、そのフィールドが他のイベントから静的に使用不能になることはない。

```sobakasu
on Start() {
  process(values);
}

on Interact() {
  Debug.Log(values[0]);
}
```

Sobakasuコンパイラは、イベント間の所有権状態やフィールドの消費状態を追跡しない。

### nullと破棄済み参照

所有権システムを導入しないことと、null安全性は別の問題として扱う。

Sobakasuは、このADRによって次を保証しない。

* すべてのnull参照の静的排除
* Unityの破棄済みオブジェクト参照の静的排除
* イベント間における参照の有効性保証
* 外部APIが返す参照の寿命保証

将来、nullable type、flow-sensitive null analysis、明示的なnull検査などを導入する場合は、所有権システムとは独立した機能として設計する。

### alias診断

配列や`DataList`などの可変参照型が複数の変数から参照されることは、言語仕様上許可する。

```sobakasu
let a = list;
let b = a;
```

コンパイラまたはlint機構は、必要に応じて可変参照の共有について警告できる。

```text
warning: mutable reference is shared by multiple bindings
```

ただし、この共有を所有権違反としてコンパイルエラーにはしない。

alias警告の詳細、警告レベル、抑制方法については、診断機能を設計する際に別途決定する。

### 将来の`move`

v1では`move`構文を導入しない。

将来、配列やユーザー定義型について、値を以後使用しない意図を示す必要性が明確になった場合に限り、ローカル変数に限定した明示的`move`を別ADRで検討できる。

ただし、導入する場合も次を基本制約とする。

* フィールドをmoveできない
* UnityおよびVRChatランタイム所有参照をmoveできない
* イベント間の所有権状態を追跡しない
* Rust互換のborrow checkerへ自動的に拡張しない
* 既存の通常代入を暗黙moveへ変更しない

## Alternatives

### 1. Rustと同等の所有権およびborrow checkerを導入する

値の単一所有者、move、借用、ライフタイムをコンパイル時に検証する。

却下した。

UnityおよびVRChatオブジェクトの大半は外部ランタイムによって管理されるため、ほとんどの実用型が所有権検査の例外または外部借用として扱われる。

また、イベント間でフィールドの所有権状態を追跡するには、オブジェクト全体の状態遷移解析が必要になる。

UdonVMの実行モデルに対して得られる利益より、言語仕様、Binder、診断、学習コストの増加が大きい。

### 2. 配列とユーザー定義型だけに所有権を導入する

Unityオブジェクトを対象外とし、Sobakasuが生成する配列や将来のユーザー定義型だけにmoveを適用する。

却下した。

対象を限定しても、配列をフィールドへ保存した場合やイベント間で利用した場合には、同じ状態遷移問題が発生する。

また、型によって通常代入がコピー、共有、moveに分かれると、利用者が代入の意味を予測しにくくなる。

### 3. 可変参照の共有をコンパイルエラーにする

配列や`DataList`への複数参照を禁止し、可変参照を一つに限定する。

却下した。

UnityおよびUdonのコードでは、同じオブジェクトやコンテナを複数箇所から参照することが一般的である。

共有を全面的に禁止すると、現実的なワールドスクリプトに対して制約が強すぎる。

### 4. 参照型を代入時に常に複製する

配列やコンテナを代入するたびに、新しい値を自動生成する。

却下した。

代入コストが見えにくくなり、配列サイズに比例する隠れた処理が発生する。

また、Unityオブジェクトを複製する意味を一般化できず、`Instantiate`などのランタイムAPIとも衝突する。

### 5. Copy-on-writeを採用する

参照型を通常は共有し、変更時に自動複製する。

却下した。

UdonVM上での実装が複雑になり、参照の同一性や変更タイミングがソースから分かりにくくなる。

Udon-firstの言語としては、通常代入による共有と明示的`clone`の方が実行コストと意味を予測しやすい。

## Rationale

Sobakasuのメモリモデルは、Rustのネイティブメモリ管理モデルとは異なる。

UdonVMでは、値や参照は主に固定されたヒープスロットを介して扱われる。配列参照を複数のスロットへコピーすること自体は、UdonVMにとって不正な操作ではない。

所有権違反を定義するとしても、それはUdonVMの制約ではなく、Sobakasuコンパイラが追加する静的制約になる。

一方、UnityおよびVRChatオブジェクトは外部ランタイムによって管理される。Sobakasuはこれらの生成、破棄、寿命を一貫して管理できない。

この環境に完全な所有権システムを追加すると、多くの型を例外扱いしながら、イベント間状態解析、ライフタイム、typestateなどを実装する必要がある。

Sobakasuの目的は、Rustのメモリ管理モデルをUdon上へ再現することではない。

SobakasuがRustから採用するのは、次のようなソースコード上の明確性である。

* 不変を既定にする
* 可変性を明示する
* 型ごとのコピーと共有の意味を明確にする
* 高コストな複製を`clone`として明示する
* 意図しない再代入を静的に検出する

これらはUdon-firstの実行モデルと衝突せず、完全なborrow checkerを導入しなくても利用者の予測可能性を高められる。

## Consequences

### Positive

* Rust風所有権およびborrow checkerの実装コストを回避できる
* UnityおよびVRChatの外部所有オブジェクトを自然に扱える
* イベント間の複雑な所有権状態解析が不要になる
* 通常代入の意味が、Copyまたは参照共有として明確になる
* 配列などの複製コストを`clone`によってソース上へ明示できる
* ADR-0007で採用したimmutable defaultと自然に統合できる
* UdonVMの実行モデルとソース言語の意味が乖離しにくい
* 将来のnull解析やalias警告を、所有権とは独立して追加できる

### Negative

* 配列や`DataList`などでは、意図しない共有による変更が発生し得る
* コンパイラはデータ競合や参照先変更をRustと同等には防止できない
* `let`が不変でも、参照先まで不変とは限らない
* 配列の`clone`が浅い複製であることを利用者が理解する必要がある
* Unityの破棄済み参照やnull参照を所有権によって排除できない
* 将来ユーザー定義型を導入する際、Copy可能性とclone semanticsを型ごとに定義する必要がある

## Notes

このADRが決定するのは、Sobakasuの所有権、代入、共有、複製に関する基本方針である。

次の事項は別ADRの対象とする。

* nullable typeおよびnull flow analysis
* 配列の具体的な構文、内部表現、生成方法
* `clone`を提供する型のカタログ
* ユーザー定義型のCopy可能性
* 関数引数に対する副作用またはread/write注釈
* alias警告の診断レベル
* Unityオブジェクトの有効性検査
* 将来のローカル限定`move`
