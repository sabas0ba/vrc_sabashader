"""EGL(llvmpipe) 上でのオフスクリーン描画。

GPU もディスプレイも不要なので、ローカルでも GitHub Actions でも同じように動く。
"""

from __future__ import annotations

import os
from typing import Optional, Tuple

import numpy as np

_context = None


class ShaderCompileError(RuntimeError):
    """GLSL のコンパイル/リンクに失敗した。行番号付きのソースを添えて投げる。"""

    def __init__(self, message: str, source: str) -> None:
        numbered = "\n".join(f"{i + 1:4d} | {line}" for i, line in enumerate(source.splitlines()))
        super().__init__(f"{message}\n\n--- generated GLSL ---\n{numbered}")
        self.source = source


def get_context():
    """スタンドアロンな OpenGL コンテキストを 1 つだけ作って使い回す。"""
    global _context
    if _context is not None:
        return _context

    import moderngl

    os.environ.setdefault("LIBGL_ALWAYS_SOFTWARE", "1")

    errors = []
    for backend in ("egl", None):
        try:
            _context = (
                moderngl.create_context(standalone=True, backend=backend)
                if backend
                else moderngl.create_context(standalone=True)
            )
            return _context
        except Exception as exc:  # pragma: no cover - 環境依存
            errors.append(f"{backend or 'default'}: {exc}")

    raise RuntimeError(
        "ヘッドレスな OpenGL コンテキストを作れませんでした。"
        "libegl1 / libgl1-mesa-dri がインストールされているか確認してください。\n"
        + "\n".join(errors)
    )


def renderer_info() -> str:
    ctx = get_context()
    return f"{ctx.info['GL_RENDERER']} / {ctx.info['GL_VERSION']}"


def render_fragment(
    fragment_source: str,
    resolution: Tuple[int, int],
    vertex_source: Optional[str] = None,
) -> np.ndarray:
    """フルスクリーン三角形を 1 枚描いて RGBA8 の配列 (H, W, 4) を返す。"""
    import moderngl

    from .glsl import VERTEX_SHADER

    ctx = get_context()
    width, height = resolution

    try:
        program = ctx.program(
            vertex_shader=vertex_source or VERTEX_SHADER,
            fragment_shader=fragment_source,
        )
    except Exception as exc:
        raise ShaderCompileError(str(exc), fragment_source) from exc

    color = ctx.texture((width, height), 4, dtype="f1")
    fbo = ctx.framebuffer(color_attachments=[color])
    try:
        fbo.use()
        ctx.disable(moderngl.DEPTH_TEST | moderngl.BLEND)
        ctx.clear(0.0, 0.0, 0.0, 1.0)

        vao = ctx.vertex_array(program, [])
        vao.render(mode=moderngl.TRIANGLES, vertices=3)

        raw = fbo.read(components=4, dtype="f1")
    finally:
        fbo.release()
        color.release()
        program.release()

    image = np.frombuffer(raw, dtype=np.uint8).reshape(height, width, 4)
    # OpenGL は左下原点なので、画像として自然な向きに直す。
    return np.flipud(image).copy()
