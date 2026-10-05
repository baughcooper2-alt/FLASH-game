# Flash Game — city and traversal prototype

Unity **6000.0.32f1** · URP **17.0.3** · Input System **1.14.2**

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

The pause menu has mouse-operated toggles for lightning trails, dynamic field of view, and control hints. Controller Menu resumes play. Focus loss pauses the simulation. USB and Bluetooth controllers connect automatically when recognized by macOS. Input System 1.14.2 includes the native macOS USB Xbox layout. A USB data cable is required; physical-controller verification is still pending.

Use **Esc → Connect / test Xbox controller** for pairing instructions, a shortcut to Bluetooth settings, automatic connection status, and live stick/trigger/button readings. Pair in system settings, then return to the Game view to test. USB data cables are also supported.

To run up a building, select a speed level above Normal with RB / E and run straight toward a tall wall. Keep moving to climb and automatically crest onto the roof. Press A / Space to jump away; LT / Left Shift or releasing movement detaches.

## What is implemented

- Four manually selected speed levels, camera-relative movement, slower steering at high speed, braking, gravity, jumping, and momentum loss on collisions.
- Vertical wall running, collision-stepped rooftop transitions, and outward wall jumps, with an animated wall pose and upward camera framing.
- Third-person orbit camera with obstruction checks, optional speed-based FOV, and no camera shake or motion blur.
- Rigged, textured Flash character with Humanoid idle/walk/run blending, jump-up and falling clips, and thin, flickering amber lightning filaments. The original mannequin remains a fallback. See [character credits](Assets/FlashPrototype/Character/ATTRIBUTION.md).
- A 1.56 km square district with 45–248 m towers, patterned facades, rooftop details, trees, parks, a waterfront promenade, and two bridges. An oval S.T.A.R. Labs has a walk-in atrium; selected towers have open lobbies. This is a procedural first art pass, not finished photorealistic architecture.
- Ten placeholder traffic vehicles. Speed perception slows these vehicles while keeping player control responsive.
- Nine-gate city circuit, real-time race clock excluding pause, swept gate detection, restart, and locally saved best time.
- Speedometer, speed tier, distance, FPS, control hints, and pause menu.
- Frame-rate target of 60, simple shared materials, inexpensive directional shading, directional sunlight and 240 m shadow distance, and limited traffic. **Mac performance has not been measured yet.**

Speeds are 7 / 28 / 65 / 130 metres per second. “Mach” and “Speed Force” are gameplay tier names, not literal canon velocity. Top prototype speed is about 291 mph. This scale keeps turns and collision testing useful in the small district. No stamina system.

## Verification status

Unity 6000.0.32f1 has now imported and compiled the project, and the FlashPrototype scene runs in Play Mode. The textured character was visually checked in-game. Isolated runtime checks confirmed idle → run → jump-up → fall → grounded idle transitions, animation pause, and disabled root motion. Controller hardware, standalone builds, and sustained performance remain unverified.

In Unity, open **Window → General → Test Runner → EditMode → Run All** to run the included checkpoint and movement-step regression tests. Then use [the manual test checklist](Docs/PROTOTYPE_TESTING.md).

Wall traversal checks passed at simulated 20, 30, and 60 FPS: attachment, an overhanging roof transition, wall jump, brake release, and normal-speed collision. `WallRunVerification.Run()` runs these isolated checks in Play Mode. Physical Xbox controller testing remains pending; no controller was detected during the final check.

## Next milestones

1. Test and tune movement, camera, braking, collisions and controller input on the M2.
2. Refine the lightweight animated Flash character toward the realistic CW visual target; retain the placeholder as a fallback.
3. Playtest wall running, then add water running and phasing with collision and camera tests.
4. Add civilian rescue interactions and one repeatable emergency.
5. Grow the city and living-world systems after traversal is stable.

Combat, villains, suit selection, time travel, other maps and multiplayer remain planned. They are not implemented in this milestone. The long-term realistic, primarily CW-inspired direction remains unchanged.

## Project layout

- `Assets/FlashPrototype/Scripts`: input, motor, camera, procedural visuals/world, circuit and scene coordinator.
- `Assets/FlashPrototype/Shaders`: referenced prototype shader included in builds.
- `Assets/FlashPrototype/Tests/Editor`: fast-crossing and movement-step tests.
- `Assets/Scenes/FlashPrototype.unity`: explicit entry scene; first in build settings.
- `Docs/DEVELOPMENT_LOG.md`: implementation status and decisions.

The district uses Unity primitives. The character and five bundled animation FBXs come from the credited HatchXR Sketchfab archive; textures are 512 px. No extra animation or import package is required.

## Supplied character reference

The user-supplied `the-flash.zip` contains a posed OBJ and textures, without a skeleton or animation clips. Its single character was extracted, the missing material definitions were reconstructed, and nine textured materials were assigned. The model is displayed on the S.T.A.R. Labs exhibit pedestal. The animated player retains the earlier rig with a darker suit; replacing it with this exact model still requires rigging and skinning.
