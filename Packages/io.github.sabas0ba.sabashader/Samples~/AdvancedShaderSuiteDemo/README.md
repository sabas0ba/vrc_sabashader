# Advanced Shader Suite Demo

Decal、Surface Detail、Spatial Interior、Transition の代表設定を1シーンで確認するサンプルです。
Spatial InteriorはUniverse Rift、Starfield、Cyber Back Face、Mudの4presetを含みます。
Decalは同じ右向きの魚型エンブレムをシリンダー側面へ貼り、UV SpaceとProjectionを比較します。
`Textures/DecalDemoEmblem.png` は画像生成で作成した、このサンプル専用の架空の図案です。
SabaShaderの正式ロゴや既存組織の商標ではありません。

## 導入

1. Package Manager で SabaShader の `Samples` から **Advanced Shader Suite Demo** を Import します。
2. Shader Core の Project Settings で `SabaShader/Illust2D` に次のモジュールを追加します。
   - Decal
   - Surface Detail
   - Spatial Interior
   - Transition
3. `AdvancedShaderSuiteDemo.unity` を開きます。

`Advanced Shader Demo Object` はこのサンプルだけで使用する表示補助Componentです。
Inspectorにはサンプル専用であることを示す警告を表示し、通常の `Add Component` メニューからは除外しています。
アバターやワールドへコピーしないでください。

モジュール構成を変更した後は、各オブジェクトの `Advanced Shader Demo Object` で `Rebuild Demo Preview` を実行するか、コンポーネントを有効化し直してください。

`Upward Dissolve`、`Glitch Spawn`、`Liquid to Solid` は、Play Modeで `Auto Animate in Play Mode` により自動再生します。
手動確認する場合はチェックを外して `Progress` を操作してください。Progress変更時は生成済みMaterialを再利用します。
Animation Controllerからは、マテリアルプロパティ名 `_io_github_sabas0ba_transition_Progress` を制御します。

`Spatial Back` は裏面の効果を外側から確認できるよう、実行時に複製したmeshの法線を内向きにしています。生成meshは保存されません。実際の髪内側や服裏側で表裏を同時表示する場合は `_Cull = Off` とし、法線方向も確認してください。
