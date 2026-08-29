# 高度シェーダーモジュール

Decal、Surface Detail、Spatial Interior、Transition の用途、設定手順、制約をまとめます。
4機能は `SabaShader/Illust2D` へ個別に追加でき、不要な機能は `Amount = 0` または
`Progress = 1` の安全な既定値で無効になります。

> このページの図は Package 同梱の `Advanced Shader Suite Demo` を Unity
> 2022.3.22f1 で描画したゴールデン画像です。手で作成した説明図ではありません。

## 有効化とサンプル

Illust2DマテリアルのInspectorで `Select Modules` を開き、次のモジュールを有効にして
`Apply` を押します。

- Decal
- Surface Detail
- Spatial Interior
- Transition

Package Manager の `Samples` から `Advanced Shader Suite Demo` を Import し、
`AdvancedShaderSuiteDemo.unity` を開くと代表設定を確認できます。

![4モジュールの代表設定を同一シーンで比較したUnityキャプチャ](../tests/golden/advanced_shader_suite_demo.png)

`Advanced Shader Demo Object` はサンプル専用の表示補助Componentで、通常の
`Add Component` メニューからは除外しています。アバターやワールドには追加しません。
モジュール構成を変更した後にサンプルがマゼンタになる場合は、各オブジェクトで
`Rebuild Demo Preview` を実行するか、コンポーネントを有効化し直します。
Transitionの3オブジェクトはPlay Modeで自動再生します。手動で確認する場合は
`Auto Animate in Play Mode` を無効にしてから `Progress` を操作します。

## Decal

任意画像をアルベドへ合成します。用途は肌のタトゥー、衣服のロゴ、汚れ、識別記号などです。
UV Space と object-space Projection を切り替えられます。
サンプルでは方向の分かる同一エンブレムを2本のシリンダー側面へ貼り、UV展開に沿う場合と
object-spaceの直方体から投影する場合を比較できます。
このエンブレムは画像生成によるサンプル専用の架空図案で、SabaShaderの正式ロゴではありません。

![UV SpaceとProjection、肌と布の微細質感、空間表現の比較](../tests/golden/advanced_shader_surface_features.png)

### マッピング

| Mapping | 座標 | 用途 | 制約 |
| --- | --- | --- | --- |
| UV Space | 選択した UV0–UV3 | UV上の正確な配置、既存テクスチャと同じ追従 | UV展開と継ぎ目の影響を受ける |
| Projection | object space の直方体 | UV編集なしの局所配置、同じ設定の再利用 | 投影方向に平行な面では消え、曲面の裏側へは回り込まない |

Projection では `Projector Center` が直方体の中心、`Projector Rotation` が度単位の
XYZ Euler角、`Projector Size` の XY が画像の幅と高さ、Z が投影の深さです。
投影方向はローカル +Z で、画像を受ける面は投影方向と逆向きの法線を持つ必要があります。
`Angle Fade` で斜めの面を除外し、`Edge Softness` で直方体の境界をぼかします。

### 合成とマスク

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

## Surface Detail

AOを追加するのではなく、UV上に微細な高さ場を作り、色の微差、micro normal、
roughnessのばらつき、微小反射へ分けて適用します。Skinは不規則な毛穴、Fabricは
縦糸と横糸を模した織りを手続き生成します。

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

## Spatial Interior

mesh表面を窓として、object-space位置と視線から宇宙、星空、cyber空間、泥状空間を
手続き生成します。
外部テクスチャとGrabPassを使わず、髪の内側、服の裏側、裂け目、アクセサリの面へ
局所適用できます。

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

## Transition

`Progress` だけをAnimation Controllerから制御できる登場・退場モジュールです。
`0` が初期状態、`1` が完全なsolid／表示状態です。退場は同じAnimation Clipを逆方向に
再生します。

![上空への分解、glitch出現、液体から固体への遷移](../tests/golden/advanced_shader_transitions.png)

### Mode

| Mode | 動作 | 主な調整値 |
| --- | --- | --- |
| Upward Dissolve | object-spaceの方向に境界が移動し、縁を発光させながらclipする | Direction、Bounds、Noise、Displacement |
| Glitch Spawn | object-spaceのblock単位で表示し、境界付近の頂点をずらす | Block Scale、Edge Width、Displacement |
| Liquid to Solid | clipせず、複数方向の波、水たまり形状、色付けを減衰させてsolidへ戻す | Liquid Amplitude、Wobble、Puddle、Frequency、Speed、Tint |

Upward DissolveとGlitch SpawnはForward、ShadowCaster、Outlineで同じfieldを使用するため、
本体、影、輪郭線の欠け方が一致します。Liquid to Solidは常に表示され、`Progress` に応じて
変形と色だけが変わります。

Liquid to Solidの `Irregular Wobble` は異なる方向と周波数の3波を合成します。`1`を超えると
不規則さに加えて変形量も増加します。`Puddle Initial State` を有効にすると、`Progress = 0`で
meshを `Direction` 軸の `Bounds` 最小値へ圧縮し、軸に直交する方向へ広げます。
`Puddle Thickness` は `Bounds` の高さ範囲に対する厚さ、`Puddle Spread` はobject-spaceの
拡大率です。接地位置は `Bounds.x` で調整します。

### Animation Controllerからの制御

Animation ClipでRendererの次のmaterial propertyへ `0` と `1` のkeyframeを設定します。

```
material._io_github_sabas0ba_transition_Progress
```

VRChatのFX Animatorでは、このClipをStateまたは1D Blend Treeへ割り当て、Avatar Parameterの
floatを遷移条件またはBlend Parameterとして使用します。複数Rendererを同時に動かす場合は、
各Rendererのmaterial propertyを同じClipへ記録します。`Mode`、`Bounds`、`Direction` は
マテリアル側で固定し、通常は `Progress` だけをアニメーションします。

頂点変位は既存頂点しか動かしません。粗いmeshではGlitchやLiquidの変形が大きな面単位に
見えます。また透明な粒子を新規生成する機能ではないため、上空へ分解した破片を長く残す
演出にはParticle Systemなどを併用します。

## 処理順と負荷

Decalをアルベドへ合成し、Surface Detailでmicro normalとroughnessを変更してから
Illust2Dのライティングを行います。Spatial Interiorはライティング後の色を置き換え、
Transitionの発光縁とclipを最後に適用します。

| モジュール | 主な負荷 | `Amount = 0`／無効状態 |
| --- | --- | --- |
| Decal | 画像1回のsample、Projectionの座標計算 | sampleを省略 |
| Surface Detail | 高さ場の複数評価、任意のdetail texture sample | 計算とsampleを省略 |
| Spatial Interior | preset別の3D noise、star field、格子 | 計算を省略 |
| Transition | object-space noise、clip、任意の頂点変位 | `Progress = 1` でclip境界と変位を省略 |

旧衣装、新衣装、露出防止用meshを1本の進行度で切り替える場合は、Transitionを個別に
組み合わせず、[衣装変身バンク](transformation-bank.md)を使用します。

Mobile、Quest向けでは、対象マテリアルを限定し、Spatial InteriorとSurface Detailを
同時に広い画面領域へ使わない構成を推奨します。
