# ADR-0009: 基礎 unary / binary operator と explicit short-circuit IR の採用

## Status

Accepted

## Context

Sobakasu v1 では literal、local variable、extern call、`use` による compile-time 名前解決までは整備が進んでいるが、source syntax としての基礎演算子体系は未整備だった。

Udon-first の言語であっても、算術・比較・論理・bitwise・assignment family を source syntax として持たない場合、日常的な式がすべて関数呼び出しへ崩れ、可読性と記述性が大きく落ちる。

一方で、演算子を backend 特例として最後に extern へ落とす設計や、`&&` / `||` を通常の eager extern call と同一視する設計は、ADR-0003 の frontend / IR / backend 分離と衝突する。

また、将来 `if` expression や制御構文を追加するためには、precedence と short-circuit semantics を AST / Binder / IR 上で明示的に扱う土台が必要である。

## Decision

Sobakasu v1 では、source syntax として基礎的な unary / binary operator を採用する。

対象に含めるのは以下とする。

* 算術: binary `+`, `-`, `*`, `/`, `%` と unary prefix `+`, `-`
* 比較: `==`, `!=`, `<`, `<=`, `>`, `>=`
* 論理: unary prefix `!` と binary `&&`, `||`
* bitwise: unary prefix `~` と binary `&`, `|`, `^`, `<<`, `>>`
* 代入: `=`, `+=`, `-=`, `*=`, `/=`, `%=`, `&=`, `|=`, `^=`, `<<=`, `>>=`

v1 の precedence は以下を採用する。

1. postfix: member access, call
2. unary prefix: `+`, `-`, `!`, `~`
3. multiplicative: `*`, `/`, `%`
4. additive: `+`, `-`
5. shift: `<<`, `>>`
6. relational: `<`, `<=`, `>`, `>=`
7. equality: `==`, `!=`
8. bitwise and: `&`
9. bitwise xor: `^`
10. bitwise or: `|`
11. logical and: `&&`
12. logical or: `||`
13. assignment: `=`, `+=`, `-=`, `*=`, `/=`, `%=`, `&=`, `|=`, `^=`, `<<=`, `>>=`

結合規則は以下とする。

* postfix は最も強く結合する
* unary prefix は右結合として扱う
* 通常の binary operator は左結合とする
* assignment family は右結合とする
* 括弧は常に precedence に優先する

`-` は負数 literal の一部ではなく unary / binary operator として扱う。
したがって `-1` は integer literal `1` に unary `-` がかかった式として構文解析する。

意味解決は Binder で確定する。
Binder は operand type と選択済み extern signature を確定し、backend はその解決済み情報の emission に専念する。
演算子を backend の ad-hoc 特例として処理しない。

v1 の型規則は保守的にする。

* exact match を最優先する
* 算術と比較は同一 numeric primitive に限定する
* equality は primitive の exact match に限定する
* shift は integer lhs と `i32` rhs に限定する
* `!` は `bool` 専用とする
* `~` は整数型専用とする
* `string + string` は禁止する
* `bool` に対する eager bitwise `&`, `|`, `^` は許可する
* signed / unsigned 混在や integer / float 混在は暗黙に通さない

`&&` / `||` は bool 専用 short-circuit operator として扱い、通常の eager extern call と同一視しない。
Binder で short-circuit operator として確定し、Lowerer / IR で CFG を使って表現する。
backend は label / branch / resolved extern call を emission するだけにする。

compound assignment は v1 では保守的に扱う。

* `x += y` は Binder で `x = x + y` 相当へ落とす
* lhs は mutable local variable に限定する
* 一般化された property / indexer / 任意 lvalue 最適化は行わない
* 条件を満たさない lhs には明示的診断を出す

実装上、Sobakasu は explicit IR を持つ。
Lowerer は bound tree を block / instruction / terminator を持つ IR へ変換し、backend は IR から UASM を組み立てる。
slot allocation、temporary allocation、constant slot / heap patch 作成は backend 側で扱う。

今回の対象外は以下とする。

* 三項演算子 `?:`
* `if` expression
* `??`, `??=`
* `++`, `--`
* string 連結の特例
* user-defined operator overloading
* checked / unchecked
* pattern matching 系演算子
* 浮動小数点や整数の高度な暗黙変換
* compound assignment の高度な lvalue 最適化
* property / indexer / extension method 専用の特殊処理

## Alternatives

1. source syntax として演算子を導入せず、すべて関数呼び出しだけに寄せる
   v1 の実装面積は減るが、日常的な式が不自然になり、今後の制御構文や expression 設計とも噛み合わない。

2. precedence を持たず左から順に解釈する
   parser は単純になるが、`1 + 2 * 3` や `a && b || c` のような基本式で期待から外れ、source language としての一貫性が低い。

3. backend で最後に operator を extern へ変換する
   frontend / backend 分離が崩れ、意味解決とエラー検出が遅延し、演算子だけが backend 特例になる。

4. `&&` / `||` も eager な通常二項演算として扱う
   実装は楽になるが、short-circuit semantics を失い、将来の条件式や制御構文の基盤として不適切になる。

5. compound assignment を最初からすべての lvalue に一般化する
   property / indexer / 複雑な lhs の評価回数問題まで抱え込み、v1 の範囲としては過剰になる。

## Rationale

Udon-first であっても、source syntax として基礎演算子を持つ価値は高い。
利用者が毎回 function style へ書き下すよりも、source 上では通常の式として書けた方が可読性と記述性が高い。

同時に、演算子の意味解決を Binder で確定し、backend を解決済み情報の emission に限定することで、ADR-0003 の frontend / backend 分離と整合する。
演算子だけ backend 特例にしないことで、設計の一貫性を保てる。

`&&` / `||` は extern call と同一視できない。
short-circuit semantics を AST / Binder / IR で明示的に扱うことで、評価順序と CFG が設計上可視化され、後段の backend は branch emission に専念できる。

explicit IR を導入して short-circuit を CFG で表現することは、今後の `if` expression や制御構文追加の土台にもなる。
今回 `if` expression 自体は見送るが、その前提となる branch-aware な lowering まではここで整備する。

compound assignment は便利だが、最初から任意 lvalue に一般化すると評価回数や副作用順序の問題を抱える。
v1 では mutable local variable に限定し、Binder で安全に desugar する方が現実的である。

## Consequences

### Positive

* Sobakasu v1 で基礎的な算術・比較・論理・bitwise・assignment family を自然な source syntax で書ける
* precedence と associativity が明示され、call / member access を含む式が安定して解釈できる
* `&&` / `||` を short-circuit として明示的に lowering できる
* Binder で演算子意味解決が確定し、backend の責務が emission に限定される
* explicit IR により、将来の制御構文追加へ拡張しやすくなる

### Negative

* parser、Binder、IR、backend のすべてに変更が入り、v1 としては比較的大きな実装になる
* v1 は exact match 中心なので、C# より保守的で通らない式がある
* compound assignment は mutable local variable に限定され、一般 lvalue にはまだ対応しない
* `?:` と `if` expression は引き続き未導入であり、条件式の表現力は段階的拡張になる
