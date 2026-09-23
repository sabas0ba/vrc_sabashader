# Transition

`Progress` だけをAnimation Controllerから制御できる登場・退場モジュールです。
`0` が初期状態、`1` が完全なsolid／表示状態です。退場は同じAnimation Clipを逆方向に
再生します。

![上空への分解、glitch出現、液体から固体への遷移](../tests/golden/advanced_shader_transitions.png)

## Mode

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

## Animation Controllerからの制御

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
