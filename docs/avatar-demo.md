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

> **ライセンスロゴの表示義務があります。**
> `.demo/` に置いて手元で見る分には再配布に当たりませんが、
> **レンダリング結果を公開する場合はロゴの表示が必要**です。
> 条項は展開先の PDF を参照してください。

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
