#!/usr/bin/env bash
# harness と tools をコンテナの中で動かす。
#
#   tools/dev.sh                       # テスト一式
#   tools/dev.sh python -m pytest tests -k render
#   tools/dev.sh python tools/gen_meta.py --check
#
# コンテナランタイムは podman を優先し、無ければ docker を使う。
# CONTAINER_ENGINE で明示もできる。
#
# nix を使う場合はこのスクリプトは要らない。
#   nix develop --command python -m pytest tests -q
#
# Windows の Git Bash から使う場合は、コンテナ内のパスが Windows のパスへ
# 書き換えられるのを止める必要がある。
#   MSYS_NO_PATHCONV=1 tools/dev.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
IMAGE="${SABASHADER_IMAGE:-vrc-sabashader-dev}"

engine="${CONTAINER_ENGINE:-}"
if [ -z "$engine" ]; then
    if command -v podman > /dev/null 2>&1; then
        engine=podman
    elif command -v docker > /dev/null 2>&1; then
        engine=docker
    else
        echo "podman も docker も見つかりません。どちらかを入れるか nix develop を使ってください。" >&2
        exit 1
    fi
fi

# イメージが無ければ組み立てる。Containerfile を触ったら --build で作り直す。
if [ "${1:-}" = "--build" ]; then
    shift
    build=1
elif ! "$engine" image exists "$IMAGE" > /dev/null 2>&1 \
    && ! "$engine" image inspect "$IMAGE" > /dev/null 2>&1; then
    build=1
else
    build=0
fi

if [ "$build" = "1" ]; then
    echo "イメージを組み立てます: $IMAGE" >&2
    "$engine" build -t "$IMAGE" -f "$REPO_ROOT/Containerfile" "$REPO_ROOT"
fi

# SELinux 環境で共有できるよう :z を付ける。付かないランタイムでも無害。
# 引数が無いときはイメージの CMD（テスト一式）が動く。
exec "$engine" run --rm \
    -v "$REPO_ROOT:/work:z" \
    -w /work \
    "$IMAGE" \
    "$@"
