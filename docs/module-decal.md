# Decal

任意画像をアルベドへ合成します。用途は肌のタトゥー、衣服のロゴ、汚れ、識別記号などです。
UV Space と object-space Projection を切り替えられます。
サンプルでは方向の分かる同一エンブレムを2本のシリンダー側面へ貼り、UV展開に沿う場合と
object-spaceの直方体から投影する場合を比較できます。
このエンブレムは画像生成によるサンプル専用の架空図案で、SabaShaderの正式ロゴではありません。

![UV SpaceとProjection、肌と布の微細質感、空間表現の比較](../tests/golden/advanced_shader_surface_features.png)

## マッピング

| Mapping | 座標 | 用途 | 制約 |
| --- | --- | --- | --- |
| UV Space | 選択した UV0–UV3 | UV上の正確な配置、既存テクスチャと同じ追従 | UV展開と継ぎ目の影響を受ける |
| Projection | object space の直方体 | UV編集なしの局所配置、同じ設定の再利用 | 投影方向に平行な面では消え、曲面の裏側へは回り込まない |

Projection では `Projector Center` が直方体の中心、`Projector Rotation` が度単位の
XYZ Euler角、`Projector Size` の XY が画像の幅と高さ、Z が投影の深さです。
投影方向はローカル +Z で、画像を受ける面は投影方向と逆向きの法線を持つ必要があります。
`Angle Fade` で斜めの面を除外し、`Edge Softness` で直方体の境界をぼかします。

## 合成とマスク

| プロパティ | 説明 |
| --- | --- |
| Amount | Decal全体の適用率。`0` でテクスチャを参照しない |
| Texture / Tiling / Offset | 合成するRGBA画像と使用領域 |
| Tint | RGBは画像へ乗算し、アルファは合成率へ乗算する |
| Blend Mode | Alpha、Multiply、Add |
| UV Channel | UV Spaceで使用するUVセット |
| Mask Channel | Illust2DのShared Maskで適用部位を限定する。Noneで全面 |

複数の独立したDecalを1マテリアルへ重ねる機能は持ちません。複数枚が必要な場合は
テクスチャを事前に合成するか、同じモジュールの複製に別の `uniqueID` を割り当てます。
