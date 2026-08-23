# harness と tools を動かすための環境。
#
# ホスト OS に Python やヘッドレス OpenGL を入れずに済ませるためのもので、
# CI（ubuntu-24.04 ランナー）と同じ Ubuntu 24.04 のパッケージを使う。
# ゴールデン画像は Mesa のバージョンに依存するので、ここを基準環境とする。
#
#   podman build -t vrc-sabashader-dev -f Containerfile .
#   podman run --rm -v "$PWD:/work:z" -w /work vrc-sabashader-dev python -m pytest tests -q
#
# ベースは digest で固定する。タグは動くため。
FROM docker.io/library/ubuntu@sha256:1e0a86e57d247923571b75e0aaf48a1449cf8c543d51fb3e07a4a7d7bfa79316

# apt がインストール中に対話を求めないようにする
ARG DEBIAN_FRONTEND=noninteractive

# ヘッドレス OpenGL 一式。GitHub Actions のランナーイメージには
# 最初から入っているものがあるため tests.yml の apt は 4 つで足りているが、
# 素の Ubuntu では libGL.so 側（libgl1 / libglx-mesa0 / libegl-mesa0）も要る。
# ここで明示することで、CI が暗黙に頼っている前提を無くす。
# git は構造チェックが Shader Core を shallow clone するのに使う。
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates \
        git \
        libegl-mesa0 \
        libegl1 \
        libgl1 \
        libgl1-mesa-dri \
        libgles2 \
        libglvnd0 \
        libglx-mesa0 \
        python3 \
        python3-pip \
        python3-venv \
        zip \
    && rm -rf /var/lib/apt/lists/*

# Ubuntu 24.04 の Python は外部管理環境なので、venv を切って使う。
ENV VIRTUAL_ENV=/opt/venv
RUN python3 -m venv "$VIRTUAL_ENV"
ENV PATH="$VIRTUAL_ENV/bin:$PATH"

COPY tests/requirements.txt /tmp/requirements.txt
RUN pip install --no-cache-dir --upgrade pip \
    && pip install --no-cache-dir -r /tmp/requirements.txt \
    && rm /tmp/requirements.txt

# GPU の無い環境で llvmpipe を確実に使う。tests.yml と同じ設定。
ENV LIBGL_ALWAYS_SOFTWARE=1 \
    MESA_LOADER_DRIVER_OVERRIDE=llvmpipe \
    PYTHONDONTWRITEBYTECODE=1

WORKDIR /work

CMD ["python", "-m", "pytest", "tests", "-q"]
