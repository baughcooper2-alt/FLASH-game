# First Editor verification

Not yet executed. Record results and errors rather than assuming a pass.

1. Open with Unity 6000.0.32f1. Wait for package import. Console should have no red script or shader errors.
2. Run EditMode tests. Expected: seven test cases pass, including crossing an entire gate between frames and rejecting near misses.
3. Open FlashPrototype, press Play. Expected: one player, one camera, visible world and HUD, no magenta materials.
4. WASD + mouse: walk forwards/backwards/sideways, orbit the camera. Try 30 and 60 FPS limits while testing movement consistency.
5. Tap E three times: normal → super → Mach → Speed Force. Additional taps stay at level four. Q stops at normal. No stamina depletion.
6. Hold Shift at full speed: stop. Turn at low and high speeds: high speed has a wider radius. Run head-on at building corners at all levels: never pass through a wall. Test at a low frame rate too.
7. Jump, hit a ceiling, land, walk down sidewalk edges. No double-jumping in midair, falling through ground, or sticking permanently against walls.
8. Enter the lab via the doorway at approximately (-55, -69). Camera retracts against walls and roof. Leave again.
9. Hold F near traffic on x=130: cars slow, player remains responsive. Release: traffic returns to normal.
10. T starts circuit. Follow all nine gates. The timer uses real time even during perception. Finish stores best time. Restart, pause, reset and leave the district: no false finish or gate awarded for a teleport.
11. Esc pauses player, cars and race timer; resume does not jump the character or count paused time. Switch away from Unity: simulation pauses. Disable trails/FOV in the menu.
12. Pair Xbox controller. Repeat move/look/speed/jump/brake/perception/race/respawn/pause. Disconnect and reconnect; keyboard still works and no repeating Console exceptions occur.
13. Exit and re-enter Play Mode twice. No duplicate input handlers, cameras or permanent cursor capture. Best time survives.
14. Build for macOS if desired. Check the shader and procedural primitive meshes are included and the first scene starts correctly.
15. On the M2, record Game view resolution, FPS, renderer count and any spikes. A 60 FPS target is a cap, not a measured result.

Known prototype limits: blockout art; cars follow simple loops; no vehicle pushing or damage; no wall/water running; perception affects traffic only; running animation is procedural; settings toggles use a mouse; map boundaries return the player to spawn.
