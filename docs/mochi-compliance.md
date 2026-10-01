# Mochi Skinのへこみやすさ

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

## 接触開始

Worldデモは回転・scaleを反映したprobe頂点と曲面の離隔から最初の接触を求め、
その位置を凹みの中心にします。接平面近似で生じていた接触位置と凹み中心のずれを減らしています。
形状全体に追従する物理シミュレーションではなく、接触後の輪郭は引き続きパラメトリック近似です。
実アバターのContact Receiverでは、sender/receiver半径に合わせたContact Thresholdの校正が必要です。

参考: [Unity Mesh.GetAllBoneWeights](https://docs.unity3d.com/ja/2020.3/ScriptReference/Mesh.GetAllBoneWeights.html)、
[Unity Animator.GetBoneTransform](https://docs.unity.cn/jp/2022.3/ScriptReference/Animator.GetBoneTransform.html)。
