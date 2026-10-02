# Flash Game — movement prototype 01

Unity **6000.0.32f1** · URP **17.0.3** · Input System **1.11.2**

## Play on your Mac

1. In GitHub Desktop, select `FLASH-game`, then **Fetch origin → Pull origin**.
2. In Unity Hub, open the **outer repository folder** containing `Assets`, `Packages`, and `ProjectSettings` directly. Do not open the old nested `Flash Game` copy.
3. Wait for the scripts and shaders to import.
4. Open **Assets/Scenes/FlashPrototype.unity** in the Project window.
5. Press **Play**, then click the Game view if it does not have focus.

The district, runner and HUD are created when Play starts. The scene intentionally contains only its bootstrap component before Play. No dragging scripts onto objects or manual input wiring is required. The original SampleScene is preserved.

## Controls

| Action | Keyboard / mouse | Xbox controller |
|---|---|---|
| Move | WASD | Left stick |
| Look | Mouse | Right stick |
| Increase speed level | E | RB |
| Decrease speed level | Q | LB |
| Jump | Space | A |
| Brake | Hold Left Shift | Hold LT |
| Speed perception | Hold F | Hold RT |
| Start / restart circuit | T | Y |
| Return to spawn / cancel race | R | View |
| Pause / resume | Esc | Menu |

The pause menu has mouse-operated toggles for lightning trails, dynamic field of view, and control hints. Controller Menu resumes play. Focus loss pauses the simulation. Connect the controller to macOS before testing; input actions also support hot plugging.

## What is implemented

- Four manually selected speed levels, camera-relative movement, slower steering at high speed, braking, gravity, jumping, and momentum loss on collisions.
- Third-person orbit camera with obstruction checks, optional speed-based FOV, and no camera shake or motion blur.
- Original lean burgundy/gold primitive mannequin with procedural arm/leg motion and three trails. This is **not** the final realistic character or a Mixamo animation.
- A roughly 680 m square graybox district with streets, 60 placeholder buildings, an enterable S.T.A.R. Labs training annex and a 650 m acceleration lane.
- Six placeholder traffic vehicles. Speed perception slows these vehicles while keeping player control responsive.
- Nine-gate city circuit, real-time race clock excluding pause, swept gate detection, restart, and locally saved best time.
- Speedometer, speed tier, distance, FPS, control hints, and pause menu.
- Frame-rate target of 60, simple shared materials, inexpensive directional shading, no runtime shadow maps, and limited traffic. **Mac performance has not been measured yet.**

Speeds are 7 / 28 / 65 / 130 metres per second. “Mach” and “Speed Force” are gameplay tier names, not literal canon velocity. Top prototype speed is about 291 mph. This scale keeps turns and collision testing useful in the small district. No stamina system.

## Verification status

This version was authored without access to a Unity Editor. Source structure and scene/meta references were checked, but **Unity compilation, shader compilation, Play Mode, controller behavior, builds, and FPS still need Editor verification**. Do not treat a source check as a passed gameplay test.

In Unity, open **Window → General → Test Runner → EditMode → Run All** to run the included checkpoint and movement-step regression tests. Then use [the manual test checklist](Docs/PROTOTYPE_TESTING.md).

## Next milestones

1. Test and tune movement, camera, braking, collisions and controller input on the M2.
2. Import a verified lean Humanoid model and licensed movement animations; retain the placeholder as a fallback.
3. Add wall running, water running, then phasing, each with explicit collision and camera tests.
4. Add civilian rescue interactions and one repeatable emergency.
5. Grow the city and living-world systems after traversal is stable.

Combat, villains, suit selection, time travel, other maps and multiplayer remain planned. They are not implemented in this milestone. The long-term realistic, primarily CW-inspired direction remains unchanged.

## Project layout

- `Assets/FlashPrototype/Scripts`: input, motor, camera, procedural visuals/world, circuit and scene coordinator.
- `Assets/FlashPrototype/Shaders`: referenced prototype shader included in builds.
- `Assets/FlashPrototype/Tests/Editor`: fast-crossing and movement-step tests.
- `Assets/Scenes/FlashPrototype.unity`: explicit entry scene; first in build settings.
- `Docs/DEVELOPMENT_LOG.md`: implementation status and decisions.

No external character, texture or animation downloads are bundled. Prototype geometry is made from Unity primitives.
