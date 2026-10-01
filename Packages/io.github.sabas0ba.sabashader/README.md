# SabaShader

[Shader Core](https://github.com/lilxyzw/Shader-Core) をベースにした VRChat 向けシェーダー集です。

## 収録シェーダー

| シェーダー | 用途 |
| --- | --- |
| `SabaShader/Illust2D` | 3D モデルを 2D イラスト調に見せるトゥーンシェーダー |
| `SabaShader/Paper2D` | 3D メッシュの奥行き圧縮、平坦な陰影、紙面ノイズ、外形線を組み合わせるシェーダー |
| `SabaShader/Acrylic2D` | 3D メッシュの奥行き圧縮、印刷面の上に合成するアクリル層、白い外縁を組み合わせるシェーダー |
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

## Paper2D / Acrylic2D

`SabaShader/Paper2D` は、3D メッシュのローカル Z 方向の厚みを圧縮し、段階的な陰影と外形線で紙の切り絵のように見せるシェーダーです。紙面の粒状感、任意の両面表示と裏面色も調整できます。

`SabaShader/Acrylic2D` は、同じ3Dメッシュを薄くし、紙状の印刷色の上に1枚のアクリル層を合成するシェーダーです。印刷面は深度を書き込むため、重なったメッシュでも半透明色が二重になりません。白い外縁と任意の反射色を追加できます。

両シェーダーは PC 向け BIRP を対象とします。表面の明暗と、他の物体へ落とす影は個別に切り替えられます。

Package Manager の Samples から `Thin2D Demo` を Import すると、元の3Dメッシュと紙・アクリル表現を比較するシーンを利用できます。

パラメータの詳細は [Paper2D / Acrylic2D のドキュメント](https://github.com/sabas0ba/vrc_sabashader/blob/main/docs/shader-thin2d.md) を参照してください。Acrylic2D には、白い余白、視線角による体積感、印刷面の視差、内部散乱光、表面の影、投影影の設定があります。

雨・汗・雪・汚れを乗せる「表面の重ね掛け」、「ドット絵風」、RenderTexture を
表示する「ビデオ入力」、LCD／LED の画素構造を重ねる「表示パネル」、
走査線や映像の乱れを足す「ブラウン管・グリッチ」は
モジュールとして同梱しています。使い方は
[docs/modules.md](https://github.com/sabas0ba/vrc_sabashader/blob/main/docs/modules.md)
を参照してください。

任意画像を貼る「Decal」、肌と布の微細質感を追加する「Surface Detail」、
裂け目や裏面へ異空間を表示する「Spatial Interior」、登場・退場を制御する
「Transition」、Contact Receiverから肌を押し込む「Mochi Skin」もモジュールとして
同梱しています。設定と制約は
[docs/modules-advanced.md](https://github.com/sabas0ba/vrc_sabashader/blob/main/docs/modules-advanced.md)
を参照してください。Package Manager の Samples から `Advanced Shader Suite Demo` を
Importすると、Mochi Skinを除く4機能の代表設定を同一シーンで確認できます。
Spatial InteriorにはUniverse、Starfield、Cyber、Mudの4presetがあります。
Mochi Skinは別sampleの`Mochi Skin World Demo`で、NonToonの肌比較とPlay Modeの接触previewを確認できます。
へこみやすさの全体係数、テクスチャマスク、Humanoidの骨・ウェイトから生成する硬さマスクは
[docs/mochi-compliance.md](https://github.com/sabas0ba/vrc_sabashader/blob/main/docs/mochi-compliance.md)
にまとめています。

旧衣装と新衣装を1本のAnimation Controllerでつなぐ「Transformation Bank」も同梱しています。
Illust2DとNonToonで使用できます。設定、12 Style、Particle補助演出は
[docs/transformation-bank.md](https://github.com/sabas0ba/vrc_sabashader/blob/main/docs/transformation-bank.md)
を参照してください。Package ManagerのSamplesから `Transformation Bank Demo` をImportすると、
12 Styleの同期再生、Roleごとの異なる形状、Particle Systemを確認できます。Unity Editorの
`Transformation Bank Clip Generator` から、衣装A／BとStyleに対応する双方向Animation Clipを生成できます。
非対応Materialがある場合は、利用可能な対応Shader／Materialを確認し、元Materialを変更せずに互換Materialを生成して
対象Renderer Slotへ割り当てられます。
Generator UIの各入力、Material修復の変更範囲、全Materialパラメータ、Style選択、目的別調整例、よくある問題は
[衣装変身バンクの利用ガイド](https://github.com/sabas0ba/vrc_sabashader/blob/main/docs/transformation-bank.md)
に図付きで記載しています。

## ライセンス

Apache License 2.0。詳細は
[LICENSE](https://github.com/sabas0ba/vrc_sabashader/blob/main/LICENSE) と
[NOTICE](https://github.com/sabas0ba/vrc_sabashader/blob/main/NOTICE) を参照してください。
