# Changelog

このプロジェクトは [Semantic Versioning](https://semver.org/lang/ja/) に従います。

## [Unreleased]

### Changed

- 開発コンテナを固定 dotfiles/Nix dev shell を実体化する構成へ変更
- Unity 検証プロジェクト生成時に旧版 sample を除去し、Podman bind mount 上の固定依存取得に対応
- 共通Effect envelopeを端点で値と勾配が0になるbell curveへ変更し、変身開始・完了時の急な収束を抑制
- Style固有のnoise・turbulence・頂点変位をRole別bell curveで減衰し、衣装表示区間の境界で生じる非連続を解消

### Added

- 衣装変身バンクモジュール（`io.github.sabas0ba.transformationbank`）
  - 旧衣装と新衣装を1本の`Progress`で制御
  - Arcane、Cyber、Astral、Gaia、Umbra、Flame、Shatter、Glitch、Melt、Cosmic Rift、Magical Sparkle、Mana Mistの12 style
  - 頂点変位と発光を一括調整する`Effect Intensity`
  - Illust2DとNonToon 0.1.3のUnityコンパイル検証
  - 既定タイミングでOutgoingとIncomingの表示率の和を維持
- 12 Style、Capsule／CylinderのRole形状、Progressへ同期する2系統の専用mesh Particleを可視化するUPM sample `Transformation Bank Demo`
  - MeltのOutgoingを液体状に波打たせ、液滴と水玉へ分離して消去
  - 火の粉、Triangle／Quad破片、pixel片、星片、液滴、霧のStyle別Particle silhouette
  - MeltのIncomingへOutgoingの融解を時間反転した液体復元を適用
  - Astral／GaiaのParticle停止、Umbraの霧粒化、Flameの火の粉主体への調整

## [0.5.0] - 2026-08-29

### Added

- Debug shader（`SabaShader/Debug`）
  - triangle wireframe
  - UV0–UV3、vertex color、world／object position
  - normal、tangent、bitangent、front face、view direction
  - main light の方向、色、attenuation

- 全表示モードを確認できる UPM sample `Debug Shader Demo`

- Decalモジュール（`io.github.sabas0ba.decal`）
  - UV0–UV3を使うUV Space配置
  - object-space projector、投影角度と境界のfade
  - Alpha、Multiply、Add合成とShared Mask

- Surface Detailモジュール（`io.github.sabas0ba.surfacedetail`）
  - Skinの毛穴とFabricの織りを手続き生成
  - micro normal、色、roughness、sheenの微細変化
  - detail textureとShared Mask

- Spatial Interiorモジュール（`io.github.sabas0ba.spatialinterior`）
  - Universe、Starfield、Cyber、Mudの4preset
  - parallax付きのstar field、nebula、3D格子、泥状noise
  - Front／Back／BothとUV上のRift領域
  - Shared Maskによる髪内側、服裏側などへの部分適用

- Transitionモジュール（`io.github.sabas0ba.transition`）
  - Upward Dissolve、Glitch Spawn、Liquid to Solid
  - Liquid to Solidの複合波による不規則な変形と水たまり初期状態
  - Animation Controllerから制御する共通の`Progress`
  - Forward、ShadowCaster、Outlineで一致するclip

- 4モジュールの代表設定を確認できるUPM sample `Advanced Shader Suite Demo`
  - シリンダー側面へ同じエンブレムを貼るUV Space／Projection比較
  - Transitionの自動再生と再生成を伴わない手動`Progress`確認
  - サンプル専用Componentを示すInspector警告

## [0.4.0] - 2026-08-27

### Added

- 表示パネルモジュール（`io.github.sabas0ba.displaypanel`）
  - LCD の RGB ストライプと遮光部
  - LED の RGB 発光点
  - LED 大画面のパネル継ぎ目とパネル単位の輝度差

### Changed

- モジュールの `Amount` が `0` のとき、テクスチャ参照や画素効果の計算を省略

## [0.3.0] - 未リリース

### Added

- ビデオ入力モジュール（`io.github.sabas0ba.videoinput`）
  - 動画プレイヤーやカメラ等の `RenderTexture` を UV0 で表示
  - Tiling / Offset、HDR Tint、明るさ、左右・上下反転
  - ドット絵風とブラウン管・グリッチの前段で Unlit 合成

### Removed

- ブラウン管・グリッチの `Screen Curvature`。入力映像を再サンプリングせずに
  モデル頂点を動かす近似は、モデルの頂点密度と画面内の位置によって不自然な
  変形になるため削除

## [0.2.0] - 2026-08-26

### Added

- 効果を足すモジュール（`.scmodule`）。シェーダー本体が描いた結果の上に
  効果を足す仕組みで、`Illust2D` 以外の `.scshader` にも後から乗せられます。
  どれも `Amount` の既定が `0` なので、有効にしただけでは見た目は変わりません
  - 表面の重ね掛け（`io.github.sabas0ba.surfaceoverlay`）
    雨・汗・雪・汚れ。面の被覆率、水滴と垂れ、積もりの厚み（頂点変位）
  - ドット絵風（`io.github.sabas0ba.pixelart`）
    明るさの段落とし、整列ディザ、組み込みパレット 9 種
  - ブラウン管・グリッチ（`io.github.sabas0ba.crtglitch`）
    走査線・シャドウマスク・ロールバー・周辺の落ち込み・色ずれ・画面の丸み、
    中間調へ寄せられるざらつき、砂嵐、横帯の乱れ、升の破綻、頂点の裂け
- モジュールの日本語・英語のマテリアルエディタ表示（`lang/*.po`）

### Changed

- 粒を作るのに使っていた擬似乱数を自作のものへ差し替えました。
  由来のはっきりしない実装を残さないためです
- モジュールのプロパティの折りたたみが二重に入っていたのを解き、
  内側を実際の区分けに使う形へ変えました

### 分かっている限界

- 画面そのものを撮り直す加工はできません。ドット絵風の升目化、
  ブラウン管の色ずれ・帯のずれ・升の破綻は、いずれも勾配からの
  1 次近似です。シルエットや模様の境目では効き方が変わります
- 積もりの縁は丸められません。頂点変位ではジオメトリが増えないためです
- 画面の丸みはモデルの頂点を動かして代用しています。粗いメッシュは曲がらず、
  他のオブジェクトとの位置関係が崩れ、影の形は曲がる前のまま残ります

詳しくは [docs/modules.md](https://github.com/sabas0ba/vrc_sabashader/blob/main/docs/modules.md)
を参照してください。

## [0.1.0] - 2026-08-23

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
