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
