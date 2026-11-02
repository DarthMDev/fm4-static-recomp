# Launcher

[← Back to the README](../README.md)

The launcher sets everything up from your discs and starts the game. For the command line
instead, see the [manual install](manual-install.md).

## First setup

1. Download `FM4StaticRecomp-Launcher.zip` from the
   [releases](https://github.com/i2kiselev/fm4-static-recomp/releases) and extract it into a
   short folder you own, for example `D:\FM4`.

   > [!WARNING]
   > Not into `Program Files`, and not by starting the launcher from inside the zip's window:
   > Windows runs it from a temporary folder then.

2. Start `FM4StaticRecompLauncher.exe`. Its first screen has these steps:

   | Step | What happens |
   |---|---|
   | **Game** | Vanilla, or [Project Forza Plus 4](project-forza-plus.md) 1.3 over it. Fixed for the install. |
   | **Disc 1** | Drop or choose your ISO, GOD folder or extracted folder. It is verified and extracted. |
   | **Disc 2** (optional) | An ISO, a GOD folder or the folder with its four packages, or **Later** (CONTENT installs it any time). It is verified now and installed after the build, so keep it where it is until then. |
   | **Microsoft licence** | The build downloads Microsoft's C++ runtime and Windows SDK files from Microsoft. Read and accept their licences, or decline and use Microsoft's installer instead, which asks for administrator permission once. |
   | **Build** | The launcher downloads its build tools, translates and compiles the game. |
   | **Your profile** | A name; the gamertag starts as it. |

3. **PLAY**. The first start compiles the game's shaders for your GPU before the title screen:
   about 30 seconds, longer on a processor with fewer threads, and the game may look frozen
   meanwhile. Later starts take a second or two; the compile runs again after an update and
   after a GPU driver update.

## The main screen

| Button | What it does |
|---|---|
| **PLAY** | Starts the game. |
| **PROFILE** | Adds, renames, imports and deletes profiles (separate saves). |
| **CONTENT** | Installs Disc 2 and DLC. Install them only here: the game's own Disc 2 install waits for a disc change that never comes. Also [unlocks the unicorn cars](#unicorn-cars). |
| **GRAPHICS** | The [GRAPHICS page's settings](graphics.md). **SAVE** on top writes this page and SETTINGS together. |
| **SETTINGS** | The game's language, launch options (among them [playing in a window](#playing-in-a-window)), advanced options (among them the [saves folder](#the-saves-folder)) and [command-line options](launch-options.md#with-the-launcher-command-line-options) for debugging. |

## Playing in a window

SETTINGS → LAUNCH OPTIONS → **WINDOWED** starts the game in a window, and **WINDOW SIZE** picks
its size in pixels. **Alt+Enter** switches between the window and fullscreen at any moment while
you play, also after a fullscreen start, and leaves the setting as it is.

Drag the window's edges to resize it, and it keeps its 16:9 shape; the next start goes back to
WINDOW SIZE. For a size the list does not have, use
[COMMAND-LINE OPTIONS](launch-options.md#with-the-launcher-command-line-options).

- The picture stays 16:9: a maximised window, or one set to another shape, shows black bars.
- An upscaler runs only while the render (1280x720 x RESOLUTION SCALE) fits the window. In a
  smaller window the picture is [downscaled without it](upscalers.md). With WINDOWED on, the
  launcher's GRAPHICS goes by WINDOW SIZE instead of your screen.

## A career from Xenia Canary

1. Close Xenia.
2. **PROFILE → IMPORT SAVE**, and choose Xenia's `content` folder: the one beside
   `xenia_canary.exe`, or `Documents\xenia\content`. If several Xenia profiles have FM4 saves,
   open `content` and choose the folder of yours instead (16 letters and digits, such as
   `E030000047B76145`).
3. Answer **VANILLA SAVE** (or **PFP4 SAVE** for a Project Forza Plus 4 career) and name the
   profile.

The game accepts a save only from the Xbox profile it was made with, so the new profile plays as
that Xenia profile. Xenia's copy is not changed. Install your Disc 2 and DLC in **CONTENT**.
Only Xenia Canary saves are supported.

The car pictures in the garage come with the save: the game draws each one once, when the car
arrives, and keeps it. A picture Xenia drew wrong (noise, no shadow under the car) stays wrong
here too. Repaint that car once and the game draws its picture again.

## Unicorn cars

Unicorn cars were rare gifts on Xbox Live; the game keeps them out of the Autoshow. With the game
closed, CONTENT → **UNLOCK UNICORN CARS** makes the 17 of the original disc buyable for every
profile of the install (some are on Disc 2, so install that too). It changes the game's database
and keeps the original as `data\game\Media\db\gamedb.before-unicorns.slt`. Rev 1 discs and
Project Forza Plus 4 have them unlocked already; their Warthog and Bentley 8 Litre stay locked,
since the game has only their Autovista model. An update that extracts Disc 1 again locks the
cars again: unlock them once more.

## Starting from Steam, Pegasus or another frontend

`FM4StaticRecompLauncher.exe --skip-launcher` starts the game without showing the launcher, on
the profile last chosen in it; `--profile "<name>"` picks another profile by its name. The
launcher waits in the background while the game runs and closes with it, so the frontend sees
the game running for its whole length. It does not check for updates then: start it as usual
for those.

Its window opens when you are needed: while setup or a new version's build is unfinished, for
an unknown profile name, when the game does not start, and after a crash (REPORT CRASH).

- **Steam:** Games → Add a Non-Steam Game to My Library, choose
  `FM4StaticRecompLauncher.exe`, then in the shortcut's Properties set Launch Options to
  `--skip-launcher`.
- **Pegasus, Playnite, LaunchBox and others:** the command
  `"D:\FM4\FM4StaticRecompLauncher.exe" --skip-launcher`, with your folder.

## The `data` folder

Everything lives in `data\` beside the launcher: the extracted disc, the build tools, the
builds, your saves and settings. Move or back up the folder as a whole, or keep the saves in a
folder of your own (below).

- A first setup with Disc 2 takes about **13 GB** there (Disc 1 7.7 GB, Disc 2 2.8 GB, the
  build tools 1.4 GB, and the game's source and build 1 GB), and setup asks for a 2 GB margin
  on top while it works.
- An update builds the new version beside the old one, about 1 GB more until the older one
  is removed.

> [!IMPORTANT]
> The folder's limits come from the build tools: the path of `data\` may be at most 54
> characters, may not hold `[` or `]`, and may not hold parentheses together with
> non-English letters. The launcher checks this at start and names the folder if it cannot
> use it.

### The saves folder

SETTINGS → ADVANCED → **SAVES FOLDER** keeps your saves outside `data\`, for example in a folder
you back up or sync, or one that stays when you delete the launcher's folder. **CHOOSE…** a
folder, then **SAVE**: the launcher moves its profiles, their backups and deleted profiles there
(as `profiles\`, `backups\` and `deleted\`). **DEFAULT** moves them back into `data\`. Disc 2 and
DLC stay in `data\`.

- **After a reinstall**, choose the saves folder you used before: its profiles appear again. The
  new install's own profiles are added beside them; one of the same name gets a number, "Main (2)",
  and an empty one that setup just made (no save yet) goes to `deleted\` instead. If your saves
  were in the old install's `data\` folder, that folder is refused (it belongs to the old
  launcher): bring each profile over with **PROFILE → IMPORT SAVE**.
- The folder must be on a drive inside this PC: a network drive or a USB stick formatted
  FAT32 or exFAT cannot hold the saves.
- Choose the folder that holds `profiles\`, not `profiles\` itself.
- Moving the saves away from a folder removes them there, also from a synced folder on your other
  PCs.
- If the saves folder is missing when the launcher starts (its drive not connected, or the folder
  renamed), the launcher says so and makes no new profile until it is back or you choose another.
- **Vanilla and Project Forza Plus 4 saves don't mix.** If the folder holds saves of the other kind
  of install, the launcher warns first: they are not guaranteed to work there
  ([why](project-forza-plus.md#its-own-install)), and starting a fresh career is recommended. To
  play both, give each install its own saves folder.

## Updates

The launcher checks GitHub when it starts (and from CHECK FOR UPDATES).

1. **UPDATE TO** downloads the new release and restarts.
2. The new version then builds beside the current one, which you can keep playing.
3. **GO BACK TO** switches back.

Saves, content and settings are kept; each profile's save is zipped to `backups\` in the saves
folder (`data\backups\` by default) before a new version first runs it.
