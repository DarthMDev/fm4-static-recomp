# macOS checks

The CMake harness in this directory is historical code from the hl360/ReXGlue
port and does not target the current ShiftGlue host. Do not use it to qualify
this fork. Current commands and requirements are in [the Mac guide](../../docs/MACOS.md#checks-and-packaging).

`fm4_macos_page_watch_test` checks real neighboring 4 KB guest-page writes on a
16 KB host page without game data or a GPU. Hosts with another page size skip it.
`fm4_upscaler_smoke` checks FSR 2/3 GPU dispatch and context resize; its synthetic
readback does not establish in-game image quality or frame generation.
