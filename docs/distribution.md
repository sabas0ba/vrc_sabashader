# 配布のしくみとリリース手順

VCC は「リスティング（`index.json`）を 1 本購読し、その中に書かれた zip を取りに行く」
という仕組みで動きます。このリポジトリはその一式を GitHub だけで完結させています。

```
git tag 0.1.0
        │
        ▼
build-release.yml
  Packages/io.github.sabas0ba.sabashader を zip 化
  GitHub Release に zip と package.json を添付
        │
        ▼ (release published)
build-listing.yml
  全リリースを走査して index.json を生成
  GitHub Pages に配信
        │
        ▼
VCC が https://sabas0ba.github.io/vrc_sabashader/index.json を読む
```

## 初回だけ必要な設定

1. リポジトリの `Settings` > `Pages` で **Source** を `GitHub Actions` にする
2. `listing.json` の `repository` / `url` / `author` が自分のものになっているか確認する

## リリース手順

1. `Packages/io.github.sabas0ba.sabashader/package.json` の `version` を上げる
2. `Packages/io.github.sabas0ba.sabashader/CHANGELOG.md` に項目を足す
3. ファイルを追加していたら `python tools/gen_meta.py` を実行して `.meta` をコミットする
4. `python -m pytest tests -q` が緑になっていることを確認する
5. `version` と同じ名前でタグを打って push する

```bash
git tag 0.1.0
git push origin 0.1.0
```

タグ名と `package.json` の `version` が食い違っていると
`build-release.yml` が明示的に失敗します。

`workflow_dispatch` から手で流すこともできます（その場合はタグが自動で作られます）。

## リスティングの中身

`tools/build_listing.py` が GitHub Releases API を走査して組み立てます。

- 各リリースの `package.json` アセットを読み、`listing.json` の `packages` に
  載っているパッケージだけを採用する
- zip アセットの URL を `url` に、その SHA-256 を `zipSHA256` に入れる
- ドラフトのリリースは無視する
- バージョンは新しい順に並べる（プレリリースは同じ数値より前）

ネットワーク無しで動作確認する場合:

```bash
python tools/build_listing.py --releases path/to/releases.json --output /tmp/index.json --html /tmp/index.html
```

## `.meta` と GUID について

マテリアルはシェーダーを **GUID** で参照します。
利用者ごとに GUID が変わると、更新のたびにマテリアルがシェーダーを見失います。
そのため `.meta` は必ずリポジトリにコミットして配布します。

`tools/gen_meta.py` はパッケージ内の相対パスから `uuid5` で GUID を導出するので、
誰がいつ実行しても同じ値になります。既存の `.meta` は書き換えません。

```bash
python tools/gen_meta.py          # 足りないものを作る
python tools/gen_meta.py --check  # 足りないものがあれば終了コード 1
```

> ファイルを **リネーム** するときは `.meta` も一緒に `git mv` してください。
> GUID が保たれるので参照が切れません。逆に `.meta` を消して作り直すと
> GUID が変わってしまうので、一度リリースしたファイルの `.meta` は消さないでください。
> GUID の重複は `tests/test_packaging.py` が検査します。

## 利用者側の手順

VCC には Shader Core のリスティングも追加してもらう必要があります。
VPM は依存パッケージを別リスティングから自動で引いてこないためです。

```
https://sabas0ba.github.io/vrc_sabashader/index.json
https://lilxyzw.github.io/vpm-repos/vpm.json
```

Pages に配信される案内ページ（`index.html`）には
`vcc://vpm/addRepo?url=...` のワンクリック追加リンクを置いてあります。
