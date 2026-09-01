# Mochi Skin World Demo

Mochi Skinの変形を、VRCSDKに依存せずUnity上で確認するWorld展示形式のサンプルです。
無変形のsurfaceと、4個のContact Receiver Proximityを模擬したsurfaceを比較できます。

## 導入

1. Package ManagerでSabaShaderの`Samples`から**Mochi Skin World Demo**をImportします。
2. Shader CoreのProject Settingsで`SabaShader/Illust2D`へ`Mochi Skin`を追加します。
3. `MochiSkinWorldDemo.unity`を開きます。
4. Play Modeへ入ると、右側surfaceの4個のprobeとPressureが位相差付きで動きます。

Edit Modeでは`Contact Driven Surface`の`Mochi Skin World Demo Object`から
`Pressure 0`–`Pressure 3`を直接操作できます。頂点密度によるsilhouette変化を確認できるよう、
preview meshは64×48分割で実行時に生成します。

`Mochi Skin World Demo Object`はサンプル専用です。VRC Contact Receiverの出力を模擬するための
通常のMonoBehaviourであり、アバターやアップロードするWorldへ追加しないでください。
生成するmeshとmaterialは`HideAndDontSave`で保持され、sceneやprojectへ保存されません。

実際のアバターでは、Contact ReceiverのFloatをFX Animatorから次のmaterial propertyへ接続します。

```text
material._io_github_sabas0ba_mochiskin_Pressure0
```

Point 1–3では末尾を`Pressure1`–`Pressure3`へ変更します。詳しい設定は
[高度シェーダーモジュール](https://github.com/sabas0ba/vrc_sabashader/blob/main/docs/modules-advanced.md)
を参照してください。
