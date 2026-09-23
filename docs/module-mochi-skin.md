# Mochi Skin

VRChatのContact Receiverが出力する4つのFloatを、UV上の固定した4接触点の押し込み量へ
変換します。中央を頂点法線の内側へ変位させ、周囲へ小さい盛り上がりを作り、同じ高さ場の
UV勾配をtangent-space法線へ加えます。Shader Coreの`morph`と`base`を使用するため、
`SabaShader/Illust2D`のほか、これらのphaseを持つ
[NonToon](https://github.com/lilxyzw/NonToon)などのShader Core shaderへ追加できます。

![NonToon 上の Mochi Skin へ 4 つの形状を接触させた Unity の描画結果](../tests/golden/mochi_skin_world_demo.png)

## Material設定

Shader CoreのProject Settingsで対象shaderへ`Mochi Skin`を追加します。まず`Amount = 1`、
各`Pressure = 1`として位置を確認し、設定後にPressureを0へ戻します。

| プロパティ | 説明 |
| --- | --- |
| Amount | モジュール全体の適用率。既定値の0で変形しない |
| UV Channel | 4点の配置に使うUV0–UV3 |
| Depth | Pressureが1のときに頂点を内側へ動かす最大距離。world単位 |
| Outer Bulge | 凹みの外周に作る盛り上がり。Depthに対する比率 |
| Indent Spread | 接触footprintのうち中央の凹みに使う範囲 |
| Edge Softness | 凹み、外周、無変位領域の間を補間する幅 |
| Contour Irregularity / Irregularity Scale | 規則的な輪郭を崩す量と低周波の密度 |
| Contact Threshold | Proximityを変形へ反映し始める値。接触前の反応を除く |
| Contact Softness | thresholdから最大圧力までの立ち上がり曲線 |
| Normal Strength | 高さ場から作る法線の強さ。頂点変位量は変えない |
| Compliance / Compliance Mask | 全体のへこみやすさと、黒を硬い領域・白を柔らかい領域として使うRチャンネルマスク |
| Contact Point 0–3 | XYがUV中心、ZWが接触footprintのUV半径 |
| Contact Shape 0–3 | XがUV上の角度、Yが形状指数、Zが輪郭のseed。2は楕円、大きい値は角丸矩形 |
| Pressure 0–3 | Animatorから駆動する0–1のProximity |

Debug shaderのUV0–UV3表示またはDCCのUV editorで、各Receiverを置く肌位置に対応するUVを
確認します。UV islandが重なるmeshでは、同じUV範囲にある別部位も同時に変形します。
Humanoidの骨・ウェイトからUV3.xへ硬さを自動生成する手順は後述します。既存UV3を使うモデルでは、
自動生成を実行せず、Compliance Maskを使用してください。

## へこみやすさとマスク

変形量は `Depth × 接触圧 × Compliance × Compliance Mask(R) × 骨マスク` です。
`Compliance` は0で変形なし、1で設定した深さです。テクスチャは黒が硬い領域、白が柔らかい領域です。
テクスチャのsRGBを無効にし、接触点と同じUVチャンネルへ配置してください。未指定は白です。
頂点と法線で同じマスクを使用し、法線にはマスク境界の勾配も反映します。
頂点変形には十分なmesh分割が必要です。高周波のマスクは頂点密度に合わせてぼかしてください。

## Humanoidからの自動生成

Shader Coreの頂点入力にHumanoidの骨ID・ウェイトはありません。そのため、ランタイムスクリプトや
外部依存を追加せず、Editorで骨マスクを生成します。骨ごとの手動登録は不要ですが、導入操作は一度必要です。

1. Mochi Skin対応materialを割り当てたHumanoidアバターをHierarchyで選択します。
2. `Tools > SabaShader > Mochi Skin > Bake Humanoid Compliance` を実行します。
3. 生成されたmeshとmaterialがRendererへ割り当てられます。シーンまたはPrefabを保存してください。

有効なHumanoid Avatar、読み取り可能なmesh、未使用のUV3が必要です。
UV3.xを骨マスク専用に使用します。既存UV3や接触用UV3がある場合は停止し、上書きしません。
元のmesh/materialは変更せず、`Assets/MochiSkinGenerated`へ複製します。Rendererの割当はUndo可能です。
Undoしても生成assetは残ります。再生成する場合は元のmesh/materialへ戻してから実行してください。

bind poseの骨区間・関節からの距離を骨長で正規化し、全頂点ウェイトで合成します。
近傍は0.15、十分に離れた領域は1へ滑らかに遷移します。補助骨は最も近いHumanoid祖先へ対応させ、
対応できないウェイトは1とします。現在のアニメーション姿勢には依存しません。
これは解剖学的な硬さの推定ではありません。骨配置や体型による差はテクスチャで補正してください。
骨・ウェイト・meshを変更した場合は再生成が必要です。VRChatへのアップロード時には生成mesh/materialだけを使用します。

参考: [Unity Mesh.GetAllBoneWeights](https://docs.unity3d.com/ja/2020.3/ScriptReference/Mesh.GetAllBoneWeights.html)、
[Unity Animator.GetBoneTransform](https://docs.unity.cn/jp/2022.3/ScriptReference/Animator.GetBoneTransform.html)。

## Worldデモ

Package ManagerのSamplesから`Mochi Skin World Demo`をImportし、
`MochiSkinWorldDemo.unity`を開きます。NonToonをbaseにした高roughnessのさらさら肌と、
低roughnessのてかり肌を並べています。各surfaceでは球、円柱、板、capsuleを異なる向きで
接触させ、円、細長い形、角丸矩形のfootprintを比較できます。Play Modeではprobeの接近、
接触、penetrationを位相差付きで自動再生します。

このsampleはVRCSDKに依存せず、Contact ReceiverのFloat出力だけを通常のMonoBehaviourで
模擬します。`Contact Threshold`まではprobeだけが接近してsurfaceを変形せず、thresholdで
probe表面がsurfaceへ到達します。それ以上のProximityだけがpenetrationと凹みになります。
表示用Componentはsample専用であり、アバターやアップロードするWorldへは追加しません。
実利用時の接続は次節のFX Animator設定を使用します。

## 接触開始

Worldデモは回転・scaleを反映したprobe頂点と曲面の離隔から最初の接触を求め、
その位置を凹みの中心にします。接平面近似で生じていた接触位置と凹み中心のずれを減らしています。
形状全体に追従する物理シミュレーションではなく、接触後の輪郭は引き続きパラメトリック近似です。
実アバターのContact Receiverでは、sender/receiver半径に合わせたContact Thresholdの校正が必要です。

## Contact ReceiverとFX Animator

接触点ごとに、肌表面へ追従するGameObjectと`VRC Contact Receiver`を1個用意します。
例としてPoint 0は次のように対応付けます。

1. ReceiverをHead bone配下の頬位置へ置き、ShapeをSphere、Receiver Typeを`Proximity`にする。
2. Collision Tagsへ`Finger`と`Hand`を追加し、`Allow Others`を有効にする。自分の手にも反応させる場合だけ`Allow Self`を有効にする。
3. ParameterをFloatの`Mochi/P0`にする。他の3点は`Mochi/P1`から`Mochi/P3`を使用する。
4. FX Animatorへ同名のFloatを追加する。Contact用parameterはExpression Parametersへ追加しなくてもよい。
5. 0と1のAnimation Clipを持つ1D Blend Treeを作り、`Mochi/P0`をBlend Parameterにする。Clipでは対象Rendererの次のmaterial propertyだけを記録する。

```
material._io_github_sabas0ba_mochiskin_Pressure0
```

Point 1–3では末尾を`Pressure1`–`Pressure3`へ変え、独立したFX layerで同様に駆動します。
VRChatのProximityは接触位置そのものではなく、Receiver中心への近さを0–1で出力します。
このため1個のReceiver内で凹み中心が指に追従する方式ではなく、4個の固定領域から最も近い
領域を連続的に押す方式です。Receiverの外周へsenderが入った時点からProximityは増えるため、
未調整では物理的な接触前に凹み始めます。`Contact Threshold`をsenderが肌へ到達する時点の値へ
合わせ、`Contact Softness`で接触後の立ち上がりを調整します。

他アバターからの接触を各clientで評価させる場合は`Local Only`を無効にします。
`Local Only`を有効にしたReceiverは装着者のlocal clientに限定されます。一方、無効なReceiverは
avatar performance rankのContacts数へ算入されます。動作はVRChat側のAvatar Interactions設定と
Safety設定によって無効化される場合があります。

VRChatはhumanoid avatarの標準Hand／Finger Contact Senderを自動生成するため、組み込みの
`Hand`、`Finger` tagを使うと他アバターとの互換性を確保できます。詳細はVRChat公式の
[Contacts](https://creators.vrchat.com/common-components/contacts/)と
[Built-In Contact Tags](https://creators.vrchat.com/common-components/contacts/built-in-contact-tags/)を参照してください。

## 制約

- 頂点変位は既存頂点だけを動かす。接触範囲内の頂点が少ないmeshでは、法線だけが変化してsilhouetteの凹みは粗くなる
- 変位方向は各頂点の法線であり、体積保存、自己衝突、隣接頂点による弾性計算は行わない
- 4点の範囲が重なる場合は高さと勾配を加算するため、最大変位はDepthを超える場合がある
- UV seamをまたぐ1個の接触点は連続しない。seamの両側を別のPointとして設定する
- Receiverはboneへ追従するが、blend shapeによる肌表面の移動には追従しないため、表情差が大きい場合はReceiver半径と位置を調整する
