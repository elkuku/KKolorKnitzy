# Knot & Spool (working title)

A cozy 2D unravelling puzzle for Android, built with Unity. All graphics are generated in code.

## How to play

- A piece of knitted fabric hangs from a wooden rod at the top. It unravels from the **bottom row** upwards.
- Spools stand on a board below it. Tap a spool to move it into the tray. Only spools that are **reachable** can be tapped: they must be in the top row or touch an open path to it. Blocked spools are drawn darker, and walls block the way.
- Some spools are **hidden** (grey, marked "?"). They show their color once they become reachable.
- A spool in the tray winds bottom squares of its color, one square at a time. When it holds as many squares as its counter says, it's full and leaves, freeing its slot.
- The tray has 5 slots on level 1, 4 on levels 2–4, and 3 after that. If the tray is full and nothing can wind, or no reachable spool is left, you lose. Unravel the whole fabric to win.

Levels are generated from the level number, so a level always has the same layout. They grow bigger, add more colors and hidden spools, and leave fewer tray slots. Every level is checked by simulating the game before you get it.

## Setup

1. Install **Unity Hub**, then a **Unity 6 LTS** editor with the **Android Build Support** module (includes OpenJDK, Android SDK & NDK).
2. In Unity Hub: **Add → Add project from disk** → choose this folder.
3. On first open Unity imports packages. If it asks to enable the new Input System backend, accept and let it restart.
4. The editor script automatically creates `Assets/Scenes/Main.unity`, adds it to the build, and sets Android player settings
   (portrait, package `com.elkuku.knotandspool`, IL2CPP, ARM64, min API 25).
5. Press **Play** to test in the editor (click = tap).

## Building for Android

- Switch platform: **File → Build Profiles → Android → Switch Platform**.
- Menu **Knot & Spool → Build Android APK** writes `Builds/KnotAndSpool.apk`.
- Menu **Knot & Spool → Build and Run on Device** installs and starts it on a USB-connected phone (USB debugging on).
- Command line:
  ```bash
  Unity -batchmode -quit -projectPath . -buildTarget Android -executeMethod KnotAndSpool.EditorTools.ProjectSetup.BuildApkBatch
  ```

## Testing tools

- **Knot & Spool → Level Report** (or `-executeMethod KnotAndSpool.EditorTools.LevelReport.Run`) logs size, colors, spools, slots and hidden spools for levels 1–30.
- `tools/playtest.py` plays the game on a USB-connected phone via adb. The game logs its state as `KS_STATE {json}` lines, and the script taps spools based on them:
  ```bash
  python3 tools/playtest.py --levels 10 --fresh --solve --out playtest-out
  ```
  `--solve` uses an exact solver (mirrors the game's winding rules); without it a simple heuristic plays. `--fresh` clears saved progress first. Screenshots are saved to `--out`.

## Code layout

| File | Purpose |
| --- | --- |
| `Assets/Scripts/GameController.cs` | Bootstraps the game; fabric, board, tray, tap input, winding, win/lose, HUD, state logging |
| `Assets/Scripts/LevelGenerator.cs` | Generates the fabric, spools and walled board, and checks each level by simulation |
| `Assets/Scripts/Spool.cs` | Spool visuals, filling, reveal/move/complete/shake animations |
| `Assets/Scripts/Shapes.cs`, `Palette.cs` | Code-generated sprites (knit stitches, rounded rects, gradients) and colors |
| `Assets/Editor/ProjectSetup.cs` | Scene creation, player settings, build menu |
| `Assets/Editor/LevelReport.cs` | Difficulty table for the first 30 levels |
| `tools/playtest.py` | adb playtester and solver |

Tuning knobs: the difficulty curve at the top of `LevelGenerator.Generate` (fabric size, colors, spool size, tray slots, shuffle window, hidden-spool chance).
