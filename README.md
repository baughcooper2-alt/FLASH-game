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

| Action | Keyboard / mouse | Xbox / GameSir controller |
|---|---|---|
| Move / look | WASD / mouse | Left stick / right stick |
| Speed tier up / down | E / Q | RB / LB |
| Jump (speed jump at high speed) | Space | A |
| Brake: tap = instant stop, hold + steer = drift | Left Shift | LT |
| Speed boost (in the air: air dash) | Left Ctrl | L3 (click left stick) |
| Punch (hold near a bot: rapid punches) | Left mouse | X |
| Lightning throw (hold: ground lightning) | Right mouse | B |
| Afterimage decoys | V | R3 (click right stick) |
| Special power, or the context action shown | G / middle mouse | Y |
| Pick special power | Z / X, mouse wheel | D-pad left / right |
| Speed perception | Hold F | Hold RT |
| Start / restart circuit | T | D-pad up |
| Return to S.T.A.R. Labs | R | View |
| Pause, suits (Q/E, LB/RB or D-pad while paused) | Esc | Menu |

## What is implemented

**Movement powers.** Four speed tiers; Speed Boost (also boosts wall climbs: Speed Climb); Air Dash; Speed Jump (momentum becomes height, up to ~15 m); Speed Drift; Instant Stop; Wall Running onto roofs; Water Running across the harbour above 24 m/s (gaps in the sea wall at the promenade; slower runners sink and return to shore); Ceiling Running (speed-jump into the training-straight tunnel roof, or wall-run up into it).

**Combat powers.** Punch combos ending in a Speed Uppercut; Rapid Punches; Mach Punch (punch a distant bot at speed); Infinite Mass Punch (charged by 4 s at Speed Force, released on the next Mach Punch); Speed Tackle (run into bots); Lightning Throw (homing, chains); Lightning Kick (punch in the air) and ground pound; Ground Lightning; Afterimage decoys that bots attack and that burst when hit. Specials (Y, paid from the Speed Force meter): Whirlwind Punch, Tornado Arms, Speed Barrage, Lightning Punch, Ground Lightning, Cyclone, Speed Steal, Vacuum Blast.

**Environmental powers.** Run circles to make a tornado (a whirlpool on water); vortices trap bots, smother fires and pull smoke or gas away (reverse tornado). Tornado Arms and Vacuum Blast extinguish fires; Speed Dig frees trapped civilians; Rapid Repair and Rapid Construction assemble scattered or stacked pieces. Four rotating emergencies (car fire, gas leak, building collapse, shelter construction) use them, with civilians who cough, wait and cheer.

**Enemies.** The supplied enemy bot, rigged as a Humanoid and animated with its own walk plus shared stand and run clips. Strikers brawl, Gunners keep their distance and fire bolts, Heavies hit hard and resist knockback. Waves warp in at rotating city sites. Health regenerates when out of combat; the Speed Force meter fills with speed and pays for powers.

**Character and look.** Realistic Flash (the supplied high-detail model, rigged in Blender) using the supplied Mixamo run; 20 suits including Barry's Seasons 1 to 9 (gold boots from Season 8), Reverse-Flash, Zoom, Kid Flash, Jesse Quick, XS, Godspeed and comics suits; Speed Force lightning in each suit's colour with bloom. S.T.A.R. Labs, the 1.56 km waterfront city, traffic, a nine-gate circuit and the controller panel are unchanged.

Speeds are 7 / 28 / 65 / 130 metres per second. “Mach” and “Speed Force” are gameplay tier names, not literal canon velocity. Top prototype speed is about 291 mph. This scale keeps turns and collision testing useful in the small district. No stamina system.

## Verification status

Unity 6000.0.32f1 has now imported and compiled the project, and the FlashPrototype scene runs in Play Mode. The textured character was visually checked in-game. Isolated runtime checks confirmed idle → run → jump-up → fall → grounded idle transitions, animation pause, and disabled root motion. Controller hardware, standalone builds, and sustained performance remain unverified.

In Unity, open **Window → General → Test Runner → EditMode → Run All** to run the included checkpoint and movement-step regression tests. Then use [the manual test checklist](Docs/PROTOTYPE_TESTING.md).

Wall traversal checks passed at simulated 20, 30, and 60 FPS: attachment, an overhanging roof transition, wall jump, brake release, and normal-speed collision. `WallRunVerification.Run()` runs these isolated checks in Play Mode. Physical Xbox controller testing remains pending; no controller was detected during the final check.

## Next milestones

1. Test and tune movement, camera, braking, collisions and controller input on the M2.
2. Speedster-specific animation (sprint lean, wall-run and phasing poses) and suit-specific geometry such as Jay Garrick's helmet or Savitar's armour.
3. Playtest wall running, then add water running and phasing with collision and camera tests.
4. Add civilian rescue interactions and one repeatable emergency.
5. Grow the city and living-world systems after traversal is stable.

Combat, villains, time travel, other maps and multiplayer remain planned. They are not implemented in this milestone. The long-term realistic, primarily CW-inspired direction remains unchanged.

## Project layout

- `Assets/FlashPrototype/Scripts`: input, motor, camera, procedural visuals/world, circuit and scene coordinator.
- `Assets/FlashPrototype/Shaders`: referenced prototype shader included in builds.
- `Assets/FlashPrototype/Tests/Editor`: fast-crossing and movement-step tests.
- `Assets/Scenes/FlashPrototype.unity`: explicit entry scene; first in build settings.
- `Docs/DEVELOPMENT_LOG.md`: implementation status and decisions.

The district uses Unity primitives. The character and five bundled animation FBXs come from the credited HatchXR Sketchfab archive; textures are 512 px. No extra animation or import package is required.

## Supplied character reference

The user-supplied `the-flash.zip` contains a posed OBJ and textures, without a skeleton or animation clips. Its single character was extracted, the missing material definitions were reconstructed, and nine textured materials were assigned. It is displayed in the Cortex suit case and, rigged, is now the playable character. To rebuild after changing the joints, run `blender -b --python Tools/Blender/rig_flash_realistic.py`, then **Flash → Configure realistic character** in Unity.
