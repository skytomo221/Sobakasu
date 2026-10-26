---
title: 'ADR-0062: Standard Library Receiver and Maybe Projection Policy'
---

## Status

Accepted

## Context

[ADR-0061](../adr-0061-mutable-self-receiver-semantics/) は、receiver 自身の値の変更を `mutable self` で表し、参照先オブジェクトの副作用と区別する。この source-language semantics は変更しない。

Standard Library Generator は CLR / Udon の物理メタデータを Sobakasu standard-library source へ投影する。物理メタデータだけでは値型メソッドの非変更性や参照出力の非 null 保証を一般には確認できないため、保守的な生成規則と明示的な例外が必要になる。

## Decision

責務を次の順序に保つ。

```text
physical CLR/Udon metadata
↓
generation policy
↓
generated member model
↓
renderer
↓
Sobakasu standard library
```

catalog に生成 policy を追加しない。renderer は generated member model に保存された receiver mutability と output projection を使い、CLR 型を再解析しない。

### Receiver

- constructor と static member に instance receiver を付けない。
- reference instance receiver は `self` とする。参照先の副作用は `mutable self` の根拠にしない。
- value / enum instance receiver は、値が変更される可能性を保守的に扱い `mutable self` とする。
- `receivers.readonly` の exact override で `self` に戻せる。

これは generator の heuristic であり、source language の推論規則ではない。method name inference は行わない。同名の `Normalize` でも reference receiver と value receiver は異なる結果になる。

canonical config member ID は既存の `physical.ClrSignature` を opaque ID として扱う。文字列を分解して owner や member kind を推定せず、nested CLR type の既存の spelling も維持する。readonly rule は duplicate、空白、stale、static、constructor、および複数 physical member に一致する ambiguous rule を拒否する。property / field の getter と setter が同一 ID を共有する場合も曖昧性を拒否する。

CLR static operator を first operand 上に配置する既存 operator projection は変更しない。receiver mutability を overload identity に含めない。

### Maybe

generation config v4 は opt-out Maybe とする。

- reference-like return / `out` は既定で `Maybe<T>` とする。Array も reference-like とする。
- Named / ConstructedGeneric は source type record の Reference / Value / Enum 分類を使う。
- value / enum、void、および constructor の通常戻り値は Raw とする。
- GenericParameter や分類不能な型は Raw とする。`!IsValueType` や primitive 判定で参照型を推測しない。
- `ref` parameter の projection scope は拡張しない。

例外は `maybe.nonMaybeTypes`、`maybe.nonMaybeReturns`、member と parameter-name 配列を持つ `maybe.nonMaybeOuts` とする。type exception は source type identity に exact match し、実際に reference-like output を Raw に変更した場合だけ match と数える。member exception は `physical.ClrSignature` に exact match する。value / enum / void に対する無意味な member exception は拒否する。

型単位の例外を特定 member で再び Maybe にする positive override は導入しない。非 null 保証を確認できない API を例外へ追加しない。Maybe return と ref/out outputs の併存を拒否する既存制約を維持し、衝突時は signature と理由を報告する。

v4 の必須 root properties は `version`、`renames`、`lang`、`prelude`、`maybe`、`receivers`、`excludes` とする。`maybe` は上記3つの例外配列、`receivers` は `readonly` 配列を必須とする。unknown property と duplicate property を拒否する。

v3 は明確な migration error で拒否し、v4 として再解釈しない。strict v4 shape validation の前に version を確認するため、旧 `maybe.returns` / `maybe.outs` に対する unknown-property error だけで移行を案内することはない。

## Alternatives

### 値型メソッドをすべて `self` にする

receiver を変更する CLR API を immutable binding から呼べてしまうため却下する。

### C# method name から mutability を推定する

名前は receiver 自身の変更を保証しない。同名の値型・参照型メソッドを区別できず、命名慣習にも依存するため却下する。

### Maybe を v3 と同様に opt-in にする

参照出力に null の可能性があるたびに設定が必要になり、未設定の API を安全な値として公開するため却下する。

### catalog へ projection metadata を埋め込む

物理 API 情報が生成先の言語・policy に依存するため却下する。

### v3 の field 名だけ意味反転する

既存設定を読み込めても挙動が反転し、誤った non-null 保証を暗黙に作るため却下する。schema と内部 rule 名を v4 の意味に合わせ、明示的な移行を要求する。

## Rationale

値型 receiver と参照出力に保守的な既定を置き、根拠のある例外だけを設定できる。物理情報と生成判断を分離することで、ADR-0061 の言語規則を変えずに standard library の契約を明示できる。

## Consequences

### Positive

- 値を変更する instance API を `mutable self` として公開できる。
- 参照出力の欠如を既定で `Maybe` として扱える。
- stale / ambiguous な例外設定を生成時に検出できる。

### Negative

- 非変更な値型 member も既定では mutable binding を要求する。
- reference-like output の広範囲な Maybe 化により、利用側の取り出し処理が必要になる。
- v3 設定は移行が必須になる。
- Maybe return と ref/out outputs の既存制約に衝突する API は、非 null 保証なしに例外で回避できない。
