# dotfiles のコンテナ構成を基準に、vrc_sabashader の開発シェルを実体化する。
# 参照元: https://github.com/sabas0ba/dotfiles/tree/fc4cdecc02a6a95c81a259549d3fb9e7df18bb8f
#
#   podman build -t vrc-sabashader-dev -f Containerfile .
#   podman run --rm -v "$PWD:/work:z" -w /work vrc-sabashader-dev
#
# flake.nix が dotfiles toolchain とプロジェクト固有依存の単一情報源である。
# ビルド時に dev shell を profile として実体化するため、実行時の依存取得は不要。

# dotfiles の Dockerfile と同じ Nix バージョンおよび digest に固定する。
ARG NIX_VERSION=2.35.1
ARG NIX_IMAGE_DIGEST=sha256:377d4887aca98f0dfa12971c1ea6d6a625a435d8b610d4c95a436843da6fbfd1
FROM docker.io/nixos/nix:${NIX_VERSION}@${NIX_IMAGE_DIGEST}

# Podman/Docker の seccomp と Nix sandbox の競合を避ける設定も dotfiles に合わせる。
RUN mkdir -p /etc/nix \
    && printf '%s\n' \
        'experimental-features = nix-command flakes' \
        'sandbox = false' \
        'filter-syscalls = false' \
        'max-jobs = auto' \
        'flake-registry = ' \
        >> /etc/nix/nix.conf

ENV SABASHADER_PROFILE=/nix/var/nix/profiles/vrc-sabashader-dev \
    PYTHONDONTWRITEBYTECODE=1

WORKDIR /work

# 環境定義だけを先に配置し、ソース変更時も Nix closure のレイヤを再利用する。
COPY flake.nix flake.lock ./
RUN nix develop --profile "$SABASHADER_PROFILE" --command true \
    && nix flake archive --json > /dev/null \
    && nix registry add nixpkgs \
       "path:$(nix eval --raw --impure --expr '(builtins.getFlake "/work").inputs.nixpkgs.outPath')" \
    && rm -rf /root/.cache/nix

COPY tools/container-entrypoint.sh /usr/local/bin/vrc-sabashader-entrypoint.sh
RUN chmod +x /usr/local/bin/vrc-sabashader-entrypoint.sh

ENTRYPOINT ["/bin/sh", "/usr/local/bin/vrc-sabashader-entrypoint.sh"]
CMD ["python", "-m", "pytest", "tests", "-q"]
