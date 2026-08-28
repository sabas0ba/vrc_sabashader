# Debug Shader Demo

`DebugShaderDemo.unity` は `SabaShader/Debug` の全18モードを同時に表示します。

- シーンを開いた状態で Game View または Scene View を確認してください。
- 各球体の下にモード名と `_Mode` の値を表示します。
- `DebugShaderDemoObject` は頂点カラーと UV0～UV3 を持つ一時メッシュを生成します。
- 生成する Mesh と Material はシーンへ保存せず、サンプル外のアセットを追加しません。
- geometry shader を使用するため Metal では表示できません。

Inspector で `Mode`、`Coordinate Scale`、`Wire Width` を変更すると、その場で表示が更新されます。
