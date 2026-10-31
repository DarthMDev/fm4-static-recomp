# Project Forza Plus 4

[← Back to the README](../README.md)

[Project Forza Plus 4](https://www.reddit.com/user/Gas-and-Games/comments/17b9k70) (PFP4) is a
large car, career and physics mod for FM4 by Gas-and-Games, made for Xenia. The launcher sets up
an install of its version 1.3 from your Disc 1 and the mod as you downloaded it. This repository
contains none of the mod's files.

## Its own install

A launcher install is either vanilla or Project Forza Plus 4, chosen at its first setup and fixed
for good. To play both, extract the launcher into a second folder and set that one up for the
mod; each keeps its own saves.

Start a fresh career on the mod. It removes and retargets many vanilla parts (engine and
drivetrain swaps among them), so a vanilla career carried over hangs or breaks races; Xenia does
the same. Saves therefore never cross between the two kinds of install.

## Setup

1. Extract the launcher into a new folder, as in [First setup](launcher.md#first-setup).
2. **Game:** choose **PROJECT FORZA PLUS 4**.
3. **Disc 1:** your Disc 1, as for vanilla. 1.0 and Rev 1 discs both work: the install runs the
   mod's own executable on either.
4. **Project Forza Plus 4 1.3:** the mod's zip as you downloaded it from the author's post (read
   in place, never extracted) or its extracted folder. Every file setup takes is checked against
   version 1.3; any other version is refused and nothing is changed. Disc 2 and every DLC pack
   come from the mod, so there is no Disc 2 step. Pick the career mode and Harder AI here.
5. **Microsoft licence**, **Build** and **Your profile** as for vanilla. The profile starts with
   **NEW CAREER** (the intro race) or a **SKIP INTRO SAVE** (see below).

The install takes about 20 GB in `data\`: as a vanilla one, with the mod's content (9.3 GB) in
place of Disc 2's 2.8 GB, and its career modes kept for later changes (0.25 GB).

> [!IMPORTANT]
> Keep the zip. An update that needs Disc 1 extracted again asks for the mod again too, and so
> does **CHOOSE THE MOD AGAIN** in CONTENT, which puts back the saved career modes and Skip Intro
> saves if they are missing or damaged.

## Career modes and Harder AI

| Mode | What it is |
|---|---|
| **Normal** | The mod's default: new starter cars, every event open from the start, endurance World Tour, stronger AI. |
| **Hard** | Normal, but you start in F class and unlock a class every 8 levels; longer races, half the earnings. |
| **Sandbox** | Every car and upgrade costs 1 credit. |
| **Vanilla** | The original career with the mod's cars and physics. |

**Harder AI** is the mod's optional variant of each mode: stronger AI drivers and AI upgrades.

SETTINGS → PROJECT FORZA PLUS 4 changes both later, from files setup kept (no zip needed).
Garages survive a change; a career started in another mode loads with a warning, once.

## Saves

- Each profile records its game and the mode it last ran in.
- **PROFILE → IMPORT SAVE** asks whether the saves are vanilla or Project Forza Plus 4 (a profile
  of this launcher answers by itself) and refuses the other game's. If Xenia's DLC folder beside
  the saves says otherwise, it warns first.
- Vanilla saves are not supported on this install: IMPORT SAVE refuses them, and SETTINGS →
  SAVES FOLDER warns before it takes over a folder that holds them. They are not guaranteed to
  work; start a fresh career, and give the mod install a saves folder of its own.
- Saves made on Xenia do not load yet: the game ties a save to the profile it was made with
  ([#35](https://github.com/i2kiselev/fm4-static-recomp/issues/35)).
- **Skip Intro saves** are the mod's ready-made careers, one per starter car of the mode. The
  starter car's garage picture is broken until you repaint the car once.

## What is left out

| Mod files | Why |
|---|---|
| The tracks' `TrackSettings*.xml` | Xenia workarounds (no shadows, no specular, no road detail blur); this port draws those. |
| `RenderScenarios` | 60 fps replays and full-rate mirrors; the port already runs those scenes at full rate. |
| `PhysicsSettings.ini for higher FPS` | Fixed frame-rate variants; the port keeps physics right at any [frame rate](high-frame-rate.md). |
| `xenia-canary.config.toml`, the instructions | Xenia's setup. |

The mod's camera FOV instructions still work on `data\game\Media\Camera\CameraSettings.ini`; an
update that extracts Disc 1 again undoes the edit. The mod's own known issues (in its
`Install Instructions and Other Info`) apply here too.
