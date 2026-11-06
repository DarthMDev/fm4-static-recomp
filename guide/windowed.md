# Playing in a window

[← Back to the README](../README.md)

The game starts fullscreen by default. It can start in a window instead, and **Alt+Enter**
or **F11** switches between a window and fullscreen while you play.

## In the launcher

At the top of the launcher's **GRAPHICS**:

| Setting | What it does |
|---|---|
| **WINDOWED** | Starts the game in a window. |
| **WINDOW SIZE** | The window's size in real pixels: 1280x720 (the default), 1600x900, 1920x1080, 2560x1440 or 3840x2160; the larger sizes are offered while they fit your main display. |

Press **SAVE**; both apply from the next start. For a size the list does not have, use
[launch options](#launch-options).

## Switching while playing

**Alt+Enter** or **F11** switches between the window and fullscreen, in menus and in races alike;
from fullscreen it opens a window of WINDOW SIZE. The in-game **GRAPHICS → FULLSCREEN** row
shows the current window state and changes it immediately. None of these changes WINDOWED or
saves a separate fullscreen setting: the next start follows the launcher's setting again.

On macOS, use **Option+Enter**. F11 may be assigned to Show Desktop in macOS keyboard settings.

## Resizing

Drag the window's edges to resize it: it keeps its 16:9 shape. The next start goes back to WINDOW
SIZE; a dragged size is not remembered.

The picture always stays 16:9. A maximised window, or one set to another shape by launch options,
shows black bars around it. A window smaller than 1280x720 shows the game's usual 720p layout,
scaled down.

## Upscalers in a window

An upscaler (DLSS, FSR 2, FSR 3 or FSR 1) only upscales: it runs while the render (1280x720 times
RESOLUTION SCALE) fits the window. In a smaller window the picture is downscaled without it, the
UPSCALER row on the in-game GRAPHICS page reads e.g. `DLSS (OFF AT 2X)`, and MSAA and post AA apply
again. Resizing the window over or under that size turns the upscaler on or off as you drag.

With WINDOWED on, the RESOLUTION SCALE and UPSCALER rows of the launcher's GRAPHICS go by WINDOW
SIZE instead of your screen: with an upscaler chosen, turning WINDOWED on or picking a smaller
size lowers a scale the upscaler cannot run at, shown as a change before SAVE. A 1280x720 window
fits only 1x. More on the upscalers: [Upscalers](upscalers.md).

## Launch options

For the [manual install](manual-install.md#4-play), or a size the launcher does not offer
(through [COMMAND-LINE OPTIONS](launch-options.md#with-the-launcher-command-line-options)):

| Option | Meaning |
|---|---|
| `--fullscreen=false` | Start in a window. |
| `--window_width=<n>`, `--window_height=<n>` | The window's size, multiplied by Windows' display scale: at 150 %, `--window_width=1600` opens a window 2400 pixels wide. WINDOW SIZE in the launcher is in real pixels instead. |
| `--window_aspect_ratio=16:9` | The shape the window keeps at its start and while resized (the default). `free` leaves it free, with black bars around the 16:9 picture. |

## Older versions

A version before 0.3.1, after [GO BACK TO](launcher.md#updates) or while an update builds, starts
fullscreen whatever WINDOWED says, and has no Alt+Enter. F11 and the in-game FULLSCREEN row
require version 0.3.2 or newer.
