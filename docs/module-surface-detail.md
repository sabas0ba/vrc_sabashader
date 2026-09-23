# Surface Detail

AOを追加するのではなく、UV上に微細な高さ場を作り、色の微差、micro normal、
roughnessのばらつき、微小反射へ分けて適用します。Skinは不規則な毛穴、Fabricは
縦糸と横糸を模した織りを手続き生成します。

![Decal、肌と布の Surface Detail、Spatial Interior を Unity で比較した画像](../tests/golden/advanced_shader_surface_features.png)

| プロパティ | 説明 |
| --- | --- |
| Amount | 全体の適用率 |
| Mode | Skin または Fabric |
| Scale | UV内の模様密度。モデルの実寸ではなくUV基準 |
| Albedo Variation | 微細な明暗差。強すぎると汚れに見える |
| Normal Strength | micro normalの強さ |
| Roughness Variation | 粗さと反射の細かな分断量 |
| Pore | Skinの毛穴の深さ |
| Weave | Fabricの織りの高さ |
| Sheen / Sheen Color | Skinの小さい反射、Fabricのgrazing sheen |
| Mask Channel | Shared Maskで肌や衣服などの部位を限定する |

`Detail Texture` を指定すると、RGBの輝度を色とroughnessのばらつきへ、RGを
追加のdetail normalへ使用します。専用テクスチャを使う場合はRGの中立値を
`0.5` にしてください。手続き模様とテクスチャは加算されます。

この機能は視差遮蔽や頂点変位を行わないため、シルエットは変化しません。
UVの密度が部位ごとに違うモデルでは `Scale` の見え方も変わります。皮膚の
subsurface scatteringや布の異方性BRDFは実装していません。
