{
  description = "vrc_sabashader の harness と tools を動かす環境";

  inputs = {
    # 開発環境の基準。リビジョンは明示的に固定し、更新時は Containerfile の
    # 参照元コメントと flake.lock も同じ変更に含める。
    dotfiles.url = "github:sabas0ba/dotfiles/fc4cdecc02a6a95c81a259549d3fb9e7df18bb8f";
    nixpkgs.follows = "dotfiles/nixpkgs";
  };

  outputs =
    {
      self,
      dotfiles,
      nixpkgs,
    }:
    let
      systems = [
        "x86_64-linux"
        "aarch64-linux"
      ];
      forAllSystems = f: nixpkgs.lib.genAttrs systems (system: f system nixpkgs.legacyPackages.${system});
    in
    {
      devShells = forAllSystems (
        system: pkgs: {
          default = pkgs.mkShellNoCC {
            packages = [
              # dotfiles の基本ツール群をそのまま含める。
              dotfiles.packages.${system}.default
              (pkgs.python3.withPackages (
                ps: with ps; [
                  moderngl
                  numpy
                  pillow
                  pytest
                ]
              ))
              pkgs.zip
            ];

            env = {
              DOTFILES_ENV = "nix-develop";
              LIBGL_ALWAYS_SOFTWARE = "1";
              MESA_LOADER_DRIVER_OVERRIDE = "llvmpipe";
              PYTHONDONTWRITEBYTECODE = "1";
            };

            # nixpkgs の Mesa は FHS パス外にあるため、llvmpipe と EGL の場所を示す。
            shellHook = ''
              export LD_LIBRARY_PATH=${
                pkgs.lib.makeLibraryPath [
                  pkgs.libglvnd
                  pkgs.mesa
                ]
              }''${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}
              export LIBGL_DRIVERS_PATH=${pkgs.mesa}/lib/dri
              export __EGL_VENDOR_LIBRARY_FILENAMES=${pkgs.mesa}/share/glvnd/egl_vendor.d/50_mesa.json
            '';
          };
        }
      );
    };
}
