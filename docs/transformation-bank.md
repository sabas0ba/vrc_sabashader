# 衣装変身バンク

`Transformation Bank` は、旧衣装、新衣装、露出防止用の `Safety Cover` を1本の
Animation Controller の進行度でつなぐ Shader Core モジュールです。PC向けを対象とし、
`SabaShader/Illust2D` と NonToon で検証しています。

シェーダーだけで衣装オブジェクトを有効・無効にはできません。変身中は3つの Renderer を
有効に保ち、モジュールの `Progress` で各meshを描画する範囲を制御します。旧衣装を無効に
するのはバンク完了後です。

## 構成

Shader Core の Project Settings で、衣装に使うシェーダーへ `Costume Transformation Bank`
を追加します。既存の `Appearance Transition` と同じマテリアルでは併用しません。双方が
`clip` と頂点変位を行うためです。

次の3種類のマテリアルを用意し、同じ `Progress` を与えます。

| 対象 | Role | 用途 |
| --- | --- | --- |
| 変身前の衣装 | Outgoing | 中盤から退場する旧衣装 |
| 変身後の衣装 | Incoming | 中盤までに出現する新衣装 |
| 露出防止mesh | Safety Cover | 衣装が入れ替わる間を完全に覆う不透明mesh |

Safety Cover は、ボディスーツ、インナー、身体を少し膨らませたshell、岩の殻、影のローブ
など、モデルに合う形を使用します。常設インナーが全身を必要な範囲まで覆う場合は、専用の
Safety Cover を省略できます。

衣装の下のbodyをBlendShapeなどで隠す場合、変身中は旧衣装と新衣装が隠す範囲の和集合を
使用します。新衣装とSafety Coverが完全表示される前にbodyの隠し方を解除すると、meshの
穴または裸体が見える可能性があります。

## Animation Controller

Animation Clipから各Rendererの次のmaterial propertyを0から1へ動かします。

```
material._io_github_sabas0ba_transformationbank_Progress
```

既定タイミングは次の通りです。数値はマテリアルの `Costume Windows` と
`Safety Cover Window` で変更できます。

| Progress | Outgoing | Incoming | Safety Cover |
| ---: | --- | --- | --- |
| 0.00–0.10 | 完全表示 | 非表示 | 非表示 |
| 0.10–0.30 | 完全表示 | 非表示～出現開始 | 出現 |
| 0.30–0.35 | 完全表示 | 出現中 | 完全表示 |
| 0.35–0.65 | 退場 | 出現 | 完全表示 |
| 0.65–0.70 | 退場中 | 完全表示 | 完全表示 |
| 0.70–0.90 | 非表示～退場中 | 完全表示 | 退場 |
| 0.90–1.00 | 非表示 | 完全表示 | 非表示 |

この既定値では、すべての時点で旧衣装、新衣装、Safety Coverのいずれか1つが完全表示に
なります。ただし、Safety Coverのmesh自体が覆っていない部位までは保証できません。Scene
Viewで全方向から確認し、腕上げ、開脚、しゃがみなど衣装とbodyの間が見えるposeでも確認します。

windowを変更する場合は、少なくとも次の順序を維持します。各windowの開始値は終了値以下に
します。この条件を崩すと、3つとも部分表示または非表示になる区間が発生します。

```
Safety Cover 出現終了 <= Outgoing 退場開始
Incoming 出現終了     <= Safety Cover 退場開始
```

Animation Clipでは3つのRendererへ同じ0、1のkeyframeを記録します。`Role`、`Style`、色、
方向、タイミングはマテリアル側へ固定します。変身中に別の衣装変更を受け付けると現在衣装の
判定が不定になるため、FX Animatorはバンク終了まで次の入力を受け付けない構成を推奨します。

## Style

| Style | 表示境界 | 表面演出 | Safety Coverの例 |
| --- | --- | --- | --- |
| Arcane | 方向軸とnoise | 輪と直線状の魔術紋様 | 不透明な魔力ボディスーツ |
| Cyber | object-space block | 格子とrim | solid hologram shell |
| Astral | 3D noise | 星と強いrim | 星界の人型shell |
| Gaia | 下から上へのnoise | 岩のひび | 土、岩、結晶の殻 |
| Umbra | noiseとblock | 流れる影とrim | 不透明な影のローブ |

`Pattern Color` と `Edge Color` はHDR色です。Safety Coverでは中央区間の基底色を
`Safety Cover Color` で置き換え、アルファを1にします。Safety Cover側のシェーダーも
Opaqueに設定してください。Transparentのrender queueや半透明blendを使うと、深度と描画順に
よって内側が見えるため、安全条件には使用できません。

頂点変位は既存頂点だけを動かします。粗い衣装meshではCyberのblockやGaiaの崩れが大きな面の
移動に見えます。変位量を増やす場合はSkinnedMeshRendererのboundsも広げ、カメラ角度による
cullingを確認してください。

## NonToon

NonToonはShader Coreの `morph`、`base`、`postpixel` phaseを持つため、同じモジュールを追加
できます。`base` phaseは通常描画とpixel clipの双方から呼ばれ、Forward、ForwardAdd、Outline、
ShadowCasterで同じ表示判定を使用します。本リポジトリではNonToon 0.1.3
(`130bea3e6be5183b4fceb60df0062d38ef98067c`)をUnity検証プロジェクトの固定依存として
コンパイルしています。

Safety CoverにNonToonを使う場合はRendering ModeをOpaqueにします。衣装本体のRendering Modeは
元の材質に合わせられますが、半透明衣装だけで露出防止を成立させることはできません。

## 外側のVFX

モジュールが生成するのはmesh表面のclip、発光、色、頂点変位です。空中へ残る破片、魔法陣、
リング、砂塵、煙には別のmeshまたはParticle Systemを使用し、同じAnimation Clipで有効時間と
発光強度を制御します。外側のVFXは裸体を隠す保証には使わず、Safety Coverを残します。
