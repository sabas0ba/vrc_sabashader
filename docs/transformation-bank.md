# 衣装変身バンク

`Transformation Bank` は旧衣装と新衣装を1本のAnimation Controller進行度でつなぐShader Core
モジュールです。PC向けを対象とし、`SabaShader/Illust2D` とNonToonで検証しています。

シェーダーだけで衣装GameObjectを有効・無効にはできません。変身中は旧衣装と新衣装のRendererを
有効に保ち、モジュールの `Progress` で各meshの描画範囲を制御します。旧衣装を無効にするのは
バンク完了後です。

## 構成

衣装に使うマテリアルInspectorで `Select Modules` を開き、
`__TransformationBank (io.github.sabas0ba.transformationbank)` を有効にして `Apply` を押します。
既存の `Appearance Transition` と同じマテリアルでは併用しません。双方がclipと頂点変位を行うためです。

| 対象 | Role | 用途 |
| --- | --- | --- |
| 変身前の衣装 | Outgoing | 中盤から退場する旧衣装 |
| 変身後の衣装 | Incoming | 中盤までに出現する新衣装 |

このモジュールはbodyの被覆を保証しません。衣装下のbodyをBlendShapeなどで隠す場合は、変身中に
旧衣装と新衣装が必要とする隠し範囲の和集合を維持してください。

## Animation Controller

Animation Clipから両Rendererの次のmaterial propertyを0から1へ動かします。

```text
material._io_github_sabas0ba_transformationbank_Progress
```

既定timingは次の通りです。数値は `Costume Windows` で変更できます。

| Progress | Outgoing | Incoming |
| ---: | --- | --- |
| 0.00–0.25 | 完全表示 | 非表示 |
| 0.25–0.35 | 完全表示 | 出現開始 |
| 0.35–0.65 | 退場 | 出現 |
| 0.65–0.75 | 退場中 | 完全表示 |
| 0.75–1.00 | 非表示 | 完全表示 |

既定値ではOutgoingとIncomingの表示率の和が1を下回りません。これはbody被覆率ではなくshader上の
表示率です。windowを変更する場合は `Incoming開始 <= Outgoing開始 <= Incoming終了 <= Outgoing終了`
の順序を維持します。

変身中に別の衣装変更を受け付けると現在衣装の判定が不定になるため、FX Animatorはバンク終了まで
次の入力を受け付けない構成を推奨します。

## 共通パラメータ

| Property | 用途 |
| --- | --- |
| Effect Intensity | 頂点変位、発光境界、表面patternの共通倍率。0–4 |
| Vertex Offset | object-space頂点変位量 |
| Edge Emission | clip境界のHDR発光強度 |
| Pattern Emission | 表面patternのHDR発光強度 |
| Noise Scale / Boundary Noise | 境界と流体・霧・炎の粗密 |
| Pattern Scale / Speed | 表面patternの密度と時間変化 |

`Effect Intensity` を上げる場合はSkinnedMeshRendererのboundsも広げ、カメラ角度によるcullingを
確認してください。

## Style

| Style | 表示・変位 | 表面演出 | Particle補助の例 |
| --- | --- | --- | --- |
| Arcane | 方向軸とnoise | 魔術紋様 | 魔力spark |
| Cyber | object-space block | 格子とrim | digital片 |
| Astral | 3D noise | 星とrim | 星粒 |
| Gaia | 下から上へのnoise | 岩のひび | 砂塵・小片 |
| Umbra | noiseとblock | 流れる影 | 影のmote |
| Flame | 上昇turbulence | 大きな炎色noise | 炎片と火の粉 |
| Shatter | cell単位clip | 破片境界と散開 | Triangle／Quad相当のmesh particle |
| Glitch | noiseから粗いblockへ変化 | scanlineと横ずれ | pixel片と断続ノイズ |
| Melt | Outgoingだけ下方向へ融解 | 液体noise | 液滴と飛沫 |
| Cosmic Rift | 中央の裂け目から展開 | 星空と裂け目rim | 背後の星片・ring |
| Magical Sparkle | 下から上へ展開 | cross状sparkle | キラキラした魔法粒子 |
| Mana Mist | 霧noiseから収束 | 柔らかいrim | 周囲から集まる魔力霧 |

Shatterはgeometry shaderでtriangleを分離する方式ではなく、object-space cellとQuad mesh particleを
組み合わせます。そのためNonToonと同じShader Core経路を維持できます。MeltはIncomingの頂点変位と
表面patternを無効にし、新衣装の形状を変えずに出現させます。

## Particle System

Package ManagerのSamplesから `Transformation Bank Demo` をImportすると、12 Styleの表面shaderと
Particle Systemを同期再生できます。

![12 Styleのsurface shaderを比較したUnityキャプチャ](../tests/golden/transformation_bank_demo.png)

この画像はUnity 2022.3のbatch `Camera.Render` を安定させるためParticle rendererを除外した
surface shader比較です。Particle補助演出はsample SceneをPlay Modeで再生して確認します。

DemoのParticleは2系統です。

- Primary: 炎片、破片、pixel、液滴、裂け目、霧など主要形状
- Accent: 火の粉、細かな破片、星、sparkleなど装飾

Inspectorの `Particle Intensity` と `Particle Size` で調整できます。実AvatarではAnimation Clipから
Particle SystemのEmissionまたはGameObject有効状態を制御します。Particleは衣装の描画状態を置き換える
ものではなく、表面shaderの外側へ演出を追加する用途です。

## NonToon

NonToonはShader Coreの `morph`、`base`、`postpixel` phaseを持つため、同じモジュールを追加できます。
Forward、ForwardAdd、Outline、ShadowCasterで同じ表示判定を使用します。本リポジトリではNonToon
0.1.3（`130bea3e6be5183b4fceb60df0062d38ef98067c`）を固定依存としてコンパイルしています。
