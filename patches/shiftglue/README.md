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

`0003-vulkan-vertex-residency.patch` removes the address/size shortcut from
vertex-buffer residency checks. A binding can retain its address and size after
CPU writes invalidate the underlying shared-memory pages; the per-frame binding
bit must lead to `RequestRange` again so those pages are re-uploaded. macOS also
keeps `clear_memory_page_state` enabled to refresh CPU-written pages each frame.
The address/size change alone did not resolve the reported stretched geometry;
`0004` addresses the host-page invalidation defect identified afterward.

`0004-macos-subpage-write-watch.patch` checks CPU-byte snapshots of 4 KB GPU
pages that were preserved while their 16 KB host page became writable. A later
CPU store within the same host page can otherwise bypass write-watch faults.
Changed pages invalidate texture watches and are uploaded again; unchanged GPU
results are retained. New GPU writes refresh the CPU baseline. The standalone
`fm4_macos_page_watch_test` exercises real host faults, subsequent neighboring
stores, bulk copies and texture-watch notifications without a GPU or game data.
The original implementation fails that regression; the patched implementation
passes. **Resolved in live Free Play testing on Apple Silicon:** the user confirmed
continued driving without further geometry corruption, texture corruption or
flickering. The runtime log confirms the hidden-write detection was exercised.
This validates the reported issue, rather than every track or game mode.
Content comparisons cannot identify
a store that writes the same bytes already present in the CPU mirror.
