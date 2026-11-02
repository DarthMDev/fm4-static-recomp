# macOS validation

## User gameplay report — November 2, 2026

Apple M3, Vulkan/MoltenVK. Default graphics settings, with the FPS limit changed
to 60 FPS. The user tested Free Play using the first default car on the first
default track, and also tested menus. Both ran at a reported stable 60 FPS.
No graphics issues were observed after the fixes for stretched triangles,
neighboring guest-page coherency and duplicate presentation contention.

The car and track names, run duration, frame-time percentiles and display latency
were not captured. This report does not establish every track, camera, mode,
controller or older macOS release. It also does not independently qualify FSR 2,
FSR 3 or MetalFX: an explicit upscaler selection was not reported.

## Automated evidence and repeatable checks

The November 2 validation rebuilt the isolated source payload with the managed
SDK (`external=false`), completed code generation and full host/facade builds,
and passed FSR 2/3 dispatch/readback smoke checks with context resize. The tested
SDK working diff exactly matched a clean pinned checkout plus all four patches.
This resumed an existing managed build; a fresh end-to-end install of the final
public ZIP remains a separate check.

Automated results: 15 Python tooling tests; 349 launcher tests; 98 native cases
and 11,803 assertions passed, with one optional fixture case skipped; and the
real host-page regression passed. The native suite used the user’s verified
Disc 1 via `FM4_TEST_GAME_ROOT`; without that fixture, the disc-identity check
cannot pass.
The memory regression checks real neighboring 4 KB guest-page writes on a 16 KB
host page. The FPS sampler checks that UI redraws do not inflate guest FPS.
Use the commands in [the Mac guide](MACOS.md#checks-and-packaging) for current runs.

The source presentation default prevents repeat host UI redraws from holding
MoltenVK’s submission queue while waiting for another drawable. Clear folding
and skipping effect-free draws reduce submission work. macOS also disables
predicated tiling. Windows keeps its existing defaults. The detailed investigation
and captured-frame comparisons are retained in [performance notes](macos-performance.md).

## Release and MetalFX qualification still required

- Clean source-payload install with a managed SDK and no development override.
- Explicit FSR 2 and FSR 3 gameplay, including motion, camera cuts and resizing.
- Additional cars, tracks, replays, longer sessions and physical controller checks.
- Runtime testing on the declared macOS 14 minimum.
- Vulkan–Metal integration in the live renderer before MetalFX. The standalone
  synthetic texture/shared-event round trip passed at two resource sizes; see
  [interop details](MACOS_METAL.md#2-vulkanmetal-feasibility-gate).
- Measured frame pacing and latency before any 60 → 120 interpolation claim.

Package only the launcher and source; users build game-derived executables locally.
Keep each release’s source commit, SDK/patch identity, automated results and
manual test scope in its release notes. The build helper records automated build
identity and does not automatically certify user gameplay.
