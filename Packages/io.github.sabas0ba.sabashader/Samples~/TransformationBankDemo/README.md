# Transformation Bank Demo

旧衣装と新衣装を1本のProgressで切り替えるTransformation Bankのサンプルです。第三者のAvatarや
衣装assetを使用せず、OutgoingをCapsule、IncomingをCylinderで表示します。各Styleにはmesh表面の
演出を補助する2系統のParticle Systemが含まれます。

## 導入

1. Package ManagerでSabaShaderのSamplesから **Transformation Bank Demo** をImportします。
2. Illust2DマテリアルのInspectorで `Select Modules` を開き、
   `__TransformationBank (io.github.sabas0ba.transformationbank)` を有効にして `Apply` を押します。
3. `TransformationBankDemo.unity` を開きます。
4. Play Modeへ入り、12 Styleの同期再生を確認します。

上3段は12 Styleを同じProgressで自動再生します。下段はProgress 0、0.25、0.5、0.75、1の
固定snapshotです。

| shape | Role | 表示内容 |
| --- | --- | --- |
| Capsule | Outgoing | 変身前の衣装 |
| Cylinder | Incoming | 変身後の衣装 |

展示を選択するとInspectorから次を調整できます。

- `Effect Intensity`: 頂点変位、発光境界、表面patternの共通倍率
- `Particle Intensity`: Particleの発生数
- `Particle Size`: Particleの大きさ
- `Progress`: 手動scrub

実利用では次のmaterial propertyをAnimation Controllerから制御します。

```text
material._io_github_sabas0ba_transformationbank_Progress
```

Particle Systemは表面shaderを補助するsample設定です。Avatarへ組み込む場合はStyleに対応する
Particle Systemを衣装rootへ移し、同じAnimation ClipでEmissionやGameObjectの有効状態を制御します。
ShatterとGlitchはQuad mesh particle、Flameは炎片と火の粉、Meltは液滴として設定しています。
