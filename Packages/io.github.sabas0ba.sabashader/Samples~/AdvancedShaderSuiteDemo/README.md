# Advanced Shader Suite Demo

Decal、Surface Detail、Spatial Interior、Transition の代表設定を1シーンで確認するサンプルです。
Spatial InteriorはUniverse Rift、Starfield、Cyber Back Face、Mudの4presetを含みます。

## 導入

1. Package Manager で SabaShader の `Samples` から **Advanced Shader Suite Demo** を Import します。
2. Shader Core の Project Settings で `SabaShader/Illust2D` に次のモジュールを追加します。
   - Decal
   - Surface Detail
   - Spatial Interior
   - Transition
3. `AdvancedShaderSuiteDemo.unity` を開きます。

モジュール構成を変更した後は、各オブジェクトの `Advanced Shader Demo Object` で `Apply` を実行するか、コンポーネントを有効化し直してください。

`Upward Dissolve`、`Glitch Spawn`、`Liquid to Solid` は `Progress` を `0` から `1` へ変更して確認できます。Animation Controller からは、マテリアルプロパティ名 `_io_github_sabas0ba_transition_Progress` を制御します。

`Spatial Back` は裏面の効果を外側から確認できるよう、実行時に複製したmeshの法線を内向きにしています。生成meshは保存されません。実際の髪内側や服裏側で表裏を同時表示する場合は `_Cull = Off` とし、法線方向も確認してください。
