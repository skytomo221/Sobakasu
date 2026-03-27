# ADR-0006: Post-Assemble Heap Patching for Typed Initial Values

## Status

Accepted

## Context

Sobakasu は Udon-first のコンパイラとして、frontend と backend を分離したパイプラインを採用している。

一方で、UdonAssembly の `.data` 表現だけでは、型付き初期値や一部のプリミティブ定数を安定して表現しづらい。

特に Sobakasu では、ADR-0003 の責務分離を守りながら、`bool`, `char`, `i64`, `u64`, `f64`, `string` などの literal を今後も広げていく必要がある。

この問題を `UasmAssembler` に押し込むと、

* backend が型変換と初期値表現の特殊処理を抱え込む
* frontend が持つ型付き定数情報を最終段で失いやすい
* patch failure の診断が assembler error と混ざる

という問題が起こる。

## Decision

Sobakasu は、typed initial value の最終反映を Editor-only の post-assemble heap patching で行う。

具体的には次を採用する。

* `UasmAssembler` は symbol を持つ `.data` / `.code` の生成に専念する
* 型付き literal の runtime value は compile result に `HeapPatchEntry` として保持する
* `SobakasuProgramAsset` は assemble 後に `IUdonProgram` の heap へ patch を適用する
* patch failure は compile failure / assembly failure と分離し、`patchError` として扱う
* patch failure 時は ProgramAsset を invalid とし、成功済み program を保存しない
* v1 の patch source は existing literal constant slots のみとする

また、refresh 時に再 assemble できるよう、patch metadata は ProgramAsset に serialized manifest として保持する。

## Alternatives

### 1. UASM の `.data` だけで正確な初期値を表現する

問題:

* 型ごとの特殊処理が backend に集中する
* UdonAssembly 依存が強くなり、責務分離を崩す

### 2. Binder / Lowerer で型付き値を捨てて UASM 文字列だけを返す

問題:

* assemble 後に typed value を復元できない
* symbol ごとの patch failure を診断しづらい

### 3. field / array / user-defined value まで今回まとめて実装する

問題:

* 現在の言語実装との差分が大きい
* v1 の検証対象が広がりすぎる

## Rationale

* ADR-0003 の「frontend / backend の責務分離」を維持できる
* compile result に typed runtime value を残せる
* symbol 名ベースで heap patch を適用できる
* compile error, assembly error, patch error を分離できる
* v1 を literal constant slots に限定することで、小さく導入して将来の field / array / user-defined value へ拡張できる

## Consequences

### Positive

* `bool`, `char`, `i64`, `u64`, `f64`, `string` を UASM 表現に依存せず扱える
* patch failure が symbol 名と型付きで報告できる
* refresh 時にも同じ patch を再適用できる
* `UasmAssembler` に過剰な意味変換を持ち込まずに済む

### Negative

* compile result と ProgramAsset の状態管理が増える
* assemble と save のライフサイクルを分ける必要がある
* patch manifest の serialization 互換を今後管理する必要がある

