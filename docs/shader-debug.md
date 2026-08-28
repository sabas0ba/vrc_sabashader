# Debug shader

`SabaShader/Debug` は、mesh と Built-in Render Pipeline から shader に渡される情報を可視化します。色や質感の制作には使わず、avatar／world の設定確認に使用します。

## 使用方法

1. 確認対象の material で `SabaShader/Debug` を選ぶ
2. `Display Mode` で可視化する情報を選ぶ
3. 裏面も確認する場合は `Culling` を `Off` にする

元の texture や material 設定は参照しません。確認後は元の shader に戻してください。

## 表示モード

| モード | 表示内容 |
| --- | --- |
| `Wireframe` | geometry shader で生成した barycentric coordinates による三角形の辺 |
| `VertexColor` | vertex color の RGB |
| `VertexAlpha` | vertex color の alpha |
| `UV0`–`UV3` | 各 UV channel。R が U、G が V |
| `WorldPosition` | world position の小数部 |
| `ObjectPosition` | object position の小数部 |
| `WorldNormal` | world-space normal を `-1..1` から `0..1` に変換した色 |
| `WorldTangent` | world-space tangent を `-1..1` から `0..1` に変換した色 |
| `WorldBitangent` | world-space bitangent を `-1..1` から `0..1` に変換した色 |
| `FrontFace` | 表面を緑、裏面をマゼンタで表示 |
| `LightDirection` | main light の方向 |
| `LightColor` | main light の HDR 色を簡易 tone mapping した色 |
| `LightAttenuation` | main light の距離減衰と realtime shadow attenuation |
| `ViewDirection` | surface から camera へ向かう方向 |
| `ViewFacing` | normal と view direction の内積。正面で白、接線方向で黒 |

`Coordinate Scale` は UV と position の繰り返し倍率です。position は負の値を含むため、そのまま色にせず小数部を表示します。

## Wireframe

`Wireframe` は mesh の実際の triangle edge を表示します。texture の格子や UV 境界ではありません。

このモードは geometry shader を使うため shader model 4.0 が必要です。Metal は geometry shader をサポートしないため、この shader の対象外です。また、VRChat の Android／Quest avatar は SDK 同梱 shader 以外を使用できません。本 shader は PC avatar と world の確認用です。

線幅は screen pixel 基準です。細い triangle や遠距離では複数の辺が重なって見える場合があります。

## 制約

- 表示値は pixel shader に補間された値です。頂点単位の離散値を確認する場合も triangle 内では補間されます
- `LightAttenuation` は ForwardBase の main light を対象とします。追加 light を個別には表示しません
- `WorldPosition` と `ObjectPosition` は絶対値ではなく周期色です。数値の測定には RenderDoc 等を使用してください
- Debug shader は module phase を公開しません。module によって診断結果が変更されることを防ぐためです

---

[目次に戻る](../README.md#ドキュメント)
