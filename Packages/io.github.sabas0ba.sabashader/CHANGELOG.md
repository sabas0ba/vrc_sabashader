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
- 日本語・英語のマテリアルエディタ表示（`lang/*.po`）
