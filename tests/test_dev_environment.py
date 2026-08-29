"""Nix/Podman 開発環境の固定と dotfiles 基準を検査する。"""

from __future__ import annotations

import json
import re
from pathlib import Path

from harness.paths import REPO_ROOT

CONTAINERFILE = REPO_ROOT / "Containerfile"
FLAKE = REPO_ROOT / "flake.nix"
FLAKE_LOCK = REPO_ROOT / "flake.lock"
ENTRYPOINT = REPO_ROOT / "tools" / "container-entrypoint.sh"


def test_dotfiles_revision_is_pinned_consistently():
    flake = FLAKE.read_text(encoding="utf-8")
    containerfile = CONTAINERFILE.read_text(encoding="utf-8")
    lock = json.loads(FLAKE_LOCK.read_text(encoding="utf-8"))
    revision = lock["nodes"]["dotfiles"]["locked"]["rev"]

    assert re.fullmatch(r"[0-9a-f]{40}", revision)
    assert f"github:sabas0ba/dotfiles/{revision}" in flake
    assert revision in containerfile
    assert lock["nodes"]["root"]["inputs"]["nixpkgs"] == ["dotfiles", "nixpkgs"]


def test_container_base_is_digest_pinned_like_dotfiles():
    containerfile = CONTAINERFILE.read_text(encoding="utf-8")

    assert "ARG NIX_VERSION=2.35.1" in containerfile
    assert re.search(r"ARG NIX_IMAGE_DIGEST=sha256:[0-9a-f]{64}", containerfile)
    assert "FROM docker.io/nixos/nix:${NIX_VERSION}@${NIX_IMAGE_DIGEST}" in containerfile


def test_container_uses_materialized_nix_profile():
    containerfile = CONTAINERFILE.read_text(encoding="utf-8")
    entrypoint = ENTRYPOINT.read_text(encoding="utf-8")

    assert 'nix develop --profile "$SABASHADER_PROFILE"' in containerfile
    assert 'nix develop "$profile" --command "$@"' in entrypoint
    assert "pip install" not in containerfile
