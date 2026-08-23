# テストの仕組み

シェーダーの見た目と構造を CI で守るための仕組みです。5 層に分かれています。
上の 4 層は Unity 無しで動き、最後の層だけ Unity ライセンスを要求します。

| 層 | 対象 | 実体 | Unity |
| --- | --- | --- | --- |
| 描画回帰テスト | シェーディングの数式 | `tests/test_core_render.py` | 不要 |
| 構造チェック | `.scshader` の展開結果 | `tests/test_scshader_structure.py` | 不要 |
| 配布チェック | `package.json` / `.meta` / リスティング | `tests/test_packaging.py` | 不要 |
| ライセンスチェック | 第三者素材の表記と非追跡 | `tests/test_licensing.py` | 不要 |
| コンパイル検証 | HLSL が実際に通るか | `.ci/UnityProject` | **必要** |

---

## 1. 描画回帰テスト（ヘッドレス）

### 何をしているか

`Illust2DCore.hlsl` は **HLSL としても GLSL 3.30 core としてもコンパイルできる**
部分集合で書かれています。テストはこのファイルを一切書き換えずに
`tests/harness/prelude.glsl`（互換マクロ）と `tests/harness/scene.frag`（テストシーン）
の間に挟んでコンパイルし、EGL + llvmpipe のオフスクリーンバッファに描画して、
`tests/golden/*.png` と比較します。

つまり **テストされている数式と Unity に出荷される数式は同一のコード** です。
「テスト用に書き直したコピー」が本体とズレる、という事故が起きません。

GPU もディスプレイも不要なので、ローカルでも GitHub Actions でも同じように動きます。

### ケース

`tests/cases.py` に定義しています。7 種類のシーンモードがあります。

| モード | 描くもの | 効くもの |
| --- | --- | --- |
| `sphere` | 落ち影つきのライティングされた球 | 合成全体 |
| `box` | 3/4 ビューの立方体 | 平らな面と鋭い稜線での塗り分け |
| `torus` | 3/4 ビューのトーラス | 自己遮蔽と連続的に変わる曲率 |
| `capsule` | 3/4 ビューのカプセル | 押し出した曲面と半球の継ぎ目 |
| `ramp` | 横軸=光の当たり具合 / 縦軸=ベースカラー | ランプ、影の色シフト |
| `outline` | 横軸=ベースカラーの反映量 | アウトライン色 |
| `light_limit` | 横軸=入射光の明るさ | 明るさクランプ |

球は解析的に解けますが、平らな面・鋭い稜線・自己遮蔽は球では出てきません。
`box` / `torus` / `capsule` は距離関数のレイマーチで描いています。
立方体は面内で `dot(N, L)` が一定なので、ぼかしを変えても絵が動きません。
`box_band_per_face` は見える 3 面がそれぞれ別の帯に入る境界値を選んであり、
境界の位置がずれると面の色が入れ替わって分かります。
カメラは球モードと同じく -Z から +Z を向く平行投影で、`V = (0, 0, -1)` の前提を崩しません。
シルエットの 1 ピクセルが誤差幅を越えて行き来しないよう、2x2 で平均しています。

### 動かす

```bash
python -m pytest tests -q                          # 比較
python -m pytest tests -k render --update-goldens  # ゴールデンを更新
```

ゴールデン画像は **llvmpipe で作る必要があります**。手元の GPU で作ると
ドライバ差で CI と一致しません。CI と同じ環境で作るには、
`tests` ワークフローを `update_goldens` を有効にして手動実行し、
`goldens` アーティファクトを落として `tests/golden/` に置いてください。

```bash
gh workflow run tests.yml --ref <branch> -f update_goldens=true
```

見た目を意図的に変えたときは `--update-goldens` で更新し、
**差分画像を目で見てから**コミットしてください。
失敗時は `_test_artifacts/` に `*.actual.png` / `*.expected.png` / `*.diff.png`
（差分を 16 倍に増幅したもの）が残り、CI ではアーティファクトとして落とせます。

### 許容誤差

llvmpipe のバージョン差で 1 階調程度はずれるため、
平均絶対誤差 1.5 / 最大絶対誤差 12（いずれも 0-255）まで許容しています。
環境変数 `SABASHADER_GOLDEN_MEAN_TOL` / `SABASHADER_GOLDEN_MAX_TOL` で変更できます。

CI は `ubuntu-24.04` に固定しており、Mesa のバージョンが揃うようにしています。
Mesa を跨いで差分が出るようになったら、ゴールデンを更新するのではなく
まず許容誤差の設定を疑ってください。

### コアを編集するときのルール

`Illust2DCore.hlsl` は両方の言語でコンパイルできる必要があるため、制約があります。

- ベクターは全成分を書く（`half3(0.0, 0.0, 0.0)`、`half3 a = 0.0;` は不可）
- 行列・テクスチャ・グローバル変数・`static` は使わない
- 使ってよい組み込みは `prelude.glsl` にあるものだけ

Unity 固有のものが必要な処理は `Illust2DLighting.hlsl` や
`Illust2DFragment.hlsl` 側に置いてください（そちらは描画テストの対象外です）。

---

## 2. 構造チェック

`tests/harness/scshader.py` が Shader Core の `SCShaderImporter` を最低限
エミュレートし、`.scshader` を最終的な ShaderLab まで展開します。
Shader Core 本体はテスト時に `.cache/Shader-Core` へ shallow clone します
（コミットをピン留め済み。取得できない環境ではこの層だけ skip されます）。

検出できるもの:

- `__SC_PHASE_*__` などマーカーの取りこぼし
- 解決できない `#include`
- 必須フェーズの実装漏れ
- `SCCustomData` / `SCVertexMorph` / `SCVertexPost` / `SCPixelClip` などフックの欠落
- 括弧の対応漏れ
- **宣言していないプロパティの参照**（`_ShadeBorder1` のタイプミスなど）
- 使われていないプロパティ
- `SCShadingData` の初期化漏れ（テクスチャを含む構造体なので `(SCShadingData)0` が使えない）
- `lang/*.po` の翻訳漏れ・不要キー
- `tests/cases.py` の初期値とマテリアル初期値のズレ

### できないこと

この層では HLSL の実コンパイルはしていません。それは最後の層の担当です。

`.scshader` が Unity から実際にどう見えているかを確認したいときは、
展開結果をファイルに書き出せます。

```bash
python tools/expand_shader.py --output /tmp/Illust2D.shader
```

---

## 3. 配布チェック

`package.json` の必須項目・semver・Shader Core への依存宣言、
`.meta` の不足／孤児／GUID の一意性と決定性、
リスティング生成（バージョン順、ドラフト除外、zip URL）を検証します。

---

## 4. ライセンスチェック

アバターデモが使う第三者素材（ユニティちゃん）のライセンス表記が
ドキュメントから消えていないか、アバターの実体が追跡対象に入っていないかを見ます。

- README とデモのドキュメントに UCL のライセンス表記が残っているか
- `.gitignore` に `.demo/` があるか
- `.fbx` / `.blend` / `.vrm` / `.unitypackage` がリポジトリに紛れ込んでいないか

UCL は二次創作物の公開・頒布時に表記を求めており、アセットデータを再配布する場合は
さらにライセンス関連ファイル一式の同梱が必要になります。後者を踏まないよう、
アバターの実体は `.demo/` から出しません。詳細は
[アバターデモ / ライセンス表記](avatar-demo.md#ライセンス表記) を参照してください。

---

## 5. Unity でのコンパイル検証

上の 4 層は「Unity 無しで分かること」しか見ていません。
**HLSL が本当にコンパイルできるか**はここでしか確認できません。

`.ci/UnityProject` に検証専用の Unity プロジェクトの雛形を置いてあります。
`tools/setup_unity_project.py` がそこへ本パッケージと Shader Core
（テストハーネスと同じコミットに固定）を埋め込みパッケージとして配置し、
`game-ci/unity-test-runner` が EditMode テストを走らせます。

検証内容（`.ci/UnityProject/Assets/Editor/`）:

- `.scshader` が `Shader` としてインポートできるか（Shader Core のインポータが動いているか）
- `ShaderUtil.GetShaderMessages` にエラーが 1 件も無いか（警告はログに出すだけ）
- 4 つのパス（FORWARD / OUTLINE / FORWARD_DELTA / SHADOW_CASTER）が存在するか
- マテリアルに主要プロパティが生成されるか（`_BaseTexture_ST` など）

C# 側に書いた期待値（パス名・必須プロパティ）が実際の `.scshader` とズレていないかは
Python 側の `tests/test_unity_project.py` が突き合わせます。
「C# の期待値が古いまま Unity ジョブが通ってしまう」抜けを塞ぐためです。

### CI で有効にする

`unity-compile.yml` は **Unity のライセンス secret が無い場合はスキップ**します
（PR は赤くなりません）。有効にするには次の secret を設定してください。

| secret | 用途 |
| --- | --- |
| `UNITY_LICENSE` | Personal ライセンスの `.ulf` の中身をそのまま貼る |
| `UNITY_EMAIL` / `UNITY_PASSWORD` | Unity アカウント |
| `UNITY_SERIAL` | Plus / Pro の場合はこちら（`UNITY_LICENSE` の代わり） |

`.ulf` の取得手順は
[GameCI のドキュメント](https://game.ci/docs/github/activation)
を参照してください。

`.ci/UnityProject/ProjectSettings/ProjectVersion.txt` の Unity バージョンが
`package.json` の `unity` と一致していることはテストで確認しています。

### 手元で動かす

Unity を持っている場合は同じ検証をローカルでも回せます。

```bash
python tools/setup_unity_project.py

# Test Runner の EditMode から実行するか、batchmode で:
<Unity>/Editor/Unity -batchmode -quit \
  -projectPath .ci/UnityProject \
  -executeMethod SabaShader.CI.ShaderCompileChecker.RunBatch \
  -logFile -
```

問題があれば終了コード 1 で落ち、どのファイルの何行目かがログに出ます。
