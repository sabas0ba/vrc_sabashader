{
  description = "vrc_sabashader の harness と tools を動かす環境";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-24.11";
  };

  outputs =
    { self, nixpkgs }:
    let
      # ヘッドレス OpenGL を使うので Linux のみ。
      # macOS は Containerfile 側を使う。
      systems = [
        "x86_64-linux"
        "aarch64-linux"
      ];
      forAllSystems = f: nixpkgs.lib.genAttrs systems (system: f nixpkgs.legacyPackages.${system});
    in
    {
      devShells = forAllSystems (pkgs: {
        default = pkgs.mkShell {
          packages = [
            (pkgs.python311.withPackages (
              ps: with ps; [
                moderngl
                numpy
                pillow
                pytest
              ]
            ))
            pkgs.git
            pkgs.zip
          ];

          # GPU の無い環境で llvmpipe を確実に使う。Containerfile と同じ設定。
          LIBGL_ALWAYS_SOFTWARE = "1";
          MESA_LOADER_DRIVER_OVERRIDE = "llvmpipe";

          # nixpkgs の Mesa は FHS のパスに置かれないので、ドライバと
          # EGL のベンダ定義の場所を明示する。DRI ドライバと
          # 50_mesa.json は out ではなく drivers output にある。
          shellHook = ''
            export LD_LIBRARY_PATH=${
              pkgs.lib.makeLibraryPath [
                pkgs.libglvnd
                pkgs.mesa
                pkgs.mesa.drivers
              ]
            }''${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}
            export LIBGL_DRIVERS_PATH=${pkgs.mesa.drivers}/lib/dri
            export __EGL_VENDOR_LIBRARY_FILENAMES=${pkgs.mesa.drivers}/share/glvnd/egl_vendor.d/50_mesa.json
          '';
        };
      });
    };
}
