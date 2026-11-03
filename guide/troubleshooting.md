# Common problems

[← Back to the README](../README.md)

Problems players have run into, and what fixes them. Not listed here? See
[Reporting issues](reporting-issues.md).

## I want Project Forza Plus 4, but my launcher is already set up for vanilla

An install cannot be switched to the mod. Set up a second one:

1. Extract `FM4StaticRecomp-Launcher.zip` again, into a **new, empty folder** (for example
   `D:\FM4-PFP4`), not over your current one.
2. Start `FM4StaticRecompLauncher.exe` from the new folder and choose **PROJECT FORZA PLUS 4** at
   its first step. Then follow [Project Forza Plus 4 → Setup](project-forza-plus.md#setup).

Your vanilla install and its saves stay as they are. Start a new career on the mod: vanilla saves
do not work there.

## The game looks frozen on its first start

The first start of every new version builds its shaders for up to 40 seconds before the title
screen, and the window may not respond meanwhile. Wait; later starts skip this.

## The game crashes in a busy moment, a different spot each time

Typical sign: the game closes during or right after a heavy scene (for example the end of the
World Tour Top Gear bowling challenge), and its log has
`Failed to submit a Vulkan command buffer: -4`. The GPU stopped responding.

**Most likely cause: an overclocked or undervolted GPU or CPU.** Short drops and spikes in GPU
load swing the clocks, and a tuned voltage curve that is stable in benchmarks and other games
fails on the swing. On a test PC an overclocked card crashed in 6 of 8 runs of the same
challenge, and in none of 4 at stock settings.

1. In MSI Afterburner, or the tuning app of your graphics card's maker, press **Reset** to go
   back to the default clocks and voltage. Turn off any "OC mode" there as well.
2. If you tuned your CPU in the BIOS (an undervolt or an overclock), set it back to its defaults.
3. Restart the PC and play the same spot again.

If it still crashes at stock settings, [report it](reporting-issues.md#crashes) with REPORT CRASH.

## Stutter, or the game feels uneven at a high frame rate

The game ticks at FRAME RATE whether or not the PC draws every frame, so a rate the PC cannot
hold jumps instead of slowing down evenly. Set **FRAME RATE** on the GRAPHICS page
(**BACK + START** or **F6**) to a rate your PC holds in a full race with AI cars, not on an empty
track. If it still stutters, lower it further or lower **RESOLUTION SCALE**. On a display
without G-Sync or FreeSync, pick the refresh rate or a whole fraction of it (144 Hz: 144 or 72).
More in [High frame rates](high-frame-rate.md#picking-a-rate).

## Sound is slow, late or crackles on an older CPU

The game's sound needs CPU time next to the race; an older or slower CPU that is busy on every
core falls behind. Set **FRAME RATE** to 60, and in Windows **Settings → System → Power &
sleep → Additional power settings** choose **High performance**. Close other programs while
playing. If the sound still crackles, turn on SETTINGS → ADVANCED → **30 FPS (DIAGNOSTIC)** in
the launcher, press SAVE and play a race: say in your report whether the sound is clean at 30 fps.
Then turn it off again and press SAVE.

## DLSS or FSR does not turn on (`DLSS (OFF AT 3X)`)

The upscalers only upscale: the render must not be larger than the game's window or screen.
Pick a **RESOLUTION SCALE** up to your screen's size (e.g. 1x or 2x on a 1440p display). A desktop
resolution raised with NVIDIA DSR counts as the screen's size. See
[Graphics settings](graphics.md) and [Upscalers](upscalers.md).

## Tearing

On a display without G-Sync or FreeSync, turn **VSYNC** on in the GRAPHICS page.

## Lossless Scaling or NVIDIA Smooth Motion do nothing

They do not see the game's frames by default. In the launcher, turn on SETTINGS → ADVANCED →
**PRESENT THROUGH DXGI** and press SAVE (see [Frame generation](launch-options.md#frame-generation)).

## Alt-tab is slow and the screen flashes, or Windows HDR is off after playing (NVIDIA)

On some NVIDIA cards the game's fullscreen acts like exclusive fullscreen. Switching to another
window takes a moment and the screen goes black for an instant. Windows HDR can also be off after
the game closes. In the launcher, turn on SETTINGS → ADVANCED → **PRESENT THROUGH DXGI** and press
SAVE.

## A car's picture in the garage is broken (noise, no shadow under the car)

The game draws a car's picture once, when the car arrives in the garage, and keeps it in the
save, so a broken one stays broken. Saves imported from Xenia bring Xenia's pictures with them.
Choose that car, then **PAINT → PAINT CAR**, repaint the body once and save: the game draws the
picture again.

## Linux, Steam Deck or Proton

Not supported yet: the setup fails there (for example "licences could not be read"). A native
Linux build is on the [roadmap](../README.md#roadmap).
