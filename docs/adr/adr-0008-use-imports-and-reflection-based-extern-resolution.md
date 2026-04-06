# ADR-0008: use導入構文とreflectionベースextern解決の採用

## Status

Accepted

## Context

Sobakasu は Udon-first の言語であり、C# 互換そのものを目的にしない。
一方で Unity / VRC / TMPro / System 由来 API を扱うためには、コンパイル時に名前空間・型・静的関数を安全に導入し、最終的に Udon extern として解決できる呼び出しだけを採用する必要がある。

既存実装では `Debug.Log` のような一部 API を hardcoded な表で扱っており、次の問題があった。

* frontend と backend の責務境界よりも、特定 API の知識が実装に埋め込まれやすい
* import のような compile-time 名前解決機構がない
* reflection で候補を見つける設計になっておらず、将来の拡張性が低い
* Udon で本当に解決可能な extern だけを採用する判定が設計上明確でない

Sobakasu では runtime 機能としての module system を今すぐ導入するのではなく、Binder に閉じた compile-time 名前解決として `use` を成立させる必要がある。

## Decision

Sobakasu v1 では、compile-time only の `use` 導入構文と、reflection ベースの extern candidate discovery を採用する。

`use` は runtime 効果を持たず、Binder の名前解決にのみ影響する。
path は `.` 区切りで解釈し、v1 で導入対象にできるのは 名前空間 / 型 / 静的関数 のみとする。

v1 のサポート構文は以下に限定する。

```sobakasu
use UnityEngine;
use UnityEngine.Debug;
use UnityEngine.Debug.Log;
use UnityEngine.Debug as D;
use UnityEngine.Debug.Log as log;
```

`use` は top-level member としてパースし、Binder はファイル全体の `use` を先に収集して order-independent に扱う。

Binder の名前解決優先順位は以下とする。

1. ローカル / パラメータ
2. `use ... as alias` による alias 導入
3. `use` による direct import
4. `use` による namespace import を起点にした部分解決
5. グローバル名前空間
6. 互換維持のための限定的 fallback

互換維持のための fallback は `Debug -> UnityEngine.Debug` のみを残し、広い implicit import は採用しない。

extern 候補の発見は compile-time / Editor domain 側で reflection ベースに行う。
ただし、reflection で見つかった任意の CLR public method をそのまま callable にする設計は採用しない。
reflection は candidate discovery にのみ使い、Binder はその候補の中から Udon extern として解決可能なものだけを callable として選択する。

候補モデルは少なくとも以下の情報を持つ。

* declaring type
* method name
* parameter CLR types
* return CLR type
* computed extern signature
* Udon exposed かどうか

reflection で見つけた候補のうち、Udon extern として解決可能なものだけを `ExternMethodSymbol` として採用する。
reflection 上は存在しても Udon extern として表現・解決できない候補は rejected candidate として保持し、呼び出し時に明示的診断を出す。

extern 解決責務は backend ではなく Binder に置く。
backend は Binder が確定した extern signature を emit するだけにする。

overload 解決は v1 では保守的に行う。

* exact match を最優先する
* 既存 primitive conversion policy に沿う widening のみを考慮する
* extern call applicability のために internal な `System.Object` catch-all conversion を許可する
* user-defined conversion は許可しない
* 曖昧な解決は失敗とし、明示的診断を出す

import collision も暗黙優先しない。
alias 衝突、import による曖昧参照、candidate 不在、rejected-only、overload ambiguity はすべて診断として可視化する。

reflection 結果は毎回フルスキャンせず、Editor domain 内でキャッシュ可能な構造を採用する。
v1 では in-memory cache のみを採用し、disk cache は導入しない。

v1 で見送るものは以下とする。

* glob import
* nested import
* `{}` による grouping import
* `*`
* relative import
* ユーザー定義 module import
* namespace alias
* property / field import
* instance member import
* 拡張メソッド
* 高度な generic binding

## Alternatives

1. hardcoded extern table を拡張し続ける
   特定 API の最小実装としては簡単だが、対象拡張のたびに compiler 実装へ知識が埋まり、保守性が低い。

2. reflection で見つけた public CLR method をそのまま callable にする
   実装は単純になるが、Udon で実際に extern として解決できる保証がなく、安全側に倒せない。

3. backend で最後に extern 解決する
   frontend / backend の責務分離が崩れ、曖昧解決や失敗が後段まで遅延する。

4. C# と同等の import / using 解決を再現する
   v1 の小さな文法・Binder 実装に対して過剰であり、Sobakasu の Udon-first 方針とも一致しない。

## Rationale

この設計は、Rust 風の導入宣言という使い勝手を取り入れつつ、Sobakasu を C# clone にしないための境界を明確にする。

`use` を Binder 専用の compile-time 機構に限定することで、runtime semantics を増やさずに名前解決だけを拡張できる。
また、reflection と Udon exposed filtering を分離することで、「見つかった」ことと「呼べる」ことを同一視しない安全な設計になる。

extern を Binder で確定させることで、曖昧さや衝突を早期に検出でき、backend は選択済み signature の emission に集中できる。
これは既存 ADR の frontend / backend 分離方針とも整合する。

## Consequences

### Positive

* `use` により namespace / type / static function の compile-time 導入が可能になる
* `Debug.Log("Hello")` や `log("Hello")` のような短い呼び出しを安全に解決できる
* reflection ベースの候補発見により hardcoded API 表の中核依存を減らせる
* Udon extern として本当に解決可能な候補だけを callable にできる
* Binder で曖昧さと衝突を確定できるため、backend が単純化する

### Negative

* Editor domain での reflection catalog 構築コストが発生する
* v1 では namespace alias や property / field / instance member import を扱えない
* overload 解決は保守的であり、C# と同じ解決結果にはならない
* cache は in-memory のみで、domain reload ごとの再構築は残る
