# vrc_sabashader

[Shader Core](https://github.com/lilxyzw/Shader-Core) をベースにした VRChat 向けシェーダー集と、
それを VCC（VRChat Creator Companion）で配布するためのリポジトリです。

第一弾として、3D モデルを 2D イラスト調に見せるトゥーンシェーダー **Illust2D** を収録しています。

---

## VCC で導入する

1. VCC の `Settings` > `Packages` > `Add Repository` に以下を追加します。

   ```
   https://sabas0ba.github.io/vrc_sabashader/index.json
   ```

   > リリースを 1 度も作っていない間は上記 URL がまだ存在しません。
   > 手元で試す場合は `Packages/jp.sabas0ba.sabashader` をプロジェクトの
   > `Packages/` にコピーしても動きます。

2. あわせて Shader Core のリスティングも追加します（依存パッケージのため）。

   ```
   https://lilxyzw.github.io/vpm-repos/vpm.json
   ```

3. プロジェクトの `Manage Project` から `SabaShader` を `+` で追加します。
   Shader Core は依存として一緒に入ります。

4. マテリアルのシェーダーに `SabaShader/Illust2D` を選びます。

## リポジトリの構成

| パス | 中身 |
| --- | --- |
| `Packages/jp.sabas0ba.sabashader/` | 配布する VPM パッケージ本体 |
| `Packages/.../Shaders/Illust2D/` | Illust2D シェーダー一式 |
| `tests/` | ヘッドレス描画による回帰テストと構造チェック |
| `tools/` | `.meta` 生成・VPM リスティング生成スクリプト |
| `listing.json` | 配信するリスティングのメタ情報 |
| `.github/workflows/` | テスト・リリース・Pages 配信 |

## ドキュメント

- [Illust2D のパラメータ](docs/shader-illust2d.md)
- [テストの仕組みと動かし方](docs/testing.md)
- [配布のしくみとリリース手順](docs/distribution.md)
- [シェーダーを追加する](docs/adding-a-shader.md)

## 開発

```bash
# 依存パッケージ
pip install -r tests/requirements.txt

# ヘッドレス OpenGL（Ubuntu の場合）
sudo apt-get install -y libegl1 libgles2 libgl1-mesa-dri libglvnd0

# 全テスト
python -m pytest tests -q

# 見た目を意図的に変えたとき（差分を必ず目視してからコミットする）
python -m pytest tests -k render --update-goldens
```

`.meta` は Unity 無しでも生成できます。ファイルを足したら忘れずに実行してください。

```bash
python tools/gen_meta.py
```

## ライセンス

MIT License（[LICENSE](LICENSE)）。

依存する [Shader Core](https://github.com/lilxyzw/Shader-Core) は本リポジトリには含まれず、
VCC 経由で別途導入されます。
