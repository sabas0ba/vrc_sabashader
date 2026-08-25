# Changelog

このプロジェクトは [Semantic Versioning](https://semver.org/lang/ja/) に従います。

## [0.1.0] - 未リリース

### Added

- 2D イラスト風トゥーンシェーダー `SabaShader/Illust2D`
  - 2 段の影ランプ（境界・ぼかし・ポスタライズ段数）
  - 影の色相／彩度／明度シフトによるイラスト調の影色
  - アニメ塗り風のハードなハイライト
  - リムライト（ライト方向への追従つき）
  - 反転ハル方式のアウトライン（色トレス、距離による太さ補正、頂点カラーマスク）
  - VRChat のワールド差を吸収する明るさクランプ（下限／上限／無彩色化／Unlit 化）
  - ForwardBase / ForwardAdd / ShadowCaster / Outline の 4 パス（ビルトインRP）
- 効果を足すモジュール（`.scmodule`）。どれも `Amount` の既定が `0` なので、
  有効にしただけでは見た目は変わりません
  - 表面の重ね掛け（`io.github.sabas0ba.surfaceoverlay`）
    雨・汗・雪・汚れ。被覆率・水滴・垂れ・積もりの厚み（頂点変位）
  - ドット絵風（`io.github.sabas0ba.pixelart`）
    明るさの段落とし、整列ディザ、組み込みパレット 9 種
  - ブラウン管・グリッチ（`io.github.sabas0ba.crtglitch`）
    走査線・シャドウマスク・ロールバー・ざらつき・周辺の落ち込み・
    帯のずれ・色ずれ・頂点の裂け
- 日本語・英語のマテリアルエディタ表示（`lang/*.po`）
