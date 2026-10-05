# Flash Game development log

## 2026-10-02 — Milestone 01: movement playground

**Status:** implemented in source; awaiting first Unity compile and gameplay test.

Confirmed direction: fresh Unity project, third-person, realistic primarily CW-inspired presentation, Barry first, manually selected speed levels, mechanics/free roam first, solo now, M2 MacBook now and gaming PC later, keyboard/mouse and Xbox controls. Enterable buildings remain part of the world plan.

This milestone adds a self-contained test scene, input actions, movement/camera, collision momentum loss, placeholder runner and district, one enterable lab, simple traffic/perception, and a repeatable circuit. Runtime geometry keeps setup to opening one scene and pressing Play. It is temporary blockout art and can be replaced independently of the motor.

Technical choices:
- CharacterController for responsive solo traversal; movement split into short collision steps.
- A dedicated input action map; no dependency on manually configured PlayerInput components.
- Player movement independent of traffic perception time; race clock uses real elapsed time and excludes explicit pause.
- Segment checks for gates so fast movement cannot simply skip a checkpoint trigger.
- Explicit shader reference on the bootstrap so builds retain the procedural material shader.
- No full-scene time-scale mutation, downloaded assets, package upgrades, or changes to the original sample scene.

Future features are not marked complete: licensed realistic model/Mixamo animations, wall and water running, phasing, combat, rescue events, progression, suits, story, timeline consequences, expansions, and co-op.

Validation limitation: this coding environment has no Unity executable, and no callable Unity Editor integration was exposed during this session. Unity import/compile, shader rendering, EditMode tests, gameplay and hardware performance must be verified before calling this milestone tested.

## 2026-10-02 — Animated Flash character

**Status:** implemented and verified in Unity Play Mode.

- Imported “The Flash Rigged (HatchXR)” by nitwit.friends from the design's backup candidate, including its supplied stand/walk/run/jumpUp/jumpDown FBXs and two textures. Source and CC BY 4.0 attribution are stored beside the assets.
- Validated the Humanoid avatar, normalized the character to 1.85 m, created URP materials and a saved prefab, and connected the prefab to the FlashPrototype scene.
- Idle/walk/run blend uses movement speed. Run playback increases up to 1.8× at high speed; jump and falling states use grounded state and vertical velocity. Root motion is disabled so existing movement remains authoritative. Pause freezes the Animator.
- Verified runtime transitions idle → run → jumpUp → jumpDown → idle and pause timing on an isolated temporary instance. Verified textured character rendering in the actual running scene. No compilation exceptions were observed during integration.
- The lightweight comic-style model is a first playable replacement, not the final realistic CW asset. Original procedural mannequin remains available when the prefab reference is empty.
- Fixed local Editor startup by backing up preferences and clearing recent-project shortcuts referencing undownloaded cloud files. This is a machine setup fix, not a game code change.
- Existing controller hardware, build and sustained performance checks remain pending.

## 2026-10-04 — Wall traversal and Xbox controls

**Status:** wall traversal implemented; isolated runtime checks passed.

- Run toward tall buildings above Normal speed to climb. Held movement carries the player onto the roof; jumping pushes outward and braking or releasing movement detaches.
- Collision-stepped movement handles roof overhangs. A reattachment cooldown prevents an immediate wall regrab after jumping.
- The animated character turns into a wall-running pose, trails follow the pose, and the camera looks upward while keeping its horizon level.
- Existing Xbox bindings feed the same movement path: left stick moves, right stick looks, RB/LB select speed, A jumps, LT brakes, RT activates perception, Y restarts the circuit, View returns to spawn, and Menu pauses/resumes.
- Isolated wall climb, rooftop transition, jump, brake release, and Normal-speed collision checks passed at simulated 20/30/60 FPS. Physical controller input is not verified; Unity detected no connected gamepad during the final check.
- Updated the design document locally with green highlights for implemented scope. Library replacement is unavailable through the current tools. Water running and rescue events remain planned.

## 2026-10-05 — Waterfront city and wired controller update

- Upgraded Input System from 1.11.2 to 1.14.2 through Unity Package Manager. Confirmed the native macOS wired Xbox layout is registered. Added USB setup/checking to the connection panel; physical hardware remains untested.
- Expanded the district to 1.56 km, with taller facades, cornices, roof equipment, trees, parks, waterfront promenade, two bridges, and an oval S.T.A.R. Labs inspired by the provided references. Selected ground-floor lobbies and the lab atrium are accessible. City meshes are combined into spatial material groups to reduce renderer overhead.
- Added sunlight, a procedural sky, shadow receiving/casting, and animated water shading. This remains a procedural prototype art pass rather than finished realistic architecture.
- Replaced broad ribbons with short amber and pale-core lightning filaments following movement history.
- Imported the supplied static Flash OBJ, removed duplicate objects and baked lightning planes, and reconstructed the omitted material definitions using the nine supplied color textures and normal maps. The model is a lab exhibit; the playable version still uses the existing animated rig.
- Validation: lab doorway traversal passed; city/lightning shader compilation passed; existing wall roof/jump/brake/collision checks passed at 20/30/60 FPS. Reference textures and lightning rendered in captured views. No sustained performance or physical USB-controller test has been completed.

## 2026-10-05 — S.T.A.R. Labs rebuilt from the reference set

- Replaced the oval S.T.A.R. Labs with `StarLabs.cs` / `StarLabsKit.cs`: a tiered disc (vertical glazing band, skylit sloped roof, glass crown with the central roof box) and three leaning cable-faced pylons, on a round plaza with lamps, trees and a monument sign. Road markings no longer cross the plaza.
- Inside, everything is walkable: the accelerator ring (the Pipeline) with 82 padded containment cells, ceiling ribs, copper main and blue guide lights; lobby; the Cortex (horseshoe desk, workbench, shelving, platforms, blue light pillars and the supplied Flash model in a suit display); Speed Lab (trusses, gallery, runway markings, treadmill, consoles, wind tunnel with spinning fans); red magnet access corridor ending in a vault door that rolls open as the player approaches; accelerator chamber (balcony, pit, lit platform steps, octagonal portal into the ring, glowing containment grids, radial fans, booths, observation deck); med bay with scanner ring; workshop; time vault with studded walls and the blue-foam alcove.
- `Prototype.shader`: interior lamp lighting inside the lab footprint (replaces the sun there, so the roof no longer shadows every room), and hazard, panel-seam, hex grating, hex tile, stud and glowing-grid surface modes. The water mode now only applies to `_Surface` 2. New Resources shaders: transparent lab glass and depth-tested sign text. Lab lamps are also real point lights so the animated character is lit indoors.
- The facade is climbable by wall running: the plinth stays below step height and the outer wall band is solid to the roof, so the crest check lands on the roof rather than an interior floor.
- Validation (scratch clone, Unity 6000.0.32f1, batch Play Mode): compile and shader compilation clean; 13 traversal probes passed (spawn → lobby, including the CityVerification entrance distance; lobby → Cortex; Cortex → corridor → vault door → pit → platform → portal → ring; Speed Lab, east wing, vault and med bay doors; facade wall run to the crown roof; full ring laps at Mach and Speed Force with no impacts). Lab build time ~0.1 s, 5.5k objects merged into material clusters. Reviewed captured views of every room. Frame rate on the M2 and physical playtesting are not yet measured.

## 2026-10-05 — Realistic Flash, Speed Force lightning, suits and GameSir support

- **Character:** the supplied posed reference model is now the playable Flash. In Blender, joints were placed on front/side renders (`Tools/Blender/flash_realistic_joints.json`), skinned through a voxel-remeshed weight proxy (bone heat fails on the open OBJ shell), and unposed to a Mixamo-named T-pose with elbow/knee hinge alignment and levelled soles. `FlashRealisticSetup` imports it as a Humanoid, builds URP Lit materials with normal maps (2K suit/face), reuses the locomotion controller and saves the scene reference. The old prefab and mannequin remain as fallbacks.
- **Lightning:** `SpeedLightning` rewritten as jagged, forking ribbon bolts in one world-space mesh, re-struck at 30 Hz along resampled travel history, with arcs between humanoid bones. `Lightning.shader` is additive HDR (white-hot core, coloured halo, fog-aware); the camera now renders post-processing with a bloom volume. Bolts fade near the camera so they never fill the screen.
- **Suits:** `FlashSkins` (13 palettes, TV and comics) and `SuitPainter`, which repaints suit textures through `SuitRecolor.shader` (hue-classified red/gold/white regions, shading preserved, optional face cover) into per-character material copies. Only the worn suit's textures stay in memory (~1 ms per change). Pause menu picker; choice saved in PlayerPrefs. Lightning colour follows the suit.
- **GameSir G7 Pro:** macOS exposes it as a generic HID game pad (USB 3537:1022, 9-byte report), so it was a Joystick that no Gamepad binding reached. `GameSirGamepad` registers a layout decoded from its HID report descriptor, with button order from SDL's GameControllerDB. Unity now creates it as a Gamepad and makes it `Gamepad.current`; idle sticks, triggers and D-pad read neutral. Button presses still need a physical check through the pause-menu live input panel.
- Fixed in passing: the realistic setup saves the scene only after its temporary build object is destroyed.
- Validation (scratch clone, Unity 6000.0.32f1, batch Play Mode): clean compile; 13 S.T.A.R. Labs traversal probes and the 20/30/60 FPS wall-run checks pass with the new character; rendered every suit and four lightning colours in game. Sustained M2 frame rate is still unmeasured.

## 2026-10-05 — Powers, bot enemies, emergencies, Season 1–9 suits

- **Suits:** Barry's CW suits for Seasons 1–9 (burgundy leather S1–2, white emblem S3, gold seams S4, fabric S5, bright red S6–9, silver accents S7 per the reference image, metallic gold boots S8–9), plus the earlier speedsters and comics suits: 20 in all. The rig script splits the boots into their own material (`Mat.10`, cut at the gold cuff tops) so suits colour boots and seams separately. Season 4 is the default because the textures were painted for it.
- **Animation:** the supplied Mixamo "Running" clip replaces the run in the locomotion blend. It is a quick 0.23 s cycle, so `RunnerVisual.RunRate` slows it to a human cadence at 7 m/s and speeds it up through the tiers.
- **Enemy bot:** imported from the supplied archive. Its head and neck carry no skin weights, so Unity's auto-mapper rejected the avatar; `EnemyBotSetup` maps the HumanIK bones explicitly. Metallic and roughness maps are packed into one URP metallic/smoothness texture. `BotEnemy` (Striker, Gunner, Heavy) steers with whisker avoidance and supports stun, slow, knockback, launch and vortex capture. `BotDirector` runs waves at six rotating sites, projectiles (bot bolts, the Flash's homing chained lightning) and afterimage decoys.
- **Powers:** `SpeedsterMotor` gains boost, air dash, speed jump, drift, instant stop, water running (`WaterZones`, sea-wall gaps), ceiling running (tunnel on the training straight; wall runs turn into ceiling runs under an overhang) and speed climb. `SpeedForcePowers` implements the combat and environmental powers; `Vortex` is the shared tornado, cyclone and whirlpool; `EmergencyDirector` runs four rotating emergencies with civilians. `FxSystem` and `LightningBuilder` provide sparks, flames, smoke, gas, dust, spray, wind, debris, shockwaves, transient bolts and afterimages (three new Resources shaders). New HUD: health, Speed Force and Infinite Mass meters, selected special or context action, objectives, bot health bars.
- **Controls:** the circuit moved from Y to D-pad up (confirmed with the user) so Y can fire specials.
- Validation (scratch clone, Unity 6000.0.32f1, batch Play Mode): clean compile; 34 power checks (every movement power, every attack and special, tornado, cyclone and water vortex, bot melee and bolts, waves, all four emergencies resolved), the 13 S.T.A.R. Labs traversal probes and the 20/30/60 FPS wall-run checks pass. A virtual gamepad pressing real buttons across real game frames passed 10 controller checks. Rendered every power and emergency. Not yet measured: sustained frame rate on the M2 with a full wave and an emergency running.
