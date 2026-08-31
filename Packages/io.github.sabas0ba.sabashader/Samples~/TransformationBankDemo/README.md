# Transformation Bank Demo

旧衣装と新衣装を1本のProgressで切り替えるTransformation Bankのサンプルです。第三者のAvatarや
衣装assetを使用せず、OutgoingをCapsule、IncomingをCylinderで表示します。必要なStyleにはmesh表面の
演出を補助するPrimary／AccentのParticle Systemが含まれます。

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

共通EffectはProgress中央で最大になり、0と1で値と勾配が0になるため、linear keyframeでも端点へ滑らかに
収束します。Style固有のnoiseや頂点変位もRole別表示率で減衰するため、`Costume Windows` の境界をまたいでも
完全表示へ不連続に切り替わりません。衣装の表示区間はMaterial Inspectorから調整できます。
IncomingとOutgoingは相補マスクを使用します。Gaia・FlameなどでNewが下から出現する場合はOldも下から
消失し、Umbraなどのnoise型ではNewが現れる領域からOldが消失します。

`Tools > SabaShader > Transformation Bank Clip Generator` では、Avatar Root、衣装A、衣装B、VFX Styleから
A→B／B→AのAnimation ClipとIncoming／Outgoing Material複製を生成できます。元MaterialとSceneは変更せず、
Outgoing衣装はProgressが1になった最終frameで無効化します。Animator ControllerとParticle Systemへの組み込みは
別工程です。

衣装のMaterialが非対応の場合、Generatorの `Material互換性` に対象Slotと利用可能な対応Shader／Materialが
表示されます。元Materialを変更せずに互換Materialを生成して割り当てるか、既存の対応Materialを割り当てられます。
明示的な割当操作は対象Renderer Slotだけを変更し、Undoに対応します。

Particle Systemは表面shaderを補助するsample設定です。Avatarへ組み込む場合はStyleに対応する
Particle Systemを衣装rootへ移し、同じAnimation ClipでEmissionやGameObjectの有効状態を制御します。
Sample ControllerはStyleごとのProgress区間へEmissionを同期し、端点ではParticleを消去します。Astralと
GaiaではParticleを停止します。汎用Quadは使わず、Flameは小さな火の粉、Umbraは霧粒、Shatterは
Triangle／Quad片、Glitchはpixel片、Meltは液滴と水玉のmeshを使用します。各形状は
`TransformationBankParticleMeshes.asset` に保存され、Particle SystemだけをAvatarへ移した場合もMesh参照を
維持します。UmbraとMana Mistは `TransformationBankMistTexture.asset` と
`TransformationBankMistParticles.mat` によるsoft particleを使用します。
