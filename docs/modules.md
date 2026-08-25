# モジュールのパラメータ

パッケージに同梱しているモジュール（`.scmodule`）2 つの説明です。
モジュールはシェーダー本体が描いた結果の上に効果を足す仕組みで、
Illust2D 以外の `.scshader` にも後から乗せられます。

> このページの図は、すべて[描画回帰テスト](testing.md#1-描画回帰テストヘッドレス)の
> ゴールデン画像です。出荷する `*Core.hlsl` をそのまま描いたものなので、
> 数式を変えれば図も変わります。手で描いた説明図ではありません。

## 有効にする

Shader Core は**シェーダーごとに**有効なモジュールを持ちます。既定値は
「そのシェーダーと同じディレクトリにあるモジュール」なので、別ディレクトリに
置いた本パッケージのモジュールは、シェーダーのインスペクタに出る
モジュール一覧で明示的に有効化します。有効にすると、マテリアルの
インスペクタにモジュールの折りたたみが増えます。

どちらのモジュールも `Amount`（強さ）が `0` のときは何もしません。
初期値は `0` なので、有効にしただけでは見た目は変わりません。

## 表面の重ね掛け（Surface Overlay）

雨・汗・雪・汚れを 1 つのモジュールで賄います。
「面がどれだけ覆われているか」を被覆率として出し、
その値で色の置き換え・沈み・法線の寝かせ・厚みの押し出しをまとめて動かします。

![横軸が面の上向き度合い、縦の 8 段がベースカラー。上向きの面にだけ乗る設定](../tests/golden/overlay_snow.png)
![向きを問わず濡らし、素の色を沈ませる設定](../tests/golden/overlay_wet.png)

被覆率は次の 3 つの掛け合わせです。

- 面の向き（`Upward Bias` が 1 に近いほど上向きの面だけ）
- マスク（共有マスクテクスチャのチャンネル、または全面）
- 水滴と垂れの模様（`Droplets` を上げたとき）

### 基本

| プロパティ | 説明 |
| --- | --- |
| Amount | 全体の強さ。`0` で無効 |
| Overlay Texture | 重ねる模様。汚れや雪の粒に使う |
| Overlay Color | 重ねる色。アルファが色の置き換え量（雨や汗は `0`） |
| Mask Channel | 共有マスクのどのチャンネルを使うか。None で全面 |
| Upward Bias | 上向き面への寄り。雪や埃は高め、汚れは低め |
| Border / Border Blur | 覆われたと見なす境界とそのぼかし |
| Pattern Scale | 粒と筋の全体の細かさ。オブジェクトの大きさに合わせる |

### 濡れと積もり

| プロパティ | 説明 |
| --- | --- |
| Wet Darkening | 覆われた部分の素の色を暗く濃くする。雨・汗向け |
| Settled Roundness | 覆われた部分の法線を上向きに寝かせる。雪向け |
| Settled Thickness | 頂点を押し出して厚みを出す。単位はメートル |
| Thickness Direction | `1` で真上、`0` で面の法線に沿って押し出す |
| Vertex Color Mask | 厚みを抑える頂点カラーのチャンネル |

厚みは頂点変位なので、**積もりの縁は丸められません**。
丸めるにはジオメトリが要りますが、モジュールはパスもテッセレーションも
足せません。縁をなだらかにしたい場合は頂点カラーで厚みを落とすか、
モデル側に縁を用意してください。

### 水滴と垂れ

`Droplets` を上げると、面に付く水の粒が被覆率に乗ります。
粒は `Run-off` で「その場に留まるもの」と「流れ出すもの」に分かれ、
流れた粒は `Streaks` で跡を残します。垂れる向きは重力方向です。

![付着した粒だけの状態。大きさにばらつきがある](../tests/golden/overlay_droplet.png)
![半分の列が流れ出した状態。止まる粒と流れる粒が混ざる](../tests/golden/overlay_runoff.png)

| プロパティ | 説明 |
| --- | --- |
| Droplets | 粒の量。濡れて見えるかはこれが効く |
| Droplet Density | 粒を置く格子の細かさ。下げると大きくまばらになる |
| Droplet Size | 格子に対する粒の大きさ。`1` に近づけると粒同士がくっつく |
| Size Variance | 粒の大きさのばらつき |
| Droplet Bump | 粒で法線を歪める量。`0` だと本体のハイライトが乗らず濡れて見えない |
| Run-off | `0` で全部その場に留まり、`1` で全部が流れ出す |
| Streaks / Streak Speed | 流れた跡の残り方と速さ |

### 作例

| 表現 | 効かせるもの |
| --- | --- |
| 雨 | Droplets + Run-off + Streaks + Wet Darkening、Overlay Color のアルファは `0` |
| 汗 | Droplets を弱め、Run-off を低く。Upward Bias は低め |
| 雪 | Upward Bias を高く、Settled Roundness と Settled Thickness を上げる |
| 汚れ | Overlay Texture と Overlay Color のアルファを上げ、Droplets は `0` |

## ドット絵風（Pixel Art）

色数を落とし、整列ディザで段差を散らし、必要ならパレットに寄せます。
升目は画面ピクセル単位で、ベースカラー・UV・塗り分けの入力を
升目の中心の値へ揃えることで、模様と帯の境界を升目に乗せます。

![色数を 4 段に落としただけの状態](../tests/golden/pixel_levels.png)
![同じ色数に整列ディザをかけた状態](../tests/golden/pixel_dither.png)

**画面そのものを間引くことはできません。** 隣接ピクセルを読むには GrabPass が
要り、このリポジトリでは使わない方針です。升目はあくまでディザと値の
スナップの粒度です。

| プロパティ | 説明 |
| --- | --- |
| Amount | 強さ。`0` で無効、`1` で完全に置き換える |
| Color Levels | 1 チャンネルあたりの段数。`2` で白黒、`4` で 64 色相当 |
| Cell Size | 升目の大きさ（画面ピクセル） |
| Dither | 段差を整列ディザで散らす量 |
| Palette Preset | 組み込みパレット。`Texture` のときだけ下のテクスチャを引く |
| Palette | 横方向のグラデーション画像。明るさで引いて色を置き換える |
| Palette Blend | パレットへの寄せ具合 |

### パレット

明るさでパレットを引いて色を置き換えます。
組み込みのプリセットは LCD / Retro / Mono / Sepia / Gray / OneBit / 8bit / Neon / Sunset の 9 種で、
`Texture` を選んだときだけ `Palette` のテクスチャを使います。

![明るさでパレットに寄せた状態](../tests/golden/pixel_palette.png)
![組み込みパレットの単色 LCD](../tests/golden/pixel_preset_lcd.png)
![組み込みパレットの 8bit。色そのものを段に落とすので色相が残る](../tests/golden/pixel_preset_8bit.png)

## 実装ファイル

| ファイル | 中身 |
| --- | --- |
| `Modules/SurfaceOverlay/SurfaceOverlayCore.hlsl` | 被覆率・水滴・垂れの数式（Unity 非依存・テスト対象） |
| `Modules/SurfaceOverlay/phase_morph.hlsl` | 厚みの押し出し（頂点） |
| `Modules/SurfaceOverlay/phase_base.hlsl` | 色の置き換え・沈み・法線（ピクセル） |
| `Modules/PixelArt/PixelArtCore.hlsl` | 量子化・ディザ・パレットの数式（Unity 非依存・テスト対象） |
| `Modules/PixelArt/phase_base.hlsl` | ベースカラーと UV のスナップ |
| `Modules/PixelArt/phase_modifylight.hlsl` | 塗り分けの入力のスナップ |
| `Modules/PixelArt/phase_postpixel.hlsl` | 色数落としとパレット |

数式が `*Core.hlsl` に切り出してあるのは、ヘッドレスで描画テストできる
ようにするためです（[テストの仕組み](testing.md)）。
モジュールを自分で足す手順は[モジュールを追加する](adding-a-module.md)にあります。

## VRChat での制約

VRChat は Android / Quest のアバターに SDK 付属シェーダーしか許可しません。
このパッケージのシェーダーとモジュールは **PC アバターとワールド専用**です。
