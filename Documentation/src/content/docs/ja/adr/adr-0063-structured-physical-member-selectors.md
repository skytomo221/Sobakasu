---
title: 'ADR-0063: Structured Physical Member Selectors'
---

## Status

Accepted

## Context

[ADR-0062](../adr-0062-standard-library-receiver-and-maybe-projection-policy/) は `physical.ClrSignature` を canonical config member ID とした。しかし property / field の getter と setter は同じ `ClrSignature` を共有するため、physical member を一意に指定できない。

Udon API Catalog はすでに `methods`、`propertyGetters`、`propertySetters`、`fieldGetters`、`fieldSetters` を区別している。この情報を文字列 ID に潰さず、生成設定の照合へ引き継ぐ。

## Decision

generation config v5 の `receivers.readonly` に structured member selector を採用する。

```json
{
  "version": "5",
  "receivers": {
    "readonly": [
      {
        "member": "UnityEngine.Vector3.ToString",
        "kind": "instanceMethod",
        "parameters": []
      },
      {
        "member": "UnityEngine.Vector3.x",
        "kind": "propertyGetter"
      }
    ]
  }
}
```

上記は設定の抜粋である。getter の例は指定方法を示し、既定設定には追加しない。

- `member` は `<physical host CLR runtime type>.<member name>` という完全修飾の logical member name とする。owner は `HostRuntimeName` であり、`DeclaringRuntimeName` ではない。入れ子型の `+` を保持し、括弧や parameter list を含めない。同じ CLR declaration から投影された inherited member でも、host が異なれば別の physical selector identity を持つ。`DeclaringRuntimeName` は宣言元を表す physical metadata として保持する。
- `kind` は `UdonApiMemberKind` に直接対応する `instanceMethod`、`propertyGetter`、`propertySetter`、`fieldGetter`、`fieldSetter` とする。readonly receiver は instance member のみを対象とし、static member と constructor はエラーにする。
- method overload identity は必須の `parameters` 配列で構造的に表す。引数なしでも `[]` を明示する。各引数の `type` に既存の `UdonApiTypeRef` と JSON converter を再利用し、`passing` は省略時に値渡し、指定時に `ref` / `out` / `in` とする。parameter name と `params` 属性は identity に含めない。
- accessor の `parameters` は indexer の引数を表し、省略時は空配列と同じである。setter の暗黙の `value` 引数は含めない。field の引数は空である。
- `genericArity` は省略時に 0 とし、generic method の arity を identity に保持する。generic method の生成対応は拡張しない。
- config DTO は immutable な `UdonBindingMemberSelector` に変換する。physical member も `HostRuntimeName`、`Name`、`Kind`、`IsStatic`、`Parameters`、`GenericParameters` から同じ構造へ投影する。
- 比較と duplicate 検出は structural equality に基づく。0 件は stale、複数件は ambiguous として拒否する。最初の一致の採用や重複の除去で曖昧性を隠さない。表示用文字列は照合の根拠にしない。
- `ClrSignature` / Udon signature を parse して identity を復元しない。`@get` / `#get` 等を member string に埋め込まない。kind は名前から推測しない。

v4 → v5 は明示 migration とする。v4 の読み込みは structured selector への変更と具体的な移行例を含むエラーにする。v5 に旧 string entry を受け付ける互換 schema は設けない。unknown property、duplicate property、null、不正な型・passing・arity を拒否する strict-config 方針を維持する。

既定設定の `Vector3.ToString()` / `GetHashCode()` のみを移行し、readonly member の追加判断はしない。

ADR-0062 の config member identity / receiver rule representation に関する部分だけを変更する。source-language の `mutable self` semantics、Maybe policy、`ClrSignature` の意味、Udon API Catalog の persisted schema は変更しない。`renames.members`、`prelude.members`、`maybe.nonMaybeReturns`、`maybe.nonMaybeOuts`、`excludes.members` の schema も維持する。selector abstraction は将来ほかの physical-member rule でも再利用できる。

## Alternatives

- `ClrSignature` を維持する: getter と setter を一意指定できない。
- `@get` / `#get` などを追加する: 独自の文字列体系と解析が必要になり、Catalog の構造を再び失う。
- Udon extern signature を ID にする: ABI 表現へ生成設定が結合し、CLR logical member の指定にならない。
- Catalog の persisted schema を変更する: すでに必要な情報を保持しており、今回の問題の原因ではない。
- 他の rule も同時に移行する: 互換性への影響が広がるため、必要性を個別に検討する別課題とする。

## Rationale

Catalog の既存モデルと型表現を活用し、accessor kind、overload、passing mode、generic arity を失わずに指定できる。physical metadata と生成 policy を分離し、設定用の識別情報と diagnostics 用の既存 signature を独立して維持できる。

例えば `System.Object.GetType` が Vector2 / Vector3 の physical surface として投影された場合、selector は `System.Object.GetType` ではなく `UnityEngine.Vector2.GetType` / `UnityEngine.Vector3.GetType` として区別する。宣言元を owner とすると一つの設定が複数 host に一致して ambiguous になるため、公開・生成される host を owner とする。一つの selector を全 derived host へ暗黙には適用しない。

## Consequences

### Positive

- 同名の getter / setter を個別に指定できる。
- overload の指定と duplicate / stale / ambiguous 検証が構造に基づく。
- 既存 receiver semantics と Catalog schema を維持できる。

### Negative

- v4 の readonly 設定は明示的な書き換えが必要になる。
- 対象外の member rule は引き続き `ClrSignature` に依存する。accessor の個別指定が必要になった場合は、各 rule の意味と互換性を検討して別途移行する。
