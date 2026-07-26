"""ゴールデン画像との比較。"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

import numpy as np


@dataclass(frozen=True)
class Diff:
    mean: float       # 平均絶対誤差 (0-255)
    max: float        # 最大絶対誤差 (0-255)
    changed: float    # 1 階調以上ずれたピクセルの割合 (0-1)

    def summary(self) -> str:
        return f"mean={self.mean:.3f} max={self.max:.0f} changed={self.changed * 100:.2f}%"


def save_png(path: Path, image: np.ndarray) -> None:
    from PIL import Image

    path.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(image, mode="RGBA").save(path)


def load_png(path: Path) -> np.ndarray:
    from PIL import Image

    with Image.open(path) as img:
        return np.array(img.convert("RGBA"))


def compare(actual: np.ndarray, expected: np.ndarray) -> Diff:
    if actual.shape != expected.shape:
        raise ValueError(f"画像サイズが違います: {actual.shape} != {expected.shape}")

    delta = np.abs(actual.astype(np.int16) - expected.astype(np.int16))
    per_pixel = delta.max(axis=2)
    return Diff(
        mean=float(delta.mean()),
        max=float(delta.max()),
        changed=float((per_pixel > 0).mean()),
    )


def diff_image(actual: np.ndarray, expected: np.ndarray) -> np.ndarray:
    """差分を強調した可視化画像。失敗時にアーティファクトとして保存する。"""
    delta = np.abs(actual.astype(np.int16) - expected.astype(np.int16))[:, :, :3]
    amplified = np.clip(delta * 16, 0, 255).astype(np.uint8)
    alpha = np.full(amplified.shape[:2] + (1,), 255, dtype=np.uint8)
    return np.concatenate([amplified, alpha], axis=2)
