# 表示パネル（Display Panel）

本体または Video Input の最終色へ、画面ピクセル座標で LCD／LED の画素構造を
重ねます。`LED Wall` は LED の発光点に加えて、パネルの継ぎ目とパネル単位の
わずかな輝度差を表現します。

![LCD の RGB ストライプと遮光部](../tests/golden/display_lcd.png)
![LED の RGB 発光点](../tests/golden/display_led.png)
![LED 大画面のパネル継ぎ目と輝度差](../tests/golden/display_led_wall.png)

| プロパティ | 説明 |
| --- | --- |
| Amount | 全体の強さ。`0` で無効 |
| Mode | `LCD`、`LED`、`LED Wall` の画素構造 |
| Pixel Pitch | 1 画素の幅（画面ピクセル） |
| Fill | 画素内で発光部が占める割合 |
| Grid | 発光部の間を暗くする強さ |
| Subpixel | RGB サブピクセルを分離する強さ |
| Subpixel Order | RGB／BGR の並び |
| Brightness | パネル表示の明るさ。HDR 値を使用可能 |
| View Angle | 正面以外から見たときの減光量 |
| Tile Cells | `LED Wall` の1パネルに含む画素数 |
| Seam | `LED Wall` のパネル継ぎ目幅（画面ピクセル） |
| Tile Variation | `LED Wall` のパネル単位の輝度差 |

処理順は Video Input とドット絵風の**後**、ブラウン管・グリッチの**前**です。
入力映像または量子化済みの色へパネル構造を付け、必要なら最後に CRT の走査線や
乱れを重ねられます。`Amount` が `0` のときは画素構造の計算を行いません。

画素ピッチは UV ではなく画面ピクセル基準なので、表示解像度に対する見かけの大きさは
安定します。一方、遠距離や細いピッチではモアレが発生します。実際の液晶の偏光、
応答遅延、残像、ブルーム、表面反射は扱いません。非光沢／光沢の表面表現は、反射や
周辺画素のサンプリングを含むため別モジュールとして扱う予定です。
