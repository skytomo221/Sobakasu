# 配列サンプル

コンパイル可能なサンプルは [`arrays.sobakasu`](arrays.sobakasu) です。配列状態、配列引数／戻り値、default生成、式repeat、添字複合代入、`length`を含みます。

ジャグ配列 ABI を公開する SDK では、同じ構文体系で次を記述できます。

```sobakasu
let matrix = [[i32; 2]; 3];
matrix[1][0] = 42;
log(matrix[1][0]);
```

このリポジトリの VRChat SDK 3.10.4 は `System.Int32[][]` を公開していないため、上の部分をコンパイル可能な `.sobakasu` サンプルへは含めていません。現在は `SBK2091` で Udon ABI 利用不可と診断されます。
