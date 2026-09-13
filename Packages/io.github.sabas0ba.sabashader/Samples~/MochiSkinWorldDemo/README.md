# Mochi Skin World Demo

Mochi Skinの変形を、VRCSDKに依存せずUnity上で確認するWorld展示形式のsampleです。NonToonをbaseに、さらさらした高roughnessの肌と、てかりのある低roughnessの肌を比較できます。各surfaceへ球、円柱、板、capsuleを異なる向きで接触させます。

## 導入

1. VPMで[NonToon](https://github.com/lilxyzw/NonToon)を導入します。
2. Package ManagerでSabaShaderの`Samples`から**Mochi Skin World Demo**をImportします。
3. Shader CoreのProject Settingsで`NonToon`へ`Mochi Skin`を追加し、NonToon標準の`Shade`と`Specular`も有効にします。
4. `MochiSkinWorldDemo.unity`を開きます。
5. Play Modeへ入ると、両方のsurfaceで4個のprobeが位相差付きで接近・接触・押し込みを繰り返します。

NonToonが無いprojectでは`SabaShader/Illust2D`へfallbackします。この場合はShader CoreのProject SettingsでIllust2DへMochi Skinを追加してください。

Edit Modeでは各surfaceの`Mochi Skin World Demo Object`から、次を直接変更できます。

- `Skin Finish`: Dry／Glossyの肌色とroughness
- `Pressure 0`–`Pressure 3`: Contact Receiver Proximityの模擬値
- `Depth`／`Outer Bulge`／`Indent Spread`: 凹みの強さ、外周の盛り上がり、範囲
- `Edge Softness`: 凹みから無変位領域までの輪郭幅
- `Contour Irregularity`／`Irregularity Scale`: 規則的な輪郭を崩す量と密度
- `Contact Threshold`／`Contact Softness`: 接触開始位置と圧力の立ち上がり

`Contact Threshold`以下ではprobeだけがsurfaceへ接近し、変形は発生しません。thresholdでprobeの表面が肌へ到達し、それ以上のProximityだけをpenetrationとして凹みへ変換します。Contact Receiverの半径やsender形状に合わせ、実機でも接触時点とthresholdを調整してください。

preview meshは72×54分割で実行時に生成します。`Mochi Skin World Demo Object`はsample専用の通常のMonoBehaviourであり、アバターやアップロードするWorldへ追加しないでください。生成するmesh、material、textureは`HideAndDontSave`で保持され、sceneやprojectへ保存されません。

接触輪郭は各probe用に設定したUV上の近似形状です。任意の接触物のmeshから輪郭を自動推定する機能や物理シミュレーションではありません。デモの接触開始位置は回転・scaleを含むprobe meshと肌の接平面から求めます。

実際のアバターでは、Contact ReceiverのFloatをFX Animatorから次のmaterial propertyへ接続します。

```text
material._io_github_sabas0ba_mochiskin_Pressure0
```

Point 1–3では末尾を`Pressure1`–`Pressure3`へ変更します。詳しい設定は[高度シェーダーモジュール](https://github.com/sabas0ba/vrc_sabashader/blob/main/docs/modules-advanced.md)を参照してください。
