# Thin2D シェーダー

`Paper2D` と `Acrylic2D` は、3D メッシュを紙やアクリル作品のように見せる PC 向け BIRP シェーダーです。元のメッシュを入力として使い、Shader Core の頂点変形、ライティング、ピクセル処理フックを利用します。

両シェーダーの `Local Z thickness` (`_DepthScale`) は各 Renderer のローカル Z 座標を縮小します。`1` は元の形状、`0.02` はほぼ平面です。厚みを縮めても元の法線を陰影の計算に使います。複数 Renderer を一体として薄くする場合はメッシュを結合するか、部品の配置も調整してください。

## Paper2D

シェーダー名は `SabaShader/Paper2D` です。

- `Flat shading` と `Shade boundary` は 3D 面の陰影を段階的にし、奥行き感を抑えます。
- 外形線はメッシュ法線に沿って拡張した裏面を描画します。`Silhouette width` はワールド空間で指定します。
- `Cull` を `Off` にすると両面を描画し、`Backface Color` が裏面に適用されます。
- `Alpha Mode` の `Cutout` はテクスチャのアルファを `_Cutoff` で切り抜きます。
- `Backface Color` と `Backface Strength` で裏面の色差を調整します。
- `Paper Grain` は UV を基準にした微細な明暗変化です。テクスチャを追加で使用しません。
- `Edge Color`、`Edge Width`、`Edge Intensity` はテクスチャのアルファ境界に加える色です。メッシュ外形線は別の設定です。
- `白い余白の幅` は暗い外形線のさらに外側へ余白を描きます。`0` にすると余白を消せます。
- `表面の影` はメッシュ上の明暗、`投影影` は他の物体へ落とす影を切り替えます。

## Acrylic2D

シェーダー名は `SabaShader/Acrylic2D` です。

- `Flat shading` は 3D 面の陰影を段階的にします。
- `Base Texture` と `Base Color` が紙状の印刷面です。アルファ値は印刷色の被覆率として使い、透過描画には使いません。
- `アクリル層の強さ` (`_Opacity`) は、印刷色の上に合成するアクリル色の強さです。出力は不透明で、深度を書き込みます。重なった3D面は1回だけ表示されます。
- `擬似厚み` (`_Thickness`) は、シルエット側のアクリル側面、内部散乱光、視差の強さをまとめて調整します。
- `印刷面の屈折` (`_RefractionStrength`) と `屈折スケール` (`_RefractionScale`) は、視線角が浅くなったときに印刷面のUVをずらします。
- `内部散乱光` (`_InternalLight`) は、表面と印刷面の間に入る色付きの光を調整します。
- `アクリル層の色` は表面へ合成する色、`Paper Grain` は下の印刷面の粒状感です。
- `白い余白の幅` は外側の余白を描きます。`外形反射の幅` は必要に応じて追加する色付き輪郭です。
- `擬似厚み` は追加メッシュを生成せず、視線角に応じた側面色、内部散乱光、印刷面の視差を合成します。
- `表面の影` と `投影影` は個別に切り替えられます。

`_DepthScale` を小さくすると側面のジオメトリも薄くなります。側面の色分けや穴の縁など、実際の成形品に固有の形状が必要な場合はメッシュ側で用意してください。

## Unity での描画確認

Package Manager から `Thin2D Demo` を Import して `Thin2DDemo.unity` を開くと、同じ 3D メッシュを Standard、Paper2D、Acrylic2D で比較できます。Scene ビューで横に回すと厚みの違いを確認できます。

CI 用 Unity プロジェクトをセットアップした後、Unity 2022.3.22f1 で `SabaShader.CI.Thin2DPreview.RunBatch` を実行すると、両シェーダーをアルファ付き円形テクスチャに適用して描画します。出力は `_test_artifacts/Paper2D-unity-preview.png` と `_test_artifacts/Acrylic2D-unity-preview.png` です。中央、輪郭、背景の画素値がログへ出力され、描画や輪郭に問題がある場合は終了コード 1 を返します。
