# Flash Game — city and traversal prototype

Unity **6000.0.32f1** · URP **17.0.3** · Input System **1.14.2**

## Play on your Mac

1. In GitHub Desktop, select `FLASH-game`, then **Fetch origin → Pull origin**.
2. In Unity Hub, open the **outer repository folder** containing `Assets`, `Packages`, and `ProjectSettings` directly. Do not open the old nested `Flash Game` copy.
3. Wait for the scripts and shaders to import.
4. Open **Assets/Scenes/FlashPrototype.unity** in the Project window.
5. Press **Play**, then click the Game view if it does not have focus.

The district, runner and HUD are created when Play starts. The scene intentionally contains only its bootstrap component before Play. No dragging scripts onto objects or manual input wiring is required.

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
| Special power, or the context action shown (grab / drop a bomb) | G / middle mouse | Y |
| Pick special power | Z / X, mouse wheel | D-pad left / right |
| Speed perception | Hold F | Hold RT |
| Start / restart circuit | T | D-pad up |
| Return to S.T.A.R. Labs | R | View |
| Pause, suits and Barry out of costume (Q/E, LB/RB or D-pad while paused) | Esc | Menu |

## What is implemented

**Movement powers.** Four speed tiers; Speed Boost (also boosts wall climbs: Speed Climb); Air Dash; Speed Jump (momentum becomes height, up to ~15 m); Speed Drift; Instant Stop; Wall Running onto roofs; Water Running across the harbour above 24 m/s (gaps in the sea wall at the promenade; slower runners sink and return to shore); Ceiling Running (speed-jump into the training-straight tunnel roof, or wall-run up into it).

**Combat powers.** Punch combos ending in a Speed Uppercut; Rapid Punches; Mach Punch (punch a distant bot at speed); Infinite Mass Punch (charged by 4 s at Speed Force, released on the next Mach Punch); Speed Tackle (run into bots); Lightning Throw (homing, chains); Lightning Kick (punch in the air) and ground pound; Ground Lightning; Afterimage decoys that bots attack and that burst when hit. Specials (Y, paid from the Speed Force meter): Whirlwind Punch, Tornado Arms, Speed Barrage, Lightning Punch, Ground Lightning, Cyclone, Speed Steal, Vacuum Blast.

**Environmental powers.** Run circles to make a tornado (a whirlpool on water); vortices trap bots, smother fires and pull smoke or gas away (reverse tornado). Tornado Arms and Vacuum Blast extinguish fires; Speed Dig frees trapped civilians; Rapid Repair and Rapid Construction assemble scattered or stacked pieces. Four rotating emergencies (car fire, gas leak, building collapse, shelter construction) use them, with civilians who cough, wait and cheer.

**Crimes.** Alternating with the rescues: an **armed robbery** at the Corner Mart (masked robbers hold the clerk and customers at gunpoint, a lookout watches the door, and they open fire when the Flash shows up), a **mugging at gunpoint** in an alley (arrive in time, or chase the mugger down when he runs with the bag), and a **bomb threat** on a busy corner (grab it, sprint to the harbour and drop it in deep water past the sea wall before the countdown ends). Criminals are people, not robots: every power works on them, and they are knocked out rather than destroyed.

**City.** Every block is a row of separate buildings around a service alley: brick walk-ups with fire escapes and rooftop water tanks, stone and concrete mid-rises with punched windows, glass towers downtown. Ground floors have storefronts with named signs, lit shop windows and awnings. Sidewalks carry street lamps, trees, hydrants, bins, newspaper boxes, mailboxes, benches, bus stops, parking meters, trash bags and litter; streets have crosswalks, stop lines, traffic signals, manholes and parked cars and taxis; alleys have dumpsters, graffiti, pallets and puddles. Townspeople walk the sidewalks and duck when the Flash blasts past. Landmarks with walk-in interiors: **CCPD** (an Art Deco tower after Vancouver City Hall, the show's CCPD: lobby, bullpen, a ramp up to Barry's CSI lab, a police lot out back), **CC Jitters** across the avenue (counter, espresso machine, pastry case, menu boards, tables), and the **Corner Mart**.

**Enemies.** The supplied enemy bot, rigged as a Humanoid and animated with its own walk plus shared stand and run clips. Strikers brawl, Gunners keep their distance and fire bolts, Heavies hit hard and resist knockback. Waves warp in at rotating city sites. Health regenerates when out of combat; the Speed Force meter fills with speed and pays for powers.

**Character and look.** Realistic Flash (the supplied high-detail model, rigged in Blender) using the supplied Mixamo run; 20 suits including Barry's Seasons 1 to 9 (gold boots from Season 8), Reverse-Flash, Zoom, Kid Flash, Jesse Quick, XS, Godspeed and comics suits; Speed Force lightning in each suit's colour with bloom. Barry Allen out of costume is the last two looks in the same picker: **Barry Allen** (white tee, jeans, sneakers) and **CSI Barry** (open plaid overshirt and watch on top), built on the supplied base mesh (reshaped to an athletic build, with real tee and jeans garments) with the face of the Meshy CSI design; every power and the lightning work on him too. A standing idle with arms at the sides and palms in is shared by everyone.

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

- `Assets/FlashPrototype/Scripts`: input, motor, camera, procedural visuals/world, circuit and scene coordinator. The city is `CityDistrict` (layout, waterfront, bridges), `CityBlocks` (lots, storefronts, alleys, street furniture, trash), `CityLandmarks` (CCPD, CC Jitters, Corner Mart) and `CityKit` (builds all of it into merged meshes and colliders); people are `Townsperson` and `Pedestrians`; crimes are `Crimes` with the emergencies.
- `Assets/FlashPrototype/Shaders`: referenced prototype shader included in builds.
- `Assets/FlashPrototype/Tests/Editor`: fast-crossing and movement-step tests.
- `Assets/FlashPrototype/Character`: the realistic Flash (`Realistic`), Barry Allen out of costume (`Barry`), the shared locomotion controller and clips.
- `Tools/Blender`, `Tools/csi_textures.py`: the Blender and texture pipelines that build the characters (sources in `Tools/Blender/source`).
- `Assets/Scenes/FlashPrototype.unity`: explicit entry scene; first in build settings.
- `Docs/DEVELOPMENT_LOG.md`: implementation status and decisions.

The district uses Unity primitives. The stand, walk and jump clips (and `Flash.fbx`, whose avatar they share) come from the credited HatchXR Sketchfab archive; the run is the supplied Mixamo clip. No extra animation or import package is required.

## Supplied character reference

The user-supplied `the-flash.zip` contains a posed OBJ and textures, without a skeleton or animation clips. Its single character was extracted, the missing material definitions were reconstructed, and nine textured materials were assigned. It is displayed in the Cortex suit case and, rigged, is now the playable character. To rebuild after changing the joints, run `blender -b --python Tools/Blender/rig_flash_realistic.py`, then **Flash → Configure realistic character** in Unity.

## Barry Allen out of costume

`Assets/FlashPrototype/Character/Barry/BarryAllen.fbx` is the supplied skinny base mesh (1.83 m), rigged as a Humanoid, wearing the outfit and face of the Meshy "CSI" design: open plaid overshirt with rolled sleeves, white tee, dark jeans, black-and-white sneakers, a watch and short dark hair. **Flash → Configure Barry Allen** imports it, builds its materials and the two prefabs (`BarryAllen` hides the overshirt and watch, `CSIBarry` wears the full outfit) and saves them into the scene. The batch setup (`FlashRealisticSetup.ConfigureBatch`) runs it too. To rebuild the model from its sources (Blender 5.1):

```
blender -b --python Tools/Blender/shape_body.py -- Male_07.obj Male_07_shaped.obj
blender -b --python Tools/Blender/rig_tpose_mesh.py -- Male_07_shaped.obj BarryBase.fbx 1.83
blender -b --python Tools/Blender/build_csi_barry.py -- geometry BarryBase.fbx work
python3 Tools/csi_textures.py work "Tools/Blender/source/CSI Barry reference (front).jpg"
blender -b --python Tools/Blender/build_csi_barry.py -- assemble work Assets/FlashPrototype/Character/Barry/BarryAllen.fbx
```

then copy `work/{Body,Shirt,Tee,Jeans,Hair,Shoes,Watch}.png` to `Character/Barry/Textures/Barry_<name>.png`, `work/NPC_{Body_Light,Body_Tan,Body_Dark,Body_Masked,Pants,Hair}.png` and `work/Jacket.png` to `Character/Barry/Townsperson/Townsperson_<name>.png`, and run **Flash → Configure Barry Allen** (it also builds `Resources/Townsperson.prefab`). `Male_07.obj` is inside `source/Male_07.zip` in the supplied `male-skinny-base-mesh.zip`.
