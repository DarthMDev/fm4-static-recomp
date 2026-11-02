# MetalFX implementation plan

The current Mac backend is Vulkan/MoltenVK, not the old hl360 native Metal
renderer. Keep that working baseline available while adding the following stages.
No MetalFX quality, performance or frame-generation result is claimed yet.

## 1. Reproducible fallback

The tested SDK and Mac pin both use `2460deb30668b49e272497929b07a2f2244c88f7`.
Ship the four checked patches and their hashes with the launcher source payload.
Validate a managed SDK installation and retain receipts. The user confirmed stable
60 FPS with default graphics and a 60 FPS limit in menus and the first default Free
Play car/track. FSR 2/3 GPU smoke dispatches are separate from in-game qualification:
explicitly test each upscaler through driving, camera cuts, resize and menus.

## 2. Vulkan–Metal feasibility gate

Use the exact MoltenVK library staged with the game, not the system Vulkan SDK.
[`VK_EXT_metal_objects`](https://docs.vulkan.org/features/latest/features/proposals/VK_EXT_metal_objects.html)
exports Metal device, queue, textures and shared events. Export intent must be
specified when the Vulkan objects are created. Extension enumeration alone is
insufficient proof of interoperability.

Acceptance requires color, depth and motion resources in the renderer’s actual
formats; Vulkan writes visible to Metal; Metal output visible to Vulkan; and
GPU-side synchronization in both directions. No per-frame CPU texture readbacks,
CPU waits or extra presentation loops. Use a standalone synthetic check first,
then prove resource lifetime, layouts and synchronization in the renderer.
Test recreation on resize and unsupported-extension fallback. Record the runtime
library hash and device with each result.

The standalone round trip passed on Apple M3 on November 2, 2026, using the
build’s MoltenVK 1.4.1 library: RGBA8 color/output, R32F reconstructed depth and
RG16F motion, with exported timeline shared-event values 1 → 2 → 3. Metal checks
all synthetic input pixels, Vulkan copies Metal’s output, and a second Metal
kernel validates every copied pixel. A four-byte status counter is read only at
test completion; no textures are read back. Sizes 64×64 and 96×96 each recreate
the instance, device and resources. This proves those formats and the standalone
path, not live renderer ownership or frame pacing.

```sh
python3 tools/test-macos-metal-interop.py --build-dir out/build/mac-arm64-release
```

The helper compiles a standalone Objective-C++ test using the SDK’s Vulkan
headers and the build’s exact MoltenVK library. It writes `metal-interop/test.log`
and `metal-interop/result.json`, including the library hash. A 60-second subprocess
timeout bounds synchronization failures. Live renderer integration is the next
gate before selecting MetalFX in gameplay.

## 3. Temporal upscaling

Add MetalFX alongside FSR 2 and FSR 3 as separate choices only after interoperability
passes. Use Apple’s [temporal scaler API](https://developer.apple.com/documentation/metalfx/mtlfxtemporalscalerdescriptor)
and device capability checks. Feed the renderer’s color, depth, motion, jitter,
exposure and reset state with verified coordinate conventions and texture usage.
Keep HUD composition outside reconstruction where practical. Reset history on
camera cuts, resizing and mode changes. Unsupported devices retain a working
upscaler selection. Compare the same race/camera with FSR using GPU timings and
image inspection; no equivalence to DLSS is assumed.

## 4. Frame interpolation

Add [MetalFX frame interpolation](https://developer.apple.com/documentation/metalfx/mtlfxframeinterpolatordescriptor)
after temporal upscaling. Gate on macOS 26+ and runtime device capability. The M3
was reported to support it; do not infer support for every Apple Silicon device.
Initial target: 60 rendered → 120 displayed FPS. Handle presentation pacing,
HUD composition, camera cuts, resize, minimized windows and loss of capability.
Count rendered, generated and displayed frames separately and measure latency.
A higher overlay refresh rate is not proof of a higher game frame rate.

The guest FPS overlay currently counts new guest image notifications. It does not
measure display completion, simulation updates or input-to-display latency.
