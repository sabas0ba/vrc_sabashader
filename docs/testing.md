# テストの仕組み

Unity を起動せずに、シェーダーの見た目と構造を CI で守るための仕組みです。
3 層に分かれています。

| 層 | 対象 | 実体 |
| --- | --- | --- |
| 描画回帰テスト | シェーディングの数式 | `tests/test_core_render.py` |
| 構造チェック | `.scshader` の展開結果 | `tests/test_scshader_structure.py` |
| 配布チェック | `package.json` / `.meta` / リスティング | `tests/test_packaging.py` |

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

`tests/cases.py` に定義しています。4 種類のシーンモードがあります。

| モード | 描くもの | 効くもの |
| --- | --- | --- |
| `sphere` | 落ち影つきのライティングされた球 | 合成全体 |
| `ramp` | 横軸=光の当たり具合 / 縦軸=ベースカラー | ランプ、影の色シフト |
| `outline` | 横軸=ベースカラーの反映量 | アウトライン色 |
| `light_limit` | 横軸=入射光の明るさ | 明るさクランプ |

### 動かす

```bash
python -m pytest tests -q                          # 比較
python -m pytest tests -k render --update-goldens  # ゴールデンを更新
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

HLSL の実コンパイルはしていません。Unity のシェーダーコンパイラを通す検証は
Unity ライセンスが要るため、このリポジトリには入れていません。
実機確認は Unity プロジェクトに入れて行ってください。

---

## 3. 配布チェック

`package.json` の必須項目・semver・Shader Core への依存宣言、
`.meta` の不足／孤児／GUID の一意性と決定性、
リスティング生成（バージョン順、ドラフト除外、zip URL）を検証します。
