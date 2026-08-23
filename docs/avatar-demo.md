# アバターに適用して確認する

`.ci/UnityProject` の確認シーンは球や立方体を並べるだけなので、
髪・肌・布・目といった実際の面が揃ったときの見え方は分かりません。
そこでアバターに Illust2D を適用し、元のマテリアルと並べて撮るデモを用意しています。

## リポジトリに入るもの / 入らないもの

第三者のアバターとその派生データはリポジトリに含めません。

| 追跡する | 追跡しない（`.demo/` 以下、`.gitignore` 済み） |
| --- | --- |
| `tools/setup_demo_project.py`（取得元 URL と commit SHA） | アバターの FBX・テクスチャ・ライセンス同梱物 |
| `tools/demo/Editor/AvatarDemoScene.cs` | 生成したマテリアル・シーン・レンダリング結果 |

`.demo/` は Git 管理外なので再配布は発生しません。

## 使うアバター

Unity Technologies Japan が公開しているユニティちゃんを、
公式リポジトリから commit を固定して sparse checkout します。

| 名前 | 取得元 | commit |
| --- | --- | --- |
| `UnityChanSD` | [UnityChanToonShaderVer2_Project](https://github.com/unity3d-jp/UnityChanToonShaderVer2_Project) | `add3a5af` |
| `UnityChanCRS` | [unitychan-crs](https://github.com/unity3d-jp/unitychan-crs) | `149acd48` |

`UnityChanSD` の subtree にはユニティちゃんライセンス（UCL 2.0）の条項全文と
ライセンスロゴが同梱されており、`Assets/Avatar/UnityChanSD/License/` に展開されます。

UCL の表示義務については [ライセンス表記](#ライセンス表記) を参照してください。

## ライセンス表記

出典は `.demo/` に展開される
`Assets/Avatar/UnityChanSD/License/UCL2.0/Japanese/` の PDF
（`02` 要約版と `03` ライセンス表示について、いずれも 2.00 バージョン）です。
以下は該当箇所の要旨で、正文は `01` の条項が優先します。

### 表示が必要になるとき

二次創作物を**公開または頒布するとき**です。手元で見るだけなら不要です。

> クリエイターの皆様が創った二次創作物を公開したり頒布する時には、以下の UCL ロゴ、
> もしくはライセンス表記のいずれかを、二次創作物の本体もしくはその説明書や奥付、
> パッケージ、もしくは公開するホームページ等に表示するようにしてください。

**ロゴと表記のどちらか一方で足ります。**ロゴ画像は必須ではありません。
このリポジトリではテキスト表記を使います。

### 表記の文面

```
この作品はユニティちゃんライセンス条項の元に提供されています
© Unity Technologies Japan/UCL
```

`© UTJ/UCL` でも構いません。

### このリポジトリでの運用

| 何をするか | UCL 上の扱い | 必要なこと |
| --- | --- | --- |
| `.demo/` でレンダリングして手元で見る | 公開・頒布に当たらない | なし |
| レンダリング結果を README や docs に貼る | 二次創作物の公開 | 上の表記を画像の近くに置く |
| FBX やテクスチャをリポジトリに入れる | アセットデータの再配布 | 表記に加えてライセンス関連ファイル一式の同梱が必要 |

3 行目は行いません。アバターの実体は `.demo/`（`.gitignore` 済み）から出しません。

> 弊社キャラクターのデジタルアセットデータを、git 等の共有 WEB サービスを使って
> 再配布する場合、ライセンスロゴもしくはライセンス表記の掲示に加えて、
> ライセンス関連ファイル一式を同梱して配布するようにしてください。

### 禁止されていること

要約版が挙げているもののうち、このデモに関係するのは次の点です。

- ユニティ・テクノロジーズ・ジャパンおよびキャラクターの価値や品位を毀損する使い方
- 他の人を不当に貶める、差別する、攻撃する目的での使用。特定の信条・宗教・政治的主張のための使用
- 別途許諾を受けずに、公式製品であると誤認されるような使い方

問い合わせ先は `unity-chan@unity3d.co.jp` です。

## 上流の事情への対応

取得時の都合で、上流の事情に合わせた処理を 2 つ入れています。

- `UnityChanCRS` の同梱シェーダー（`CandyRockStar/Shader`）は持ち込みません。
  Unity 2022.3 で通る保証が無く、比較対象でもないためです。
  参照を失ったマテリアルは Standard に置き換えられます。
- `UnityChanSD` の FBX に付く `def_mat` はテクスチャ未設定なので、
  ベースカラーのアトラス `utc_all2_light.png` を別途取得します。

## 動かす

```bash
python tools/setup_demo_project.py            # 取得と組み立て
python tools/setup_demo_project.py --only UnityChanSD   # 片方だけ
```

Unity で `.demo/UnityProject` を開き、メニューの
`Tools > SabaShader > アバターデモを生成` を実行すると
`Assets/Demo/AvatarDemo.unity` が出来ます。batchmode なら次のとおりです。

```bash
<Unity>/Editor/Unity -batchmode \
  -projectPath .demo/UnityProject \
  -executeMethod SabaShader.Demo.AvatarDemoScene.BuildBatch -logFile -

<Unity>/Editor/Unity -batchmode \
  -projectPath .demo/UnityProject \
  -executeMethod SabaShader.Demo.AvatarDemoScene.CaptureBatch \
  -captureOutput demo.png -logFile -
```

アバターごとに左が元のマテリアル、右が Illust2D です。
ライティングは `tests/cases.py` と同じ条件、カラースペースは VRChat と同じ Linear に固定します。

## 比較を公平にするための処理

差が Illust2D によるものだと言えるように、左右で同じベーステクスチャが乗る状態に揃えます。
左側に手を入れるのは次の 2 つの場合だけです。

- 参照シェーダーがプロジェクトに無く、そのままではピンクになる → Standard に置き換える
- テクスチャが 1 枚も設定されていない → 右側と同じベーステクスチャを設定する

テクスチャの引き継ぎは `Material.GetTexture` ではなくシリアライズされた値を直接読みます。
シェーダーが見つからないマテリアルでは `HasProperty` が常に false になり、
プロパティ経由では読み出せないためです。

## 別のアバターを使う

`.demo/UnityProject/Assets/Avatar/<名前>/` に置けば自動で拾います。
複数置けばすべて横に並びます。明示する場合は
`-avatarAsset "Assets/Avatar/A/a.fbx;Assets/Avatar/B/b.fbx"` を渡してください。

入手先ごとに利用規約が異なります。BOOTH の無料アバターは多くが
「改変可・再配布禁止」で、CC0 ではありません。`.demo/` に置く限り再配布は
発生しませんが、規約は個別に確認してください。
