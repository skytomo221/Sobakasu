# ADR-0015: Ruby-Style Callable Names and Optional Zero-Argument Parentheses

## Status

Accepted

## Context

Sobakasu は C# 互換を目的とせず、VRChat の Udon VM と Unity Editor 統合を重視する Udon-first の独自言語である。標準ライブラリを設計する際、フィールドと値を返すゼロ引数メソッドの表記が大きく異なると、軽い問い合わせ API まで呼び出し構文を強く意識させる。

Ruby では、問い合わせ目的の callable を `ready?` や `empty?` のように命名し、ゼロ引数呼び出しの括弧を省略できる。この読み味を Sobakasu に限定的に採り入れつつ、引数付き呼び出し、式境界、名前解決、将来構文の設計余地を明確に保つ必要がある。

この ADR は、ADR-0010 のイベント宣言と ADR-0011 の関数宣言・呼び出しに対する後続の決定であり、callable 名とゼロ引数時の括弧省略規則の正本とする。ADR-0009 で採用した単項論理否定 `!` と演算子 precedence は変更しない。

## Decision

### Callable 名

関数名、および将来導入するメソッド名には、末尾の `?` を 1 個だけ任意で付けられる。

```text
CallableName
  ::= Identifier "?"?
```

```sobakasu
fn ready? -> bool {
  true
}

fn empty?() -> bool {
  false
}
```

`?` は callable 名の末尾にだけ置き、名前の途中や `??` という接尾辞には使用しない。`?` は問い合わせ目的の callable に推奨する命名規約であり、戻り値を `bool` に強制する型機能ではない。ローカル変数、引数、トップレベル状態変数、型名、イベント名には使用しない。

Lexer は `ready?` 全体を通常の識別子に含めず、`IdentifierToken` に相当する token と `QuestionToken` に相当する token に分ける。これにより一般の名前への混入を防ぎ、将来の `??`、`?.`、`?:` などの構文余地を残す。

callable 名末尾の `!` は採用しない。`!` は将来の macro 構文用に予約し、既存の単項論理否定と `!=` は維持する。

```sobakasu
if !ready? {
}
```

### ゼロ引数宣言

関数の仮引数が 0 個の場合に限り、空の引数リスト `()` を省略できる。戻り値型の有無を問わず、括弧ありと括弧なしは同じシグネチャを表す。

```sobakasu
fn reset {
}

fn ready? -> bool {
  true
}
```

従来の `fn reset()` と `fn ready?() -> bool` も引き続き有効である。引数を 1 個以上宣言する場合は必ず括弧内へ記述し、`fn set_value value: i32 {}` は受理しない。Parser は関数名の直後が `{` または `->` なら省略された空の引数リストとして扱い、既存の括弧付き parameter list の解析経路も維持する。

引数が 0 個のイベント宣言でも `()` を省略できる。

```sobakasu
on Interact {
}

on Interact() {
}
```

引数付きイベントでは従来どおり括弧を必須とする。イベント名には `?` を許可しない。

### ゼロ引数呼び出し

実引数が 0 個の callable に限り、呼び出し時の `()` を省略できる。

```sobakasu
reset;
reset();

if ready? {
}

if ready?() {
}
```

括弧ありと括弧なしは同じ呼び出しを表す。引数が 1 個以上必要な callable は `set_value(10)` のように呼び出し、`set_value 10` や裸の `set_value` を呼び出しとして扱わない。

Parser は括弧のない名前を無条件に呼び出し構文へ変換せず、通常の名前式として保持する。Binder は解決済み symbol に従って次の順で意味を確定する。

1. ローカル変数または引数なら通常の値参照にする
2. トップレベル状態変数なら通常の状態読み取りにする
3. 引数 0 個の user-defined function なら引数 0 個の呼び出しにする
4. 現在の名前解決で得られる引数 0 個の extern function なら引数 0 個の呼び出しにする
5. 引数を必要とする callable しかなければ、括弧と引数が必要であることを診断する
6. 候補がなければ既存の未定義名診断を使用する

括弧を省略した呼び出しも既存の Bound Call 表現へ変換する。Binder 以降はソース上の括弧の有無を区別せず、IR Lowerer の既存の user-defined function inline lowering または extern call lowering を利用する。括弧省略専用の IR node、命令、UASM backend 分岐は追加しない。イベント宣言の括弧の有無も Udon entry point 名や署名へ影響させない。

### 名前衝突

同じ可視メンバー集合で、同名の値メンバーとゼロ引数 callable の両方を公開することは禁止する。トップレベル状態変数とゼロ引数関数が同名なら、宣言または symbol 収集時に曖昧な名前として診断し、一方を暗黙に優先しない。

ローカル変数または引数による既存の通常の shadowing は維持する。将来フィールドとメソッドを導入する場合も、同じ公開メンバー集合で同名のフィールドとゼロ引数メソッドを公開しない。

## Alternatives

### 常に括弧を必須にする

構文が単純で呼び出しが明示される。一方、フィールドと値取得用ゼロ引数メソッドの見た目が大きく異なり、Ruby 風の読み味や property 相当の軽い問い合わせ API を表現しにくいため採用しない。

### 引数が 1 個以上ある呼び出しでも括弧を省略可能にする

Ruby により近く短く書ける。一方、式境界や演算子との組み合わせ、Parser のエラー回復が複雑になり、現在の Sobakasu には設計面積が大きすぎるため採用しない。

### `?` を通常の識別子文字としてすべての名前に許可する

変数名、型名、イベント名にも `?` が入り、callable 専用の命名意図と将来の `?` 構文の余地を失うため採用しない。

### `?` 付き callable に `bool` 戻り値を強制する

述語名と型は一致するが、Ruby の命名規約より強い制約となり、命名規則と型システムを不必要に結合して将来の API wrapping を制限し得るため採用しない。

### 値メンバーまたはゼロ引数 callable を暗黙に優先する

API 追加により既存コードの意味が変化し、利用者から名前解決を予測しにくくするため採用しない。公開メンバー集合での衝突を診断する。

## Rationale

Ruby 風の `?` は問い合わせ目的の callable を読み取りやすくする。ゼロ引数時だけ括弧を省略することで、標準ライブラリのフィールドと値取得メソッドを近い見た目にしつつ、引数付き呼び出しの文法と Parser の複雑さを維持できる。

`fn` と `on` が宣言境界を明示するため、ゼロ引数宣言から `()` を省略しても曖昧さは小さい。括弧のない名前参照の意味を Binder で決定し、同じ Bound Call へ統合すれば、ADR-0003 と ADR-0009 の責務分離を保ち、backend に表面構文の差を持ち込まずに済む。

`!` は macro 構文用に予約し、`?` は型制約ではなく命名規約とすることで、それぞれの構文と型システムの将来余地を残す。

## Consequences

### Positive

* ゼロ引数 callable をフィールドに近い読み味で利用できる
* 述語的な関数を `ready?` や `empty?` と表現できる
* 従来の括弧付き宣言・呼び出しを維持できる
* 引数付き呼び出しの文法を変更せず、Parser の複雑化を限定できる
* Binder 以降の IR と backend を括弧あり・なしで共通化できる

### Negative

* 裸の名前が値参照かゼロ引数呼び出しかを Binder が判断する必要がある
* 第一級関数値を将来導入する場合、裸の関数名との曖昧性を別途解決する必要がある
* `?` を使用できる名前の種類と位置を Parser または Binder で検証する必要がある
* 値メンバーとゼロ引数 callable の衝突検査が必要になる
* 括弧ありと括弧なしという複数の表記が存在する

## Out of Scope

この決定では、引数付き呼び出しの括弧省略、メソッド宣言、`impl`、`self`、`static fn`、associated function、constructor、property、field の新規機能、function pointer、first-class function value、`&function`、bound method、closure、lambda、macro、callable 名末尾 `!`、`?` 付きイベント名または変数名、新しい IR 呼び出し規約、再帰、cross-file function resolution を導入しない。
