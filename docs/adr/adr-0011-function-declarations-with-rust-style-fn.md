# ADR-0011: Rust-style `fn` Function Declarations

## Status

Accepted

ADR-0029 supersedes this ADR's prohibition of user-defined function overloads.
Function syntax, return rules, recursion restrictions, and inline lowering remain active.

## Context

Sobakasu は Udon-first の言語であり、C# 互換そのものを目的にしない。
ADR-0002 では top-level script を基本形とし、1 ファイルを 1 つの script module として扱い、top-level に状態、関数、イベントを記述できる余地を残している。

既存機能では ADR-0010 により `on interact() { ... }` のような top-level event declaration を扱う方針が整理された。
一方で、イベント本体から処理を分離し、複数のイベントや式から同じ処理を再利用する user-defined function はまだ未定義である。

この決定は既存 ADR と次のように整合する。

* ADR-0001: ADR は軽量な形式で、Context / Decision / Alternatives / Rationale / Consequences を明示する
* ADR-0002: Sobakasu は Udon-first であり、C# 互換そのものを目的にしない
* ADR-0003: Lexer / Parser / Binder / Desugar / IR Lowerer / Optimizer / UasmAssembler の責務分離を維持する
* ADR-0005: `i32`, `f32`, `f64`, `()` など Rust 風の組み込み型名と整合させる
* ADR-0007: `let` / `mut` による local binding と mutability 方針と整合させる
* ADR-0008: `use` と Binder による名前解決方針と衝突しないようにする
* ADR-0009: 意味解決は Binder で確定し、backend は emission に専念する
* ADR-0010: `on` event declaration と同じ compilation unit の top-level member として扱う

## Decision

Sobakasu v1 は user-defined function declaration として Rust 風の `fn` キーワードを採用する。
C# 風の `void Foo()`、`int Add(...)`、`static` method declaration は採用しない。

基本構文は次の通りとする。

```sobakasu
fn greet() {
  Debug.Log("Hello");
}

fn add(x: i32, y: i32) -> i32 {
  return x + y;
}

fn log_message(message: string) -> () {
  Debug.Log(message);
  return;
}
```

v1 の `fn` declaration は top-level member に限定する。
block 内の nested function、closure、lambda、method、associated function、generic function は v1 では扱わない。
ADR-0010 の event declaration と同じ compilation unit member として並べられる。

```sobakasu
fn message() -> string {
  return "Hello";
}

on interact() {
  Debug.Log(message());
}
```

戻り値型は Rust 風の `-> T` で書く。
戻り値型を省略した場合は `()` とみなす。
`-> ()` は許可するが、標準的な書き方は戻り値型省略とする。

```sobakasu
fn hello() {
  Debug.Log("Hello");
}

fn compute() -> i32 {
  return 42;
}
```

パラメータは `name: Type` とする。
これは `let x: T = expr;` と同じ型注釈方向であり、Rust 風の型名とも整合する。

```sobakasu
fn set_speed(speed: f32) {
  Debug.Log(speed);
}
```

v1 では以下を禁止する。

* default parameter
* varargs
* named argument
* `mut` parameter
* destructuring parameter
* generic parameter

パラメータは immutable local binding として扱う。
パラメータを書き換えたい場合は、関数本体内で `let mut` にコピーする。

```sobakasu
fn increment(x: i32) -> i32 {
  let mut value = x;
  value += 1;
  return value;
}
```

関数導入に合わせて `return` statement を導入する。
また、Rust 寄りの関数構文として trailing expression return を限定的に採用する。

```sobakasu
return;
return expr;
```

`return` と trailing expression return の規則は次の通りとする。

* `()` 関数では `return;` を許可する
* `()` 関数で `return expr;` は型エラーにする
* 非 `()` 関数では `return expr;` を許可する
* 非 `()` 関数で `return;` は型エラーにする
* `return expr;` の式型は関数戻り値型へ適合する必要がある
* 非 `()` 関数では、関数本体の末尾に semicolon なしの expression を置いた場合、それを戻り値として扱う
* trailing expression の型は関数戻り値型へ適合する必要がある
* `()` 関数の末尾 trailing expression は、値を返す式であれば型エラーにする
* `return` statement は関数内でのみ許可する

次の 2 つは同等に扱う。

```sobakasu
fn add(x: i32, y: i32) -> i32 {
  return x + y;
}
```

```sobakasu
fn add(x: i32, y: i32) -> i32 {
  x + y
}
```

ただし、trailing expression は関数本体の末尾式であり、通常の statement とは区別する。
semicolon を付けた場合は expression statement として扱い、戻り値にはならない。

```sobakasu
fn add(x: i32, y: i32) -> i32 {
  x + y;
}
```

上記は `i32` を返す関数としては return 不足の診断対象とする。

関数シンボルは Binder で確定する。
Binder は top-level member を先に収集し、関数宣言順に依存しない呼び出しを許可する。

```sobakasu
on interact() {
  Debug.Log(message());
}

fn message() -> string {
  return "Hello";
}
```

v1 では user-defined function overload を禁止する。
同一スコープ内に同名関数が複数ある場合は診断する。

名前解決優先順位は既存の local variable、parameter、`use`、extern 解決と衝突しないように次の方針を採る。

* local / parameter は式中の単純名として最優先する
* user-defined function は call expression の callee として解決する
* `use` で導入された extern function と user-defined function が同名であり、同じ call expression の callee 候補になる場合は曖昧さを診断する
* v1 では user-defined function に overload resolution を導入しない

v1 の実装を単純にするため、直接再帰と相互再帰はコンパイルエラーとする。
再帰呼び出しは将来の call frame、runtime stack、UASM lowering 設計が必要になった時点で別 ADR に分離する。

```sobakasu
fn fact(n: i32) -> i32 {
  return fact(n - 1);
}
```

上記は v1 では診断対象とする。

v1 では user-defined function call を IR Lowerer で inline 展開する。
これは Udon VM 上の call stack、return address、frame layout を最初から設計しないためである。

inline lowering を採用する理由は次の通りである。

* v1 の実装を小さくできる
* UASM backend に function call frame の責務を持ち込まない
* Binder で型検査済みの関数本体を呼び出し地点へ展開できる
* 再帰を禁止すれば inline lowering で破綻しにくい

inline lowering には次の制約がある。

* コードサイズが増える
* 再帰に対応できない
* 大きい関数を多用すると生成 UASM が増える
* 将来的に internal call lowering へ移行する可能性がある

frontend / IR / backend の責務分離は次の通りとする。

Lexer は次を token として扱う。

```txt
fn
return
->
,
:
```

Parser は次の syntax を構築する。

* `FunctionDeclarationSyntax`
* `ParameterSyntax`
* `ReturnStatementSyntax`
* `FunctionCallExpressionSyntax` または既存 `CallExpressionSyntax` の拡張

Binder は次を担当する。

* 関数シンボルを収集する
* パラメータシンボルを関数スコープに導入する
* 引数個数、引数型、戻り値型を検査する
* `return` statement の型を検査する
* 非 `()` 関数の return 不足を診断する
* 直接再帰と相互再帰を診断する

IR Lowerer は次を担当する。

* v1 では user-defined function call を inline 展開する
* `return` は inline 展開時の synthetic result slot と synthetic end label へ下ろす
* `()` 関数は result slot を持たない

UasmAssembler は解決済み IR を UASM へ emit する。
関数名前解決や型検査は行わない。

v1 では以下を対象外とする。

* nested function
* closure / lambda
* generic function
* function overload
* recursion
* function pointer
* first-class function value
* method / associated function
* instance method declaration
* public event method としての外部公開
* `SendCustomEvent` から呼べる user-defined function 化
* async / coroutine
* attribute
* default parameter
* varargs
* named argument
* cross-file function resolution

## Alternatives

### 1. C#風メソッド宣言を採用する

例:

```sobakasu
i32 add(i32 x, i32 y) {
  return x + y;
}
```

または:

```sobakasu
int Add(int x, int y) {
  return x + y;
}
```

この案は却下する。
Sobakasu は C# 互換を目的にしない。
また、既存の `let`、Rust 風型名、top-level script 方針と整合しにくく、`static`、`class`、method model を連想させる。

### 2. `func` キーワードを採用する

この案は却下する。
`func` は関数宣言であることは分かりやすいが、既存の Rust 風 `let`、`use`、固定幅型名との統一感が弱い。
`fn` の方が短く、関数宣言であることも明確である。

### 3. 最初から runtime call frame を実装する

この案は却下する。
runtime call frame を導入すれば再帰や非 inline の関数呼び出しに拡張しやすいが、v1 として設計面積が大きい。
Udon / UASM の制約により frame、return address、recursion の設計が重くなるため、まずは関数構文、型検査、処理の再利用性を確立する方がよい。

### 4. 明示的な `return` のみを採用する

この案では trailing expression return を採用せず、関数の戻り値は `return expr;` と `return;` のみで表現する。

長所は次の通りである。

* Parser / Binder / Lowerer の実装が単純になる
* UdonSharp 利用者にとって C# 風の `return` 必須構文は直感的である
* semicolon の有無で意味が変わる罠を避けられる
* `return expr;` / `return;` だけを見ればよく、block の末尾式を特別扱いしなくてよい

短所は次の通りである。

* `fn` / `let` / Rust 風型名を採用している Sobakasu の表記方針と比べると一貫性が弱い
* 小さい値計算関数が冗長になる
* 将来 `if` expression や block expression を導入する場合、式指向の設計へ拡張しにくい
* 末尾式を値として扱えないため、Rust 寄りの読み味が弱くなる

この案は却下する。
`fn`、`let`、Rust 風型名を採用しているため、関数だけ明示的 `return` 専用にすると一貫性が弱い。
小さい計算関数を簡潔に書ける利点が大きく、将来 `if` expression などを導入する際にも式を値として扱う方向へ自然に拡張できる。
semicolon の有無で statement と trailing expression を区別すれば、構文上の曖昧さを抑えられる。

## Rationale

`fn` は Sobakasu の Rust 寄り表記と整合する。
Sobakasu は C# 互換ではなく Udon-first 言語であるため、関数宣言だけを C# 風 method syntax にしない。
top-level script を基本形とする ADR-0002 とも整合し、ADR-0010 の `on` event declaration と並べて記述できる。

関数により、イベント本体から処理を分離でき、読みやすくなる。
同じ処理を複数イベントや複数の式から再利用できるため、script module 内の構造化が進む。

Binder で関数シンボル、引数型、戻り値型を確定することで、backend を単純に保てる。
これは ADR-0003 と ADR-0009 の責務分離方針と整合する。
backend は解決済み IR の emission に専念し、関数名の探索、型検査、overload 判定、extern との曖昧さ解決を行わない。

trailing expression return は、採用する長所と短所を比較したうえで v1 に限定導入する。

採用する長所は次の通りである。

* `fn` / `let` / `mut` / Rust 風固定幅型名と整合する
* `fn add(x: i32, y: i32) -> i32 { x + y }` のような小さい関数を簡潔に書ける
* 将来の `if` expression / block expression / match 的構文と相性がよい
* 表面構文の問題であり、`return expr;` と同等の IR へ下ろせるため runtime cost は基本的に増えない

採用する短所は次の通りである。

* semicolon の有無で意味が変わる
* Parser / Binder が block の末尾式を扱う必要があり、実装が複雑になる
* `fn add(...) -> i32 { x + y; }` のようなミスに対して、分かりやすい診断が必要になる
* `()` 関数の末尾式が値を返す場合の型エラー規則を明確にする必要がある

結論として、Sobakasu v1 では trailing expression return を採用する。
ただし、function body の末尾に限り、semicolon なしの expression だけを戻り値として扱う限定的な採用とする。
semicolon ありの expression statement と semicolon なしの trailing expression を区別することで、Rust 寄りの読み味と診断しやすさを両立する。

inline lowering は v1 の実装負荷を抑えるために採用する。
Udon VM 上の call frame、return address、frame layout を最初から設計しないことで、関数構文と Binder の型検査を先に安定させられる。
再帰を禁止するため inline lowering と整合しやすく、将来必要になった時点で internal call lowering へ移行する余地も残せる。

## Consequences

### Positive

* イベント本体の処理を小さな関数へ分解できる
* 同じ処理を複数箇所から再利用できる
* 型付きパラメータと戻り値により、Binder の診断品質が上がる
* trailing expression return により、小さい関数を簡潔に書ける
* Rust 風 `fn` により言語全体の統一感が上がる
* 将来の制御構文、ユーザー定義型、module 設計の土台になる

### Negative

* Lexer / Parser / Binder / IR Lowerer に変更が必要になる
* `return` statement と関数スコープの導入で Binder が複雑になる
* inline lowering によりコードサイズが増える可能性がある
* v1 では再帰、overload、cross-file function に対応しない
* 将来 internal call lowering に移行する場合、ADR の更新または後続 ADR が必要になる

## Notes

後続の ADR-0015 が callable 名とゼロ引数時の括弧規則を更新した。ゼロ引数関数では宣言・呼び出しとも `()` を省略でき、callable 名末尾の `?` を 1 個だけ使用できる。引数が 1 個以上なら従来どおり括弧を必須とし、既存の括弧付き構文も引き続き有効である。

後続実装では、少なくとも次の領域を変更する。

* Lexer: `fn`、`return`、`->`、`,`、`:` の token 追加または確認
* Parser: function declaration、parameter、return statement、call expression の構文対応
* Binder: function symbol table、parameter scope、戻り値検査、return 不足診断、再帰検出、extern との曖昧さ診断
* IR Lowerer: inline expansion、synthetic result slot、synthetic end label、`()` 関数の result slot 省略
* Tests: syntax、binding、diagnostics、inline lowering、event からの function call、trailing expression return
