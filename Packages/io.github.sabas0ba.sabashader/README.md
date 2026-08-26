# SabaShader

[Shader Core](https://github.com/lilxyzw/Shader-Core) をベースにした VRChat 向けシェーダー集です。

## 収録シェーダー

| シェーダー | 用途 |
| --- | --- |
| `SabaShader/Illust2D` | 3D モデルを 2D イラスト調に見せるトゥーンシェーダー |

## 必要環境

- Unity 2022.3 以上
- ビルトインレンダーパイプライン
- [Shader Core](https://github.com/lilxyzw/Shader-Core) 0.1.9 以上（VCC で自動的に導入されます）

## 使い方

1. マテリアルのシェーダーに `SabaShader/Illust2D` を選ぶ
2. Base Texture にアバターのテクスチャを入れる
3. 「塗り」の境界とぼかし、影の色相シフトでイラストらしさを調整する
4. 必要なら「輪郭線」「リムライト」「ハイライト」を足す

各パラメータの詳細は
[docs/shader-illust2d.md](https://github.com/sabas0ba/vrc_sabashader/blob/main/docs/shader-illust2d.md)
を参照してください。描画例の図が付いています。

雨・汗・雪・汚れを乗せる「表面の重ね掛け」、「ドット絵風」、RenderTexture を
表示する「ビデオ入力」、LCD／LED の画素構造を重ねる「表示パネル」、
走査線や映像の乱れを足す「ブラウン管・グリッチ」は
モジュールとして同梱しています。使い方は
[docs/modules.md](https://github.com/sabas0ba/vrc_sabashader/blob/main/docs/modules.md)
を参照してください。

## ライセンス

MIT License。詳細は [LICENSE.md](LICENSE.md) を参照してください。
