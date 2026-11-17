# Knot & Spool (working title)

A cozy 2D thread-untangling puzzle for Android, built with Unity.

## How to play

- A tangle of colored yarn fills the top of the screen. Threads lying **under** other threads are dimmed and can't be pulled yet.
- Spools wait in columns at the bottom. Tap the front spool of a column to move it into the tray.
- A spool in the tray automatically pulls free threads of its color. After 3 threads it's full and leaves, freeing its slot.
- The tray has only 4–5 slots. If it's full and no spool can pull anything, you lose. Clear every thread to win.

Levels are generated from the level number (always the same layout for the same level) and are guaranteed solvable.

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

## Code layout

| File | Purpose |
| --- | --- |
| `Assets/Scripts/GameController.cs` | Bootstraps itself into any scene; level setup, tap input, pulling, win/lose, HUD |
| `Assets/Scripts/LevelGenerator.cs` | Random tangle + spool order that is solvable within the tray size |
| `Assets/Scripts/YarnThread.cs` | One strand (LineRenderer), free/blocked state, reel-in animation |
| `Assets/Scripts/Spool.cs` | Spool visuals, filling, move/complete/shake animations |
| `Assets/Scripts/Shapes.cs`, `Palette.cs` | Code-generated sprites and colors (no art assets needed) |
| `Assets/Editor/ProjectSetup.cs` | Scene creation, player settings, build menu |

Tuning knobs: difficulty curve at the top of `LevelGenerator.Generate`, tangle density via `CurveReach`.
