# macOS performance checks

Measured on Apple Silicon with the pinned ShiftGlue Vulkan/MoltenVK backend,
2026-11-02. The reported live starting point was roughly 27–30 FPS in races and
15 FPS in menus with `fm4_frame_rate=60`, 1x resolution, 2x race MSAA and 4x
showroom MSAA.

The macOS defaults now enable `fh1_fold_clears` and
`fh1_skip_draws_without_effect`. Clear folding lets attachment load actions
consume pending clears instead of issuing separate clear passes. The second
option skips draws only when they have no attachment writes, occlusion query
or memory export. Both are existing SDK optimizations. Windows defaults retain
their previous values.

## Captured-frame comparisons

The race capture was frame 3245 (806,601 packet dwords); the second capture was
a scenic transition, frame 4024 (190,905 packet dwords). Each A/B pair used the
same repeat count, cached shaders and all other settings. Times below are wall
time between the SDK's `FH1 frame replay: frame` and `done` log entries, excluding
application startup. They include final readback and comparison overhead and
are not GPU timestamps or live gameplay frame times.

| Capture | Repeats | Both disabled | Both enabled | Final buffers |
|---|---:|---:|---:|---|
| Race | 240 | 8.420 s | 3.694 s | Byte-identical |
| Scenic transition | 120 | 4.951 s | 1.860 s | Byte-identical |

The race comparison's final SHA-256 was
`138cccc7c2b0a214f42f86a02aefa7ad5cbed61f13a99ec095af445d10ce5bef`.
The transition comparison's final SHA-256 was
`7294432e84b953c4f8c692744ba1de6174ed3ba1b99231d04b5e7b0790c9c99f`.

Reproduce using a local frame capture with the built executable and the normal
game/cache/config paths, adding:

```text
--gpu_record_thread=false
--fh1_frame_replay=/absolute/path/to/capture.fh1
--fh1_frame_replay_repeat=240
--fh1_fold_clears=false
--fh1_skip_draws_without_effect=false
```

Repeat with both final switches set to `true`. Run sequentially: replay writes
`<capture>.replay.bin` and `<capture>.replay.json`, so preserve the first outputs
before the second run. Compare buffers for matching repeat counts; repeated
execution changes retained render state, so different repeat counts need not
produce identical buffers. These captures are local diagnostics, not distributed
game assets.

Replay suspends the guest CPU and cannot qualify CPU/GPU coherency or simulation
speed. The original race capture also predates the graphics-corruption fix;
matching its baseline establishes equivalence between these settings, not
accuracy against the original front buffer. Live validation is recorded below;
broader track and showroom coverage remain separate checks.
The 16 KB page-coherency regression continues to pass; its implementation
is unchanged apart from explicitly including its logging dependency so the
standalone test builds without a precompiled header.

## Live follow-up: overlay submission contention

The first optimized build did **not** achieve stable 60 FPS. The user's race
screenshot showed 19.4 FPS (51.65 ms). A ten-second live `sample` of that build
contained 1,213 samples per long-lived thread:

- 1,052 main-thread samples were in MoltenVK's `getCAMetalDrawable`, underneath
  the presenter's `vkQueueSubmit`.
- 824 submission-worker samples were waiting for the graphics queue mutex.
- 245 recorder samples were in the per-draw subpage check, including 165 in
  `memcmp` and 63 waiting for the global memory lock.

The SDK's `pinyon_shift_fh1_source_presentation` defaults to false. The macOS app
now enables it: overlay ticks skip presentation when the guest refresh sequence
has already been presented. The existing SDK code still permits UI repainting
over a stalled title after 100 ms and a final repaint to erase a closed overlay.
This targets duplicate presentations that wait for a drawable while holding the
queue shared with game submissions. Build passed. The race-replay timings above
do not exercise this windowed path.

After restarting with source presentation enabled, the user confirmed on
2026-11-02: "sweet its playing stabily at 60 fps i closed the game". This
validates stable 60 FPS in the tested live race with the FPS overlay enabled.
The fix remains enabled by default on macOS. The follow-up report also confirms stable 60 FPS in menus, using default
graphics settings and a 60 FPS limit. Free Play used the first default car and
first default track, with no observed graphical issues. Frame-time percentiles
and all-track qualification remain unmeasured. See [validation](MACOS_PERFORMANCE.md).
