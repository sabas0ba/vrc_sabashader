# SabaShader/Acrylic2D

印刷面の上へ色付きのアクリル層を合成し、側面色・内部散乱光・視差で成形品のように見せる PC 向け BiRP シェーダーです。

![同じメッシュの Standard、Paper2D、Acrylic2D を Unity 2022.3.22f1 で比較した画像。下段の 2 例が Acrylic2D](../docs/assets/thin2d-demo.png)

この画像は Package 同梱の `Thin2D Demo` の描画結果です。下段はアクリル色と印刷色を変えた 2 例で、右は表面の影を無効にしています。例では `Local Z thickness = 0.18`、`擬似厚み = 0.9`、色付きの縁と白い余白を設定しています。

## 印刷面とアクリル層

- `Base Texture` と `Base Color` が印刷面です。アルファ値は印刷色の被覆率として使い、透過描画には使いません。
- `アクリル層の強さ` (`_Opacity`) は、印刷色の上に合成するアクリル色の強さです。出力は不透明で、深度を書き込みます。重なった 3D 面は 1 回だけ表示されます。
- `アクリル層の色` は表面へ合成する色、`Paper Grain` は下の印刷面の粒状感です。

## 厚みと光

- `Local Z thickness` (`_DepthScale`) は各 Renderer のローカル Z を縮小します。`1` が元の形状、`0.02` がほぼ平面です。
- `擬似厚み` (`_Thickness`) は、シルエット側のアクリル側面、内部散乱光、視差の強さをまとめて調整します。追加メッシュは生成しません。
- `印刷面の屈折` (`_RefractionStrength`) と `屈折スケール` (`_RefractionScale`) は、浅い視線角で印刷面の UV をずらします。
- `内部散乱光` (`_InternalLight`) は、表面と印刷面の間に入る色付きの光を調整します。
- `白い余白の幅` は外側の余白、`外形反射の幅` は追加する色付き輪郭です。
- `表面の影` と `投影影` は個別に切り替えられます。

![同じ Thin2D Demo を横方向から描画した画像。Acrylic2D の側面と白い余白を確認できる](../docs/assets/thin2d-demo-side.png)

`_DepthScale` を小さくすると側面のジオメトリも薄くなります。側面の色分けや穴の縁など、実際の成形品に固有の形状はメッシュ側で用意してください。

## Unity で確認する

Package Manager から `Thin2D Demo` を Import し、`Thin2DDemo.unity` を開きます。同じメッシュの Standard、Paper2D、Acrylic2D を比較でき、Scene ビューを回転すると厚みの違いを確認できます。

[Paper2D の設定](shader-paper2d.md)と[Thin2D の比較](shader-thin2d.md)も参照してください。
