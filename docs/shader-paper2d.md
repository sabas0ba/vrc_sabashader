# SabaShader/Paper2D

3D メッシュの奥行きを縮め、平らな陰影と外形線で紙作品のように見せる PC 向け BiRP シェーダーです。

![同じメッシュの Standard、Paper2D、Acrylic2D を Unity 2022.3.22f1 で比較した画像。右上が Paper2D](../docs/assets/thin2d-demo.png)

この画像は Package 同梱の `Thin2D Demo` を Unity 2022.3.22f1 で描画したものです。右上が Paper2D、左上が元の 3D マテリアルです。例では `Local Z thickness = 0.18`、`Flat shading = 1`、暗い外形線と白い余白を設定しています。

## 形状と陰影

`Local Z thickness` (`_DepthScale`) は各 Renderer のローカル Z 座標を縮小します。`1` は元の形状、`0.02` はほぼ平面です。厚みを縮めても元の法線を陰影の計算に使います。複数 Renderer を一体として薄くする場合は、メッシュの結合または部品の配置調整が必要です。

- `Flat shading` と `Shade boundary` は 3D 面の陰影を段階的にし、奥行き感を抑えます。
- `表面の影` と `投影影` は、メッシュ上の明暗と他の物体へ落とす影を個別に切り替えます。

![同じ Thin2D Demo を横方向から描画した画像。薄くした Paper2D と Acrylic2D の側面を比較できる](../docs/assets/thin2d-demo-side.png)

## 輪郭・裏面・紙面

- 外形線はメッシュ法線に沿って拡張した裏面を描画します。`Silhouette width` はワールド空間で指定します。
- `白い余白の幅` は暗い外形線のさらに外側へ余白を描きます。`0` にすると余白を消せます。
- `Cull` を `Off` にすると両面を描画し、`Backface Color` が裏面に適用されます。`Backface Strength` で裏面の色差を調整します。
- `Paper Grain` は UV 基準の微細な明暗変化です。追加テクスチャは使用しません。
- `Alpha Mode = Cutout` はテクスチャのアルファを `_Cutoff` で切り抜きます。
- `Edge Color`、`Edge Width`、`Edge Intensity` はテクスチャのアルファ境界に加える色です。メッシュ外形線とは別の設定です。

## Unity で確認する

Package Manager から `Thin2D Demo` を Import し、`Thin2DDemo.unity` を開きます。同じメッシュの Standard、Paper2D、Acrylic2D を比較でき、Scene ビューを回転すると厚みの違いを確認できます。

[Acrylic2D の設定](shader-acrylic2d.md)と[Thin2D の比較](shader-thin2d.md)も参照してください。
