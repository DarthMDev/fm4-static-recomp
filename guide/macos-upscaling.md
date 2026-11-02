# macOS upscaling and frame generation

The current macOS game build uses ShiftGlue's Vulkan presenter through MoltenVK.
It does not include the older hl360 port's native Metal renderer. MoltenVK uses
Metal underneath, but that does not automatically enable Apple's MetalFX APIs.

| Feature | Current macOS build |
| --- | --- |
| FSR 1 spatial upscaling | Built-in presenter shaders |
| FSR 2 temporal upscaling | FidelityFX 2.3.3 runtime, built and staged from source |
| FSR 3 temporal upscaling | FidelityFX 3.1.4 runtime, built and staged from source |
| NVIDIA DLSS / DLAA | Requires supported NVIDIA hardware and the NGX runtime; unavailable on Apple Silicon |
| FSR frame generation | Not integrated; the bundled FidelityFX build explicitly disables its frame-generation provider |
| MetalFX temporal upscaling | Not integrated into this Vulkan presenter |
| MetalFX frame interpolation | Not integrated into this Vulkan presenter |

**FSR 3 upscaling does not enable frame generation.** The frame-rate slider controls
real game rendering; it does not generate intermediate frames.

## Trying FSR 2 or FSR 3

Choose **GRAPHICS → UPSCALER → FSR 2** or **FSR 3**, then save. Use a resolution
scale that fits the output display. For example, a 1x render (1280×720) can be
upscaled to a 2560×1440 or 3840×2160 output. A render larger than the output is
downscaled, so the temporal upscaler does not run. At matching input/output sizes,
temporal upscalers can provide native-resolution antialiasing.

The game supplies depth, motion, jitter and camera-cut information. While a
temporal upscaler actually runs, it takes over antialiasing. Visual quality in
races, camera cuts, mirrors and menus still needs gameplay testing.

The Mac launcher enables both the FidelityFX API loader and the source-built
runtime. Setup refuses a configuration with missing provider headers or a missing
staged runtime, instead of silently producing a build without FSR 2/3. The runtime
lives at `upscalers/fidelityfx/1.1.4/libamd_fidelityfx_vk.dylib` beside the game.

## GPU verification

For an already configured game build:

```sh
python3 tools/test-macos-upscalers.py --build-dir out/build
```

For a launcher install, pass that version's
`data/source/<version>/out/build/mac-arm64-release` as `--build-dir` and run the
script in that version's extracted source. The diagnostic does not read game
assets or saves. It creates each actual runtime provider, submits three frames
through Vulkan, checks GPU readback against a known color and recreates the
context at a different output size. Results and runtime hash go into
`upscaler-smoke.json`; full output goes into `upscaler-smoke.log` in the build.
This checks GPU execution and repeated dispatch, not visual quality or motion
reconstruction in the game.

On 2026-11-01, FSR 2.3.3 and FSR 3.1.4 both passed on Apple M3/macOS 27 through
MoltenVK. Every frame returned the expected RGB values 64, 128, 191. Native
Metal device queries also reported hardware support for MetalFX temporal scaling
and frame interpolation on that machine; neither is connected to the game yet.

## Native equivalents and frame generation

Apple provides [MetalFX temporal upscaling](https://developer.apple.com/documentation/metalfx/mtlfxtemporalscalerdescriptor)
and [MetalFX frame interpolation](https://developer.apple.com/documentation/metalfx/mtlfxframeinterpolatordescriptor).
The locally installed SDK marks frame interpolation as macOS 26+. Support must
also be checked on the actual Metal device; the existing macOS 14 build baseline
must remain usable when interpolation is unavailable.

Adding these features requires a Vulkan/Metal texture and synchronization bridge
or a native Metal presenter, compatible depth/motion inputs, history management,
and an additional paced presentation for generated frames. The game and UI must
handle camera cuts, resizes, overlays and fallback. Simply enabling the FidelityFX
frame-generation build option is insufficient: its shader permutations,
frame-generation provider and swapchain/pacing integration are also absent here.
