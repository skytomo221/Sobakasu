# ADR-0012: Expression-oriented `if`, `while`, and `loop` Control Flow

## Status

Accepted

## Context

Sobakasu は Udon-first の言語として、Udon の制約を明示的なコンパイラパイプラインで扱いながら、楽しく直接的にプログラミングできるソース言語を目指している。
日常的な分岐と反復を関数呼び出しだけで表現することは難しく、`if`、`while`、無条件ループ、早期終了を言語機能として定義する必要がある。

この決定は既存 ADR と次のように整合する。

* ADR-0003 の Lexer / Parser / Binder / Desugar / IR Lowerer / Optimizer / UasmAssembler という責務分離を維持する
* ADR-0007 の block scope、local slot、immutable default、shadowing と整合させる
* ADR-0009 の式 precedence と explicit CFG による short-circuit lowering を制御構文へ拡張する
* ADR-0011 の trailing expression を関数本体だけでなく、値を返す制御構文の block にも適用する

Udon Assembly は低レベルの label、branch、heap slot を持つ一方、ソースレベルの loop label や `break` 対象、分岐型の統一を表現しない。
したがって、これらの意味を UasmAssembler まで遅延させず、Binder で解決し、IR Lowerer で CFG へ変換する必要がある。

Sobakasu は Rust 風の式指向構文を基礎にするが、`while` の条件再評価を行わず現在の反復をやり直したい場面がある。
Ruby の `redo` はこの意図を直接表現でき、Udon の低レベル branch にも自然に変換できる。

## Decision

Sobakasu v1 は次の制御構文を採用する。

* `if` expression
* `while` expression
* `loop` expression
* `break` と value-producing `break`
* `continue`
* Ruby 由来の `redo`
* Rust 風の loop label

### 共通構文

`if`、`while`、`loop` の本体は常に `{}` で囲む。
一文だけの場合も brace を省略できない。

`if` と `while` の条件を囲む `()` は要求しない。
通常の parenthesized expression として `()` を使うことはできる。

```sobakasu
if x > 0 {
  Debug.Log("positive");
}

if (x > 0) {
  Debug.Log("positive");
}
```

次は構文エラーとする。

```sobakasu
if x > 0
  Debug.Log("positive");
```

`if` と `while` の条件型は `bool` に限定する。
数値、文字列、参照値の truthy / falsy 変換は導入しない。

control expression を statement の位置で使う場合、末尾 semicolon は省略できる。
`let` initializer、`return` value、`break` value など、外側の構文が区切りを要求する位置では、その外側の規則に従う。

### 文法

Parser は現在の Pratt parser と block parser に合わせ、概念的に次の文法を実装する。

```text
IfExpression
  ::= "if" Expression Block
      ("else" (IfExpression | Block))?

WhileExpression
  ::= LoopLabel? "while" Expression Block

LoopExpression
  ::= LoopLabel? "loop" Block

LoopLabel
  ::= LabelIdentifier ":"

BreakStatement
  ::= "break" ";"
    | "break" Expression ";"
    | "break" LabelIdentifier ";"
    | "break" LabelIdentifier Expression ";"

ContinueStatement
  ::= "continue" LabelIdentifier? ";"

RedoStatement
  ::= "redo" LabelIdentifier? ";"
```

`else if` は専用の多分岐 node ではなく、`else` に別の `IfExpressionSyntax` が続く構造とする。

### Block の値

block 末尾の semicolon なし expression を block の値とする。
semicolon 付き expression statement は値を返さず、その位置の型は `u0` になる。

```sobakasu
let value = if enabled {
  10
} else {
  20
};
```

関数本体について ADR-0011 で採用した trailing expression の parser 機構を、`if` branch と loop body にも使用する。
loop body の trailing value は、通常終了時には捨てて次の反復へ進む。

### `if` expression

`if` は statement 専用ではなく、値を返せる expression とする。

```sobakasu
let message = if succeeded {
  "Success"
} else {
  "Failure"
};
```

型規則は次の通りとする。

* 到達可能な then / else branch の型は完全一致させる
* branch 型を合わせるための暗黙数値変換は行わない
* 値を返す `if` には `else` が必要である
* `else` のない `if` 自体の型は `u0` とする
* `else` のない `if` の then branch は `u0` または制御が戻らない内部型でなければならない
* 一方の branch が制御を戻さない場合、他方の branch 型を `if` の型にできる
* 両 branch が制御を戻さない場合、`if` も制御を戻さない

```sobakasu
let value = if enabled {
  10
} else {
  loop {
  }
};
```

次は診断対象とする。

```sobakasu
let value = if enabled {
  10
};

let value = if enabled {
  10
} else {
  "disabled"
};
```

IR Lowerer は条件を一度だけ評価する。
値を返す `if` では `IrTemporaryStorage` を synthetic result slot とし、到達可能な各 branch の値を `IrCopyInstruction` で格納して merge block へ分岐する。
制御が戻らない branch からは result copy と merge jump を生成しない。

### 内部 `Never` 型

Binder の `TypeKind` と `TypeSymbol` にソースから名前指定できない `Never` を設ける。
表示名は内部用の `<never>` とし、組み込み型名表には登録しない。

`Never` は少なくとも次を型付けするために使う。

* 対象 loop を終了する到達可能な `break` がない `loop`
* trailing expression が `Never` の block
* 末尾が無条件の `return`、`break`、`continue`、`redo` である block
* 両 branch が `Never` である `if`

`Never` は分岐型、return 型、代入先型へ適合できる。
これは実行時変換ではなく、その expression が値を生成する地点へ到達しないことを表す Binder 内部の規則である。
公開された `!` 型は導入しない。

### `while` expression

`while` は構文上 expression として扱うが、型は常に `u0` とする。

```sobakasu
while running {
  update();
}
```

意味は次の通りとする。

* 各反復の前に条件を評価する
* 条件が false なら `u0` で終了する
* `break;` で終了できる
* `break expression;` は、label の有無にかかわらず `while` を対象にできない

`while` に value-producing `break` を許可すると、条件が最初から false の場合の値が存在しない。
`Option<T>`、既定値、または `while ... else` のような別機能が必要になるため、v1 では採用しない。

Lowerer が生成する CFG は概念的に次の形とする。

```text
current -> condition
condition -- true --> body
condition -- false --> exit
body normal end ----> condition
continue -----------> condition
redo ----------------> body
break ---------------> exit
```

### `loop` expression と value-producing `break`

`loop` は無条件に反復し、`break` により値を返せる expression とする。

```sobakasu
let answer = loop {
  if ready {
    break 42;
  }
};
```

型規則は次の通りとする。

* `break;` で終了する `loop` の型は `u0`
* `break expression;` の expression 型を対象 `loop` の型とする
* 同じ `loop` を対象とする到達可能な value-producing `break` の型は完全一致させる
* 暗黙変換による break 型の統一は行わない
* 同じ `loop` を対象とする `break;` と `break expression;` の混在を禁止する
* 内側 loop を対象とする `break` は外側 loop の型へ影響しない
* label 付き `break` は指定された外側 loop の型へ影響する
* value-producing `break` は `loop` だけを対象にできる
* expression statement の位置では、値付き `loop` の結果を捨てられる
* 対象 loop を終了する到達可能な `break` がない `loop` の型は内部 `Never` とする

```sobakasu
let answer = 'search: loop {
  if found {
    break 'search 42;
  }

  continue;
};
```

Lowerer は value-producing `break` の expression を一度だけ評価する。
結果を対象 loop の synthetic result slot へ copy してから、対象 loop の exit block へ jump する。

`loop` の通常終了、`continue`、`redo` はいずれも body 先頭へ分岐する。
`continue` と `redo` は `loop` 上では同じ branch になるが、ソース上の意図を示す別の構文として維持する。

### Loop label

Rust 風の label 構文を採用する。

```sobakasu
'outer: while running {
  loop {
    if completed {
      break 'outer;
    }

    continue 'outer;
  }
}
```

規則は次の通りとする。

* label は `while` と `loop` にだけ宣言できる
* label なしの `break`、`continue`、`redo` は最内周 loop を対象とする
* `break 'label`、`continue 'label`、`redo 'label` で対象を指定できる
* label は local、parameter、function、type とは別の名前空間で Binder が管理する
* label reference はその文を字句的に包含する loop だけを対象にできる
* 存在しない、または包含していない label reference は診断する
* loop 外の `break`、`continue`、`redo` は診断する
* v1 では有効範囲が重なる同名 label を禁止する

Binder は各 loop に `LoopSymbol` を割り当てる。
bound jump node は文字列 label ではなく解決済み `LoopSymbol` を保持する。
これにより、Lowerer と UasmAssembler はソース名を再探索しない。

### Character literal と label の字句規則

閉じる single quote を持つものは character literal、`'identifier` は `LabelIdentifier` token とする。

```sobakasu
'a'
'outer
```

Lexer は opening quote の直後が identifier start である場合、identifier part を先読みする。
identifier の直後に closing quote があれば、既存の character literal reader を優先する。
closing quote がなければ opening quote と identifier を一つの `LabelIdentifier` token として最長一致させる。

このため、`'a'` は `char`、`'outer` は label であり、`'ab'` は複数文字を含む不正な character literal として診断される。
空文字、未終端 character、無効 escape、不正な quote 列には既存の character literal 診断を使用する。
label declaration の colon 欠落には専用の parser 診断を使用する。

### `continue`

`continue` の分岐先は対象 loop の種類により決まる。

* `while` では condition 評価位置へ分岐する
* `loop` では body 先頭へ分岐する
* label 付きの場合は解決済み外側 loop の対応位置へ分岐する

### `redo`

Ruby 由来の `redo` を正式な制御構文として採用する。

```sobakasu
while read_input() {
  if should_retry() {
    redo;
  }

  process();
}
```

意味は次の通りとする。

* 現在の反復を対象 loop の body 先頭からやり直す
* `while` の条件は再評価しない
* `continue` は `while` の condition block へ戻る
* `redo` は condition block を飛ばして body block へ戻る
* `redo 'label;` は指定された外側 loop の body 先頭へ分岐する
* `redo` で抜けた block scope の local は、body 先頭から宣言と initializer が再実行される
* 終了性解析や隠れた反復回数制限は導入しない

```sobakasu
'outer: while condition() {
  let value = create_value();

  loop {
    if retry_outer() {
      redo 'outer;
    }
  }
}
```

local declaration は body block 内の copy instruction として生成されるため、body 先頭へ戻る `redo` では initializer も再実行される。

### Parser とエラー回復

Parser は次の回復を行う。

* control body の `{` がない場合、brace 必須の診断を出し、後続 token を body として消費しない
* loop label の colon がない場合、missing colon token を補い、直後の `while` / `loop` から解析を継続する
* `break` / `continue` / `redo` の不正な token 列は semicolon、right brace、EOF のいずれかまで同期する
* `continue` と `redo` の後に値がある場合、値を受け取らないことを明示する
* loop 以外に付けられた label は専用の構文診断とする

一つの不正な制御構文により、後続の event や function declaration まで大量に誤解析しないことを parser test で保証する。

### Binder と型検査

Binder は次を担当する。

* condition の `bool` 検査
* `if` branch 型の完全一致と `Never` 適合
* `loop` を対象とする break value 型の統一
* `break;` と `break expression;` の混在検査
* `while` を対象とする value-producing `break` の拒否
* `break`、`continue`、`redo` の対象 `LoopSymbol` 解決
* label の宣言、重複、字句的包含関係の検査
* loop 外 jump と unknown label の診断
* nested loop と label 付き外側 jump の正しい解決

実装では `LoopBindingContext` の stack を使う。
各 context は `LoopSymbol`、break の有無、value 有無、break value 型を保持する。
label 付き jump は stack を内側から外側へ探索し、該当 context を更新する。

### CFG lowering

IR Lowerer は loop ごとに `LoopLoweringFrame` を管理する。
frame は解決済み `LoopSymbol` と次を保持する。

* break target
* continue target
* redo target
* result storage

値付き `if` と値付き `loop` の result storage には既存の `IrTemporaryStorage` を使用する。
non-SSA CFG であるため phi node は導入しない。

IR は既存の次の表現へ下ろす。

* `IrBasicBlock`
* `IrCopyInstruction`
* `IrJumpTerminator`
* `IrConditionalJumpTerminator`
* `IrTemporaryStorage`

condition と break value の評価回数は一回に保つ。
`Never` branch、無限 `loop`、戻らない inline function からは到達不能な merge / function-end jump を生成しない。

Desugar は今回の制御構文を別のソース構文へ書き換えない。
Binder で確定した bound node を保持し、IR Lowerer が CFG へ変換する。

### UasmAssembler

UasmAssembler は既存の解決済み IR 出力機構を使用する。

* `IrCopyInstruction` を `PUSH` / `PUSH` / `COPY` へ変換する
* conditional terminator を `JUMP_IF_FALSE` と `JUMP` へ変換する
* jump terminator を `JUMP` へ変換する
* `IrTemporaryStorage` と local storage に typed data slot を割り当てる

UasmAssembler は次を行わない。

* source label の解決
* `break` 対象の探索
* condition や branch の型検査
* `if` branch 型の統一
* `continue` と `redo` の意味判断

### 診断

既存の `DiagnosticBag` と `TextSpan` を使用し、少なくとも次を診断する。

* control body の brace 欠落
* label declaration の colon 欠落
* loop 以外への label
* `continue` / `redo` に書かれた値
* 不正な jump token 列
* `if` / `while` condition の非 `bool`
* 値を返すが `else` のない `if`
* `if` branch 型不一致
* `while` を対象とする value-producing `break`
* loop における value-less / value-producing `break` の混在
* loop break value 型不一致
* loop 外の `break` / `continue` / `redo`
* unknown または non-enclosing label
* 有効範囲が重なる duplicate label
* 不正、未終端、複数文字の character literal

診断には期待される書き方を hint として付ける。

### Test policy

次の層を自動テストする。

* Lexer: keyword、character literal と `LabelIdentifier` の区別、不正 quote
* Parser: brace 必須、括弧あり / なし condition、`else if`、trailing expression、label 宣言と参照、回復
* Binder: bool condition、`if` 型統一、`Never` 適合、loop result 型、break 混在、while value break、nested / labeled jump、loop 外 jump
* IR Lowerer: then / else、merge slot、while condition 再評価、loop back-edge、break / continue / redo target、外側 label、break value 一回評価、到達不能 branch
* UasmAssembler / end-to-end compile: branch instruction、label、typed local / temporary slot、result copy

特に `while` の `continue` が condition block、`redo` が body block を指すことを CFG test で直接検証する。

### 対象外

次はこの ADR の対象外とする。

* `for`
* `do while`
* `match`
* `switch`
* 三項演算子
* 任意 block への label
* `while` からの値返却
* `while ... else`
* break value の暗黙型変換
* 公開された `!` 型
* 終了性解析
* 暗黙の反復回数制限

## Alternatives

### 1. `if` を statement 専用にする

実装範囲は小さくなるが、値の選択に mutable local と分岐ごとの代入が必要になる。
Rust 風の trailing expression や ADR-0011 と一貫せず、小さな計算を直接書く楽しさを損なうため却下する。
Udon では non-SSA temporary slot に自然に下ろせるため、expression 化を避ける必要もない。

### 2. `while` にも value-producing `break` を許可する

`loop` と表面上は統一できるが、condition が最初から false の場合の値が存在しない。
`Option<T>`、既定値、`while ... else` のいずれかを同時に決める必要があり、Udon-first v1 として意味と実装が過剰になるため却下する。

### 3. `break value` を導入しない

loop result slot と型統一は不要になるが、探索や再試行の結果を外側 mutable local に書く必要がある。
`loop` を expression にする価値が小さくなり、Rust 風構文との一貫性と直接性を損なうため却下する。

### 4. Loop label を導入しない

最内周 loop だけなら実装は簡単になるが、nested loop から外側を終了、継続、redo する意図を直接書けない。
flag local を介した不自然な制御へ崩れ、CFG では表現できる機能をソースから隠すことになるため却下する。

### 5. `redo` を導入せず `continue` だけにする

構文数は減るが、`while` condition を再評価せず同じ反復を最初からやり直す意味を直接表現できない。
condition に副作用や入力取得がある Udon script では違いが重要である。
Ruby の明確な語彙を採り、CFG target を分ける方が意図を読みやすいため却下する。

### 6. 一文の control body では `{}` を省略可能にする

短いコードは減るが、dangling `else`、statement 境界、将来の trailing expression との組み合わせが複雑になる。
Rust 風の block expression と一貫せず、編集時に一文を追加した際の事故も増えるため却下する。

### 7. Control flow を IR Lowerer ではなく UasmAssembler で直接処理する

短期的には bound tree から UASM label を作れるが、source label 解決、型検査、result slot、`continue` / `redo` の意味が backend へ漏れる。
ADR-0003 と ADR-0009 の責務分離を破り、CFG test と optimizer の余地も失うため却下する。

## Rationale

expression-oriented な `if` と `loop` は、一時的な mutable state を増やさず、分岐や探索結果を使用地点で直接表現できる。
これは Rust 風の `let`、`fn`、trailing expression を採用してきた Sobakasu の読み味と一貫する。

一方、`while` は false 終了経路を必ず持つため `u0` に限定し、意味の曖昧さを避ける。
value-producing `break` を `loop` だけに限定することで、型検査と CFG result slot を単純に保てる。

`redo` は Udon 上では単なる branch だが、`continue` と異なる意図と評価回数をソースで明示する。
特に入力取得、状態確認、Unity / VRChat API 呼び出しを condition に含む script では、再評価の有無が観測可能である。

Binder で loop target と型を確定し、IR Lowerer で CFG と storage を構築し、UasmAssembler を emission 専用に保つ。
この分離により、Udon-first の制約を満たしつつ、source language の意味を backend の ad-hoc な処理にしない。

## Consequences

### Positive

* `if` と `loop` の結果を直接 local、return、call argument に使える
* `continue` と `redo` の評価回数の違いがソースと CFG の両方で明確になる
* nested loop を label 付き jump で直接制御できる
* internal `Never` により、戻らない branch と値を返す branch を型安全に組み合わせられる
* result slot と branch が explicit IR に現れ、Lowerer と UasmAssembler を独立にテストできる
* UasmAssembler は解決済み branch / copy / slot の出力に専念できる

### Negative

* Lexer、Parser、Binder、bound tree、IR Lowerer、診断、テストの変更範囲が大きい
* semicolon の有無が block value に影響するため、利用者向け診断と説明が必要になる
* v1 の branch / break 型統一は完全一致であり、暗黙変換を期待するコードは通らない
* label と character literal は single quote を共有するため、Lexer の先読み規則を保守する必要がある
* `while` は expression であっても値を返せず、将来値が必要になれば別 ADR が必要になる
