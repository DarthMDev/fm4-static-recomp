# macOS Apple Silicon preview

This fork uses the current FM4 Static Recomp host and Avalonia launcher with
ShiftGlue’s Vulkan renderer over MoltenVK. The older hl360/ReXGlue native Metal
instructions do not apply. MetalFX is planned; it is not a selectable backend yet.

## Install the fork release

1. Download `FM4StaticRecomp-Launcher-macos-arm64.zip` and its `.sha256` file from
   [DarthMDev’s releases](https://github.com/DarthMDev/fm4-static-recomp/releases).
2. Verify it with `shasum -a 256 -c FM4StaticRecomp-Launcher-macos-arm64.zip.sha256`.
3. Extract the ZIP into a writable folder you own. Launch `FM4 Static Recomp.app`.
4. Follow setup with your own supported Disc 1; Disc 2 is optional. The launcher
   verifies the disc and builds the game on your Mac. No game executable or assets
   are distributed. See [supported discs](../README.md#supported-game).
5. Start with default graphics and a 60 FPS limit. First use compiles shaders;
   revisit the same race to compare warm-cache behavior.

Requires Apple Silicon, a macOS 14.0 deployment target, Xcode Command Line Tools,
Python 3.11+, CMake 3.25+ and Ninja. Install the tools with:

```sh
xcode-select --install
brew install python cmake ninja
```

An internet connection is needed to fetch SDK sources and build dependencies.
macOS 14 is the build target; runtime testing on that OS remains pending. The
reported gameplay test used Apple M3. The bundle is ad-hoc signed, not notarized;
Gatekeeper may require an explicit approval in System Settings → Privacy & Security.

Keep the adjacent `data` folder when updating: it holds your game, content,
profiles, settings, source and builds. Updates on macOS are manual. Back up your
profiles before replacing an installation. The launcher also offers Project
Forza Plus 4 through its managed installer, but that path has not been qualified
on macOS. Use vanilla FM4 for the baseline. DLSS requires supported NVIDIA hardware and is unavailable on Apple
Silicon. FSR 2 and FSR 3 are temporal upscaling choices; neither enables frame
generation here. See [testing status](#testing-status) below.

## Build from source

Use a native ARM64 terminal and your own extracted Disc 1 at `game/disc1`:

```sh
git clone https://github.com/DarthMDev/fm4-static-recomp.git
cd fm4-static-recomp
git checkout macos-support
./tools/build-mac.sh
./tools/run-mac.sh --fm4_frame_rate=60
```

`FM4_GAME_ROOT=/absolute/path/to/disc1 ./tools/build-mac.sh` changes the build’s
input folder. For launch, pass `--game_data_root=/absolute/path/to/disc1` to
`tools/run-mac.sh`. Keep saves and caches separate when comparing builds.

`config/macos-toolchain.json` pins ShiftGlue to
`2460deb30668b49e272497929b07a2f2244c88f7` and four SHA-256 checked patches.
The tested SDK revision matches that pin. Windows retains its independent SDK
pin in `config/release-toolchain.json`. The helper creates an isolated SDK under
`.local/shiftglue-sdk-macos/<revision>` and verifies its submodules. It builds
Release ARM64 binaries in `out/build/mac-arm64-release`, including the facade
libraries and FidelityFX runtime. Do not build only the host target: runtime
staging and facade targets are part of the complete build.

Remove any `FM4_SDK_ROOT` override and use a new source extraction for clean-install
validation. A development override must match the pinned revision; its additional
working changes are recorded, so it does not qualify as a clean managed SDK.
Build receipts are `.local/logs/macos-sdk.json` and `macos-build.json`.
The latter’s `gameplay_validated=false` means the build helper cannot certify
manual gameplay; the reported test scope is summarized below.

## Checks and packaging

```sh
python3 -m unittest discover -s tools/tests -v
dotnet test launcher/Fm4StaticRecomp.Launcher.Tests -c Release
python3 tools/test-macos-upscalers.py --build-dir out/build/mac-arm64-release
python3 tools/test-macos-metal-interop.py --build-dir out/build/mac-arm64-release
cmake -S . -B out/build/mac-arm64-release -DFM4_BUILD_TESTS=ON -DFM4_BUILD_MACOS_MEMORY_TESTS=ON
cmake --build out/build/mac-arm64-release --target fm4_unit_tests fm4_macos_page_watch_test --parallel 4
FM4_TEST_GAME_ROOT="$PWD/game/disc1" out/build/mac-arm64-release/fm4-tests/fm4_unit_tests
out/build/mac-arm64-release/fm4_macos_page_watch_test
python3 tools/package-launcher-macos.py --output-dir out/releases/macos-preview
```

Packaging also requires the .NET 8 SDK. Use a fresh output directory. Mac release
version and repository come from `config/macos-release.json`; the embedded payload
receives those values in `config/release.json` without changing Windows metadata.
Never use `--sdk-source` for a public package: it writes a local development hint.

Only allowlisted source, launcher runtime and notices enter the archive. No SDK
binaries, translated guest code, game data, saves or caches are included. The
packager signs and verifies the app, adds installation notes to the ZIP and writes
a SHA-256 sidecar. Publish the ZIP and sidecar together as an experimental fork
prerelease, with the test scope and limitations below.

## Testing status

On November 2, 2026, the user reported stable 60 FPS in menus and a Free Play race
on Apple M3, using the first default car and track, default graphics settings and
a 60 FPS limit. No graphics issues were observed after the geometry/coherency
fixes. The upscaler remained at its default selection.

The isolated source payload rebuilt successfully using the pinned managed SDK.
Automated checks passed: 15 Python tests, 349 launcher tests, 98 native cases
with 11,803 assertions, the real 16 KB page-coherency regression, and FSR 2/3
GPU dispatch and resize checks. One optional native fixture case was skipped.
The packaged launcher initialized successfully and its signature was verified.

A standalone Vulkan–Metal test also passed color, reconstructed-depth and motion
texture sharing with GPU synchronization in both directions, without texture
readbacks. This is a feasibility result; MetalFX temporal upscaling and frame
interpolation are not included in the release.

Explicit FSR 2/3 gameplay, more cars/tracks/modes, longer sessions, physical
controllers, runtime testing on macOS 14 and a fresh end-to-end installation of
the final release ZIP remain unqualified. The reported 60 FPS is not a measured
frame-pacing or input-latency result.
