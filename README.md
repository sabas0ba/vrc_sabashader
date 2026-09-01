# vrc_sabashader

**VCC（VRChat Creator Companion）にリポジトリ URL を 1 本足すだけで導入できる、
VRChat 向けシェーダー集です。**

[![tests](https://github.com/sabas0ba/vrc_sabashader/actions/workflows/tests.yml/badge.svg)](https://github.com/sabas0ba/vrc_sabashader/actions/workflows/tests.yml)
[![unity compile](https://github.com/sabas0ba/vrc_sabashader/actions/workflows/unity-compile.yml/badge.svg)](https://github.com/sabas0ba/vrc_sabashader/actions/workflows/unity-compile.yml)
[![build listing](https://github.com/sabas0ba/vrc_sabashader/actions/workflows/build-listing.yml/badge.svg)](https://github.com/sabas0ba/vrc_sabashader/actions/workflows/build-listing.yml)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](LICENSE)
[![Unity](https://img.shields.io/badge/Unity-2022.3-black.svg?logo=unity)](https://unity.com/)

収録しているのは、3D モデルを 2D イラスト調に見せるトゥーンシェーダー **Illust2D**、
mesh とライティング入力を可視化する **Debug shader** と、
その上に効果を足すモジュール（表面の重ね掛け・ドット絵風・ビデオ入力・
表示パネル・ブラウン管とグリッチ・Decal・Surface Detail・Spatial Interior・
Transition・衣装変身バンク）です。
[Shader Core](https://github.com/lilxyzw/Shader-Core) をベースにしています。

シェーディングの数式は
[ヘッドレス描画による回帰テスト](docs/testing.md)で、
HLSL が実際にコンパイルできるかは
[Unity 上での検証](docs/testing.md#5-unity-でのコンパイル検証)で守っています。
出荷されるコードとテストされるコードは同一のファイルです。

---

## VCC で導入する

1. VCC の `Settings` > `Packages` > `Add Repository` に以下を追加します。

   ```
   https://sabas0ba.github.io/vrc_sabashader/index.json
   ```

   > リリースを 1 度も作っていない間は、リスティングは配信されていても
   > パッケージが空です。手元で試す場合は `Packages/io.github.sabas0ba.sabashader`
   > をプロジェクトの `Packages/` にコピーしても動きます。

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
| `Packages/io.github.sabas0ba.sabashader/` | 配布する VPM パッケージ本体 |
| `Packages/.../Shaders/` | Illust2D と Debug shader |
| `tests/` | ヘッドレス描画による回帰テストと構造チェック |
| `.ci/UnityProject/` | Unity でのコンパイル検証用プロジェクトの雛形 |
| `tools/` | `.meta` 生成・VPM リスティング生成・Pages のサイト生成・Unity プロジェクト組み立て |
| `listing.json` | 配信するリスティングのメタ情報 |
| `Containerfile` / `flake.nix` | dotfiles を基準にした harness/tools 環境（CI もこれを使う） |
| `.github/workflows/` | テスト・リリース・Pages 配信 |

## ドキュメント

並びと表記は [GitHub Pages のサイト](https://sabas0ba.github.io/vrc_sabashader/)と揃えてあります。

Core Shader:

- [Core Shader一覧](docs/core-shaders.md)（Illust2DとDebugの用途・使い方・主要パラメータ）
- [Illust2D の全パラメータ](docs/shader-illust2d.md)
- [Debug shaderの全表示モード](docs/shader-debug.md)

Shader拡張:

- [Shader拡張一覧](docs/shader-extensions.md)（全10項目のレンダリング例・使い方・主要パラメータ）
- [基本拡張の全パラメータ](docs/modules.md)（Surface Overlay・Pixel Art・Video Input・Display Panel・CRT / Glitch）
- [高度拡張の全パラメータ](docs/modules-advanced.md)（Decal・Surface Detail・Spatial Interior・Transition）
- [衣装変身バンク](docs/transformation-bank.md)（Clip Generator UI・全パラメータ・12 Style・NonToon・トラブル対応）

利用・開発:

- [アバターに適用して確認する](docs/avatar-demo.md)
- [テストの仕組みと動かし方](docs/testing.md)
- [Core Shaderを追加する](docs/adding-a-shader.md)
- [Shader拡張を追加する](docs/adding-a-module.md)
- [配布のしくみとリリース手順](docs/distribution.md)

パラメータの説明には図が付いています。shaderの見た目は描画回帰テストのキャプチャ、UIと作業範囲は
実装に対応する静的図として、いずれも `tests/golden/` で参照切れを検査します。

## 開発

harness と tools は**コンテナか nix の中で動かします**。ホスト OS に
Python やヘッドレス OpenGL を入れる必要はありません。環境差が
ゴールデン画像の比較に出るため、実行環境を固定しています。
`flake.nix` は `sabas0ba/dotfiles` の固定リビジョンを基準 toolchain とし、
プロジェクト固有の Python と Mesa を追加しています。`Containerfile` は同じ
dev shell を Nix profile として実体化します。

```bash
# コンテナ（podman / docker）。Containerfile が基準環境で、CI も同じイメージを使う
tools/dev.sh                                       # 全テスト
tools/dev.sh python -m pytest tests -k render      # 描画だけ
tools/dev.sh python tools/expand_shader.py --output /tmp/Illust2D.shader

# nix
nix develop --command python -m pytest tests -q
```

見た目を意図的に変えたときは、CI と同じコンテナでゴールデンを更新し、
**差分を必ず目視してから**コミットしてください。

```bash
tools/dev.sh python -m pytest tests -k render -q --update-goldens
```

HLSL が実際にコンパイルできるかは Unity でしか確認できません。
`.github/workflows/unity-compile.yml` がその検証をしますが、
Unity のライセンス secret が設定されるまではスキップされます。
設定方法とローカルでの回し方は [docs/testing.md](docs/testing.md#5-unity-でのコンパイル検証) を参照してください。

`.meta` は Unity 無しでも生成できます。ファイルを足したら忘れずに実行してください。

```bash
tools/dev.sh python tools/gen_meta.py
```

## ライセンス

Apache License 2.0（[LICENSE](LICENSE)、[NOTICE](NOTICE)）。

依存する [Shader Core](https://github.com/lilxyzw/Shader-Core) は本リポジトリには含まれず、
VCC 経由で別途導入されます。

### デモで使用する第三者素材

[アバターデモ](docs/avatar-demo.md)では、Unity Technologies Japan が公開している
ユニティちゃんを使用します。ユニティちゃんライセンス条項（UCL）が適用されます。

モデルデータ本体は本リポジトリには含まれず、`.demo/`（`.gitignore` 済み）へ
取得されるため、アセットデータの再配布は行いません。

**このデモのレンダリング結果を本リポジトリに掲載する場合は、画像の近くに
次の表記を置いてください。**UCL は「UCL ロゴ、もしくはライセンス表記のいずれか」を
求めており、ロゴ画像は必須ではありません。

```
この作品はユニティちゃんライセンス条項の元に提供されています
© Unity Technologies Japan/UCL
```

条項の要旨と運用上の切り分けは
[アバターデモ / ライセンス表記](docs/avatar-demo.md#ライセンス表記) にまとめています。
