#!/bin/sh
# Containerfile が実体化した Nix dev profile 内でコマンドを実行する。
set -eu

profile="${SABASHADER_PROFILE:-/nix/var/nix/profiles/vrc-sabashader-dev}"

if [ ! -e "$profile" ]; then
    echo "開発 profile が見つかりません: $profile" >&2
    exit 1
fi

exec nix develop "$profile" --command "$@"
