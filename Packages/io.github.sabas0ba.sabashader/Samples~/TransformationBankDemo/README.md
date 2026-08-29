# Transformation Bank Demo

旧衣装、新衣装、Safety Coverを1本のProgressで切り替えるTransformation Bankのサンプルです。
第三者のAvatarや衣装assetを使用せず、3層のcapsule shellで被覆関係を表示します。

## 導入

1. Package ManagerでSabaShaderのSamplesから **Transformation Bank Demo** をImportします。
2. Illust2DマテリアルのInspectorで `Select Modules` を開き、
   `__TransformationBank (io.github.sabas0ba.transformationbank)` を有効にして `Apply` を押します。
3. `TransformationBankDemo.unity` を開きます。
4. Play Modeへ入り、上段のArcane、Cyber、Astral、Gaia、Umbraを確認します。

上段は5 Styleを同じProgressで自動再生します。下段はProgress 0、0.25、0.5、0.75、1の
固定snapshotです。各展示は次の3 Rendererを重ねています。

| shell | Role | 表示内容 |
| --- | --- | --- |
| 外側 | Outgoing | 変身前の衣装 |
| 中央 | Incoming | 変身後の衣装 |
| 内側 | Safety Cover | 衣装切り替え中の不透明な被覆 |

展示を選択して `Auto Animate in Play Mode` を無効にすると、Inspectorの `Progress` で
手動スクラブできます。実利用では次のmaterial propertyをAnimation Controllerから制御します。

```text
material._io_github_sabas0ba_transformationbank_Progress
```

このSampleのcapsuleは時間的な被覆関係を示すためのものです。実際のAvatarでは、モデルに合う
Safety Cover meshとbody BlendShapeを用意し、全方向とpose変化を確認してください。
