# Spatial Interior

mesh表面を窓として、object-space位置と視線から宇宙、星空、cyber空間、泥状空間を
手続き生成します。
外部テクスチャとGrabPassを使わず、髪の内側、服の裏側、裂け目、アクセサリの面へ
局所適用できます。

![Decal、Surface Detail、Spatial Interior を Unity で比較した画像。Spatial Interior は表面内の空間表現](../tests/golden/advanced_shader_surface_features.png)

| プロパティ | 説明 |
| --- | --- |
| Amount | 空間表現への置き換え量 |
| Preset | Universe、Starfield、Cyber、Mud |
| Side | Front、Back、Both。裏面だけならBack |
| Region | Full Surface またはUV上のRift |
| Color A / Color B / Emission | 背景色、nebula色、発光量 |
| Scale / Depth / Parallax | 空間模様の密度、奥行き、視線移動量 |
| Star Density / Star Size | 星の量と大きさ |
| Nebula / Nebula Scale | nebulaの強さと密度 |
| Time Scale | 模様を時間で移動する速度。`0` で固定 |
| Mask Channel | Shared Maskによる部位指定 |
| Rift Center / Rift Size | UV上の裂け目の中心と大きさ |
| Rift Noise / Edge Width / Edge Color | 裂け目の乱れと発光縁 |

| Preset | 生成内容 | 主な調整値 |
| --- | --- | --- |
| Universe | Color A/Bで着色するstar fieldとnebula | Color A/B、Star、Nebula |
| Starfield | 暗い背景に大小2層の星と薄い天の川状の帯 | Star Density、Star Size、Nebula |
| Cyber | cyan／magentaの3D格子、cell単位のsignal pulse | Scale、Depth、Time Scale |
| Mud | 複数scaleのnoiseを歪めた粘性のある泥状field | Scale、Nebula Scale、Time Scale |

Universe以外は用途を選んだ時点で識別できる固定paletteを持ちます。`Emission` は4presetに
共通して適用されます。Starfieldの星量、Cyberの格子密度、Mudの塊の大きさは既存の
共通プロパティで調整できます。

髪内側や服裏側で `Side = Back` を使う場合は、マテリアルの `Cull` を `Off` にします。
サンプルの `Spatial Back Face` は裏面の効果を外側から観察できるよう、実行時に
複製したmeshの法線を内向きにしています。生成meshはシーンへ保存しません。

この機能はmesh表面を置き換えるため、実ジオメトリの無い空中へ穴を発生させることは
できません。空間を割る演出では、裂け目用の板またはmeshを用意します。深度へ穴を開けず、
背後のオブジェクトを別空間へ置き換えるポータルでもありません。
