# SabaShader

[Shader Core](https://github.com/lilxyzw/Shader-Core) をベースにした VRChat 向けシェーダー集です。

## 収録シェーダー

| シェーダー | 用途 |
| --- | --- |
| `SabaShader/Illust2D` | 3D モデルを 2D イラスト調に見せるトゥーンシェーダー |
| `SabaShader/Debug` | mesh、UV、vertex color、法線、ライト入力を可視化する診断用シェーダー |

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

avatar／world の mesh とライティング入力を確認する場合は `SabaShader/Debug` を使用します。
各表示モードと制約は
[docs/shader-debug.md](https://github.com/sabas0ba/vrc_sabashader/blob/main/docs/shader-debug.md)
を参照してください。
Package Manager の Samples から `Debug Shader Demo` を Import すると、全表示モードを同時に確認できるシーンを利用できます。

雨・汗・雪・汚れを乗せる「表面の重ね掛け」、「ドット絵風」、RenderTexture を
表示する「ビデオ入力」、LCD／LED の画素構造を重ねる「表示パネル」、
走査線や映像の乱れを足す「ブラウン管・グリッチ」は
モジュールとして同梱しています。使い方は
[docs/modules.md](https://github.com/sabas0ba/vrc_sabashader/blob/main/docs/modules.md)
を参照してください。

任意画像を貼る「Decal」、肌と布の微細質感を追加する「Surface Detail」、
裂け目や裏面へ異空間を表示する「Spatial Interior」、登場・退場を制御する
「Transition」もモジュールとして同梱しています。設定と制約は
[docs/modules-advanced.md](https://github.com/sabas0ba/vrc_sabashader/blob/main/docs/modules-advanced.md)
を参照してください。Package Manager の Samples から `Advanced Shader Suite Demo` を
Importすると、4機能の代表設定を同一シーンで確認できます。
Spatial InteriorにはUniverse、Starfield、Cyber、Mudの4presetがあります。

旧衣装と新衣装を1本のAnimation Controllerでつなぐ「Transformation Bank」も同梱しています。
Illust2DとNonToonで使用できます。設定、12 Style、Particle補助演出は
[docs/transformation-bank.md](https://github.com/sabas0ba/vrc_sabashader/blob/main/docs/transformation-bank.md)
を参照してください。Package ManagerのSamplesから `Transformation Bank Demo` をImportすると、
12 Styleの同期再生、Roleごとの異なる形状、Particle Systemを確認できます。

## ライセンス

MIT License。詳細は [LICENSE.md](LICENSE.md) を参照してください。
