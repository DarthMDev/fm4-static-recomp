# Pinned macOS ShiftGlue patches

`config/macos-toolchain.json` pins the macOS SDK at commit
`2460deb30668b49e272497929b07a2f2244c88f7` and records the SHA-256 of every patch.
Windows continues to use its existing `config/release-toolchain.json` pin and
prebuilt SDK bundle.

`0001-apple-float-parser.patch` is the Apple float-parsing workaround from the
tested development checkout. `tools/mac_sdk.py` verifies the pin and patch hashes
and applies this stack automatically, including on resumed builds. Conflicting
source edits are preserved and rejected rather than reset. Managed SDK source
lives in `.local/shiftglue-sdk-macos/<revision>` below each source payload.

An explicit development SDK (`FM4_SDK_ROOT` or the launcher's `mac-sdk-path.txt`)
must have the pinned revision and submodules too. Its identity and working diff
hash are recorded in `.local/logs/macos-sdk.json`. A clean managed checkout is the
release validation path; a development override is not evidence of a clean build.

`patches/sdk/` and the uppercase Mac tools are inherited from the older hl360
port and target its separate ReXGlue/Metal backend. The current Avalonia/ShiftGlue
launcher uses `tools/setup-macos.py` and `tools/package-launcher-macos.py`.
