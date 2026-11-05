# Keyboard

[← Back to the README](../README.md)

The keyboard plays beside a gamepad, with a layout after Forza Horizon 6's. While the
keyboard is the last device you used, the game's button prompts show keycaps of the bound
keys, and English prompt sentences ("PRESS THE BACK BUTTON...") name them too; touching the
pad brings the controller buttons back. **Alt+Enter** switches between a window and fullscreen
at any moment ([Playing in a window](windowed.md)).

## Driving

| Action | Keys |
|---|---|
| Throttle / brake | W / S |
| Steer | A / D |
| Handbrake | Space |
| Gear up / down | E / Q |
| Clutch | Shift |
| Camera | Tab |
| Rewind | R |
| Look around (back: Down) | Arrow keys |
| Look straight back | C |
| Telemetry | T, G |
| Pause | Esc |

Driving keys apply while the car is on track and the game is not paused. Steering, pedals
and looking around ramp up while a key is held and ease back on release.

## Menus

The pre-race screen, pause menu, replays and results use these keys too.

| Button | Keys |
|---|---|
| Move | W A S D, arrow keys |
| Select (A) | Enter, Space |
| Back (B) | Esc, Backspace |
| X / Y | X / Y |
| LB / RB | Q / E |
| LT / RT | Z / C |
| Left stick | Numpad 8 4 2 6 |
| Right stick | I J K L |
| BACK / START | Tab / P |

The mouse is not used.

## Rebinding keys

### In the launcher

1. Open the launcher and click **CONTROLS** under Play.
2. Choose **DRIVING** (on the track) or **MENUS**.
3. Click the key you want to change, then press the new key. Each action has a primary key
   and an optional secondary key (**NOT SET** until you click it); the **✕** beside a
   secondary key clears it. **RESET** puts one action back to its default, **RESET ALL** the
   whole list.
4. If you give an action a key that another action has by default, the game takes that key
   off the other action when it starts (it keeps its other key if it has one, otherwise it gets
   a new key), and the panel says so under it, with the lost key dimmed. If you set two
   actions to the same key yourself, both show "Also used by". Nothing stops you: change one
   of them if that is not what you meant.
5. Esc is a key like any other, so it does not back out of a rebind: click **CANCEL** or
   anywhere else instead.
6. The small button names under each action (A, RT and so on) are the game's default
   controller layout. If you chose another layout in the game's options, an action may be on
   a different button than the one named.
7. Click **SAVE** and start the game. Changes apply at the next launch.

**SMOOTHING** sets how many seconds steering, the pedals and looking around take to reach
full and to return to rest; lower is snappier.

### In the file

Or create `config\controls.toml` in the state folder and add `menu_<button>` or
`driving_<button>` lines. The launcher reads it and leaves the lines of actions you do not
change as they are; changing an action rewrites its line (or removes it when you put the
keys back to the default). A value lists keys separated by commas:

```toml
driving_x = "Q,Numpad1"
```

Changes apply at the next launch.

A key you bind leaves the button it had by default, so it does not do two things (unless you
bind it to two buttons yourself; `Shift+C` counts as C). A button left with no key gets a new
one: the old key of the button you moved there, or else a free key. The button prompts in the
game show where it went, and `runtime.log` lists every move
(`Controls: driving_right_shoulder C -> F`). Bind it yourself to pick a different key.

**Buttons:** `a`, `b`, `x`, `y`, `left_shoulder`, `right_shoulder`, `left_trigger`,
`right_trigger`, `lstick_up`, `lstick_down`, `lstick_left`, `lstick_right`, `lstick_press`,
the same five for `rstick_`, `dpad_up`, `dpad_down`, `dpad_left`, `dpad_right`, `back`,
`start`.

**Key names:** `A`..`Z`, `0`..`9`, `F1`..`F24`, `Up`, `Down`, `Left`, `Right`, `Space`,
`Return`, `Escape`, `Backspace`, `Tab`, `Shift`, `Control`, `Alt`, `Numpad0`..`Numpad9` and
the like.

**Ramps**, in seconds from rest to full and back:

| Key | Default |
|---|---|
| `ramp_steer_seconds` | 0.17 |
| `ramp_steer_return_seconds` | 0.10 |
| `ramp_pedal_seconds` | 0.10 |
| `ramp_pedal_return_seconds` | 0.08 |
| `ramp_look_seconds` | 0.15 |
