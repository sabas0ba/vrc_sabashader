# シェーダーを追加する

Illust2D と同じ構成でシェーダーを増やす手順です。

## 1. ファイルを置く

```
Packages/io.github.sabas0ba.sabashader/Shaders/<名前>/
  <名前>.scshader            ShaderLab 本体
  <名前>_properties.hlsl     プロパティ定義（ファイル名は必ずこの形）
  sc_common.hlsl             Shader Core が要求するフック
  <名前>Core.hlsl            数式（Unity 非依存・テスト対象）
  <名前>Fragment.hlsl        ピクセルシェーダー
  lang/ja-JP.po, en-US.po    マテリアルエディタの表示
```

> `_properties.hlsl` の名前は Shader Core のインポータが
> `{シェーダー名}_properties.hlsl` を探す仕様なので固定です
> （ドキュメントには `properties.hlsl` と書かれていますが、実装はこちらです）。

> `_properties.hlsl` に **コメントは書けません**。
> Shader Core 0.1.9 の `SCProperty.Parse` は空行以外で `SC_` から始まらない行を
> すべて例外にするため、`//` を 1 行でも置くとインポートが
> `Exception: Property error.` で失敗します。行末コメントも同様です。
> 説明はこのドキュメント側に書いてください。

## 2. Shader Core 側の約束

`sc_common.hlsl` には以下が必要です。BiRP のパスから include されます。

| 名前 | 役割 |
| --- | --- |
| `struct SCCustomData` | モジュール間で共有する追加データ（空構造体は不可） |
| `void SCVertexMorph(...)` | `__SC_PHASE_morph__` を含む |
| `void SCVertexPost(..., half3 L)` | `__SC_PHASE_postvertex__` を含む。頂点の最終調整 |
| `void SCVertexPost(...)` | 上の 4 引数版のラッパ |
| `void SCPixelClip(v2f, bool, float)` | ShadowCaster のアルファクリップ |

ライティングを Shader Core に任せる場合は、`birp_lighting.hlsl` を include する前に
`SCCalculateLight` と `SCCalculateEnvironmentLight` を定義しておきます。

パスの中の include 順はこの並びになります。

```hlsl
#include "Packages/jp.lilxyzw.shadercore/ShaderLibrary/birp_forward.hlsl"  // sc_common.hlsl を読む
#include "<名前>Lighting.hlsl"                                              // 上の 2 関数
#include "Packages/jp.lilxyzw.shadercore/ShaderLibrary/birp_lighting.hlsl"  // SCCalculateAllLights
#include "<名前>Fragment.hlsl"                                              // frag
```

`Properties` ブロックの末尾には
`[SCHide]_ShaderLabDummy("", Float) = 0` のようなダミーを残してください
（展開結果の末尾がアトリビュートになるとシェーダーエラーになります）。

## 3. HLSL ミニファイアの落とし穴

Shader Core は `HLSLPROGRAM` ブロックをミニファイします。
記号の周りの空白が削られるため、次の書き方は壊れます。

```hlsl
#define FOO (1)     // -> #define FOO(1) 関数形式マクロになってしまう
#define BAR -1      // -> #define BAR-1
```

オブジェクト形式のマクロに値を持たせるのは避け、定数は関数内の `const` にしてください。

## 4. コアを数式だけに切り出す

`<名前>Core.hlsl` は **HLSL と GLSL 3.30 core の両方でコンパイルできる**
部分集合で書きます。制約は [docs/testing.md](testing.md) を参照してください。
こうしておくと、そのファイルをそのままヘッドレス描画テストにかけられます。

Unity 依存の処理（テクスチャサンプリング、SH、行列変換）は
`Fragment` / `Lighting` 側に置きます。

## 5. テストを足す

1. `tests/cases.py` に `DEFAULT_STYLE` 相当と `Case` を足す
2. 必要なら `tests/harness/scene.frag` にシーンモードを足す
3. `tests/test_scshader_structure.py` の `OUR_SOURCES` に新しいファイルを足す
4. `python -m pytest tests -k render --update-goldens` でゴールデンを作り、目視する

`tests/test_scshader_structure.py` は今のところ Illust2D 固定です。
2 本目を足すときは `SCSHADER` をパラメータ化してください。

## 6. `.meta` とリリース

```bash
python tools/gen_meta.py
python -m pytest tests -q
```

あとは [docs/distribution.md](distribution.md) のリリース手順に従います。

## モジュール（`.scmodule`）について

Shader Core にはラメやデカールのような機能を差し込むモジュール機構があります。
`tests/harness/scshader.py` はモジュールの読み込みとフェーズへの差し込み、
プロパティ名の `uniqueID` による書き換えまで再現します。
書き方は [モジュールを追加する](adding-a-module.md) を参照してください。
