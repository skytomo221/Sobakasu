---
title: 構文一覧
description: 現行Sobakasuの主要構文を簡易文法で一覧します。
sidebar:
  order: 18
---

このページは、Sobakasu の主要な構文を形式的に確認したいときのための**簡易文法**です。通常の使い方を知りたい場合は、各機能の説明ページを先に参照してください。

この一覧は読みやすさを優先しており、コンパイラが不正なコードから復帰するための細かな処理までは記載していません。

## 表記

- `'token'` — その文字列をそのまま書く
- `name` — 別に定義された構文規則
- `?` — 0 回または 1 回
- `*` — 0 回以上
- `+` — 1 回以上

## ソース全体

```text
compilation-unit := declaration* EOF
```

## モジュールと `use`

```text
module-declaration := 'public'? 'module' IDENTIFIER ';'

use-declaration := 'public'? 'use' use-tree ';'
use-tree := path
          | path 'as' IDENTIFIER
          | path '::' '*'
          | path '::' '{' use-tree (',' use-tree)* ','? '}'
          | 'self' ('as' IDENTIFIER)?

path := IDENTIFIER ('::' IDENTIFIER)*
```

## 状態変数、定数、ローカル変数

```text
state-block := 'state' '{' state-member* '}'
state-member := 'public'? sync-modifier? IDENTIFIER type-clause? '=' (expression | 'field') ';'

sync-modifier := 'sync'
               | 'sync' '(' ('none' | 'linear' | 'smooth') ')'

const-declaration := 'public'? 'const' IDENTIFIER type-clause? '=' expression ';'

let-statement := 'let' 'mutable'? binding-pattern type-clause? ('=' expression)? ';'
type-clause := ':' type
```

## 関数

```text
function-declaration := 'public'? 'function' function-name generic-parameters?
                        parameter-list? return-type?
                        (block | external-binding)

parameter-list := '(' parameter-items? ')'
parameter-items := 'state' (',' parameter (',' parameter)*)?
                 | parameter (',' parameter)*
parameter := IDENTIFIER ':' type
           | 'self'
return-type := '->' type
```

`self` は通常、メソッドの最初の引数として使用します。

## 型

```text
type := path type-arguments?
      | '[' type ']'
      | '('
          (type (',' type)* ','?)?
        ')'

type-arguments := '<' type (',' type)* '>'
generic-parameters := '<' IDENTIFIER (',' IDENTIFIER)* '>'
```

`(T)` は括弧で囲んだ `T` と同じ型です。1 要素タプル型は `(T,)` と書きます。

## `struct`、`enum`、外部型

```text
struct-declaration := 'public'? 'struct' IDENTIFIER generic-parameters?
                      ('=' 'extern' external-name)?
                      '{' field* '}'

field := IDENTIFIER ':' type ('=' 'extern' member-name)? ','?

type-declaration := 'public'? 'type' IDENTIFIER '=' 'extern' external-name ';'

enum-declaration := 'public'? 'enum' IDENTIFIER generic-parameters?
                    ('=' 'extern' external-name)?
                    '{' enum-variant* '}'

enum-variant := IDENTIFIER
              | IDENTIFIER '(' type (',' type)* ','? ')'
              | IDENTIFIER '{' field* '}'
```

外部の列挙型では、バリアントの後ろに `= extern member-name` を指定できる場合があります。

## `implementation`

```text
implementation-declaration := 'public'? 'implementation' generic-parameters? type
                              ('=' 'extern' external-name)?
                              '{' function-declaration* '}'
```

現在の `implementation` 内では `static` を修飾子として使用しません。

## `behavior` とイベント

```text
behavior-declaration := 'behavior' '{' behavior-member* '}'
behavior-member := function-declaration
                 | event-declaration
                 | receive-declaration

event-declaration := 'on' IDENTIFIER parameter-list? (':' type)? block
receive-declaration := 'public'? 'receive' IDENTIFIER parameter-list? block
```

`receive` には戻り値型を指定できません。

## `send`

```text
send-statement := 'send' IDENTIFIER argument-list? 'to' expression ';'
argument-list := '(' (expression (',' expression)*)? ')'
```

引数がない `send` では引数一覧の `()` を省略できます。

## 制御フロー

```text
if-expression := 'if' expression block ('else' (block | if-expression))?
while-expression := loop-label? 'while' expression block
loop-expression := loop-label? 'loop' block
loop-label := LABEL_IDENTIFIER ':'

break-statement := 'break' LABEL_IDENTIFIER? expression? ';'
continue-statement := 'continue' LABEL_IDENTIFIER? ';'
redo-statement := 'redo' LABEL_IDENTIFIER? ';'
return-statement := 'return' expression? ';'
```

## `match`

```text
match-expression := 'match' expression '{' match-arm* '}'
match-arm := match-pattern '=>' (expression | block) ','?

match-pattern := literal-pattern
               | '_'
               | path '::' IDENTIFIER
               | path '::' IDENTIFIER '(' binding (',' binding)* ','? ')'
               | path '::' IDENTIFIER '{' binding (',' binding)* ','? '}'
```

現在、列挙型のバリアントから値を受け取る `binding` には単純な識別子を使用します。

## 基本式と後置構文

```text
primary-expression := literal
                    | name
                    | parenthesized-or-tuple
                    | array-literal
                    | if-expression
                    | match-expression
                    | while-expression
                    | loop-expression
                    | 'new' type argument-list
                    | 'extern' expression

postfix-expression := primary-expression
                      (type-arguments
                       | '.' member-name
                       | '::' member-name
                       | argument-list
                       | '[' expression ']'
                       | aggregate-initializer)*
```

## 演算子

演算子の優先順位と結合規則は [演算子](../operators/) を参照してください。

## 実装を見る

- [Parser directory](https://github.com/skytomo221/Sobakasu/tree/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Parser)
- [DeclarationParser.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Parser/DeclarationParser.cs)
- [ExpressionParser.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Parser/ExpressionParser.cs)
- [StatementParser.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Parser/StatementParser.cs)
- [TypeParser.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Parser/TypeParser.cs)
- [PatternParser.cs](https://github.com/skytomo221/Sobakasu/blob/main/Packages/com.skytomo221.sobakasu/Editor/Compiler/Parser/PatternParser.cs)
