using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FlashGame
{
    // Explicit scene component: never injects gameplay into other scenes.
    public sealed class FlashPrototype : MonoBehaviour
    {
        [SerializeField] Shader prototypeShader;
        [SerializeField] GameObject characterPrefab;
        // Barry Allen out of costume (BarryAllen, CSIBarry); set by Flash > Configure Barry Allen.
        [SerializeField] GameObject[] civilianPrefabs;
        RunnerInput input;
        PrototypeWorld world;
        SpeedsterMotor runner;
        RunnerVisual visual;
        RunnerCamera followCamera;
        CheckpointCircuit circuit;
        Camera view;
        VolumeProfile postProfile;
        FxSystem fx;
        BotDirector bots;
        EmergencyDirector emergencies;
        SpeedForcePowers powers;
        float downTimer, noticeTimer;
        string notice;
        bool paused, perception, trailsEnabled = true, showHelp = true;
        bool controllerPanel;
        Vector2 controllerScroll;
        float fps, oldShadowDistance;
        int oldTargetFrameRate, oldVSync;
        GUIStyle title, body, small, speedStyle, labelStyle, suitStyle, objectiveStyle;
        readonly Vector3 spawn = new Vector3(-390, 0.12f, -410);
        void Start()
        {
            if (prototypeShader == null) { Debug.LogError("FlashPrototype needs its prototype shader assigned."); enabled = false; return; }
            oldTargetFrameRate = Application.targetFrameRate;
            oldVSync = QualitySettings.vSyncCount;
            oldShadowDistance = QualitySettings.shadowDistance;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            QualitySettings.shadowDistance = 65;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.55f,.64f,.75f);
            RenderSettings.ambientEquatorColor = new Color(.35f,.39f,.43f);
            RenderSettings.ambientGroundColor = new Color(.22f,.2f,.17f);
            RenderSettings.skybox = Resources.Load<Material>("CitySky");
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.32f, 0.42f, 0.52f);
            RenderSettings.fogStartDistance = 650;
            RenderSettings.fogEndDistance = 2600;
            var environment = new GameObject("Central City training district");
            environment.transform.SetParent(transform, false);
            var sunObject=new GameObject("Afternoon sunlight");sunObject.transform.SetParent(environment.transform,false);
            sunObject.transform.rotation=Quaternion.Euler(38,-35,0);
            var sun=sunObject.AddComponent<Light>();sun.type=LightType.Directional;
            sun.color=new Color(1,.91f,.78f);sun.intensity=1.5f;sun.shadows=LightShadows.Hard;RenderSettings.sun=sun;
            world = new PrototypeWorld(environment.transform, prototypeShader);
            world.Build();
            var player = new GameObject("Barry - placeholder runner");
            player.transform.SetParent(transform, false);
            player.layer = 2; // Camera casts ignore the player.
            runner = player.AddComponent<SpeedsterMotor>();
            runner.Respawn(spawn);
            visual = player.AddComponent<RunnerVisual>();
            visual.Build(world, characterPrefab, civilianPrefabs);
            var cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(transform, false);
            cameraObject.tag = "MainCamera";
            view = cameraObject.AddComponent<Camera>();
            view.clearFlags = CameraClearFlags.Skybox;
            view.backgroundColor = RenderSettings.fogColor;
            // HDR bloom makes the Speed Force lightning and lab lights glow; the other default effects are neutral.
            view.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            postProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = postProfile.Add<Bloom>(true);
            bloom.threshold.Override(1.05f); bloom.intensity.Override(1.1f); bloom.scatter.Override(.62f);
            var volume = environment.AddComponent<Volume>();
            volume.isGlobal = true; volume.priority = 10; volume.sharedProfile = postProfile;
            cameraObject.AddComponent<AudioListener>();
            followCamera = cameraObject.AddComponent<RunnerCamera>();
            followCamera.Follow(runner, 0.016f, true);
            circuit = new CheckpointCircuit(world, transform);
            // Powers, bots and emergencies.
            fx = FxSystem.Create(transform, Resources.Load<Material>("SpeedLightning"), world.Material("Debris", new Color(.62f, .63f, .66f)));
            var vortices = new GameObject("Vortices").transform; vortices.SetParent(transform, false);
            powers = player.AddComponent<SpeedForcePowers>();
            bots = BotDirector.Create(transform, runner, powers.Vitals, fx);
            emergencies = EmergencyDirector.Create(transform, world, fx);
            powers.Init(runner, visual, followCamera, fx, bots, emergencies, vortices);
            powers.Downed += () => { downTimer = 2.5f; Notice("DOWN \u2022 returning to S.T.A.R. Labs", 2.5f); };
            runner.Events += (e, at) => { if (e == MotorEvent.Sank) Sank(); };
            input = new RunnerInput();
            SetPaused(false);
        }
        void Update()
        {
            if (input == null) return;
            if (input.Pause.WasPressedThisFrame()) SetPaused(!paused);
            fps = Mathf.Lerp(fps, 1f / Mathf.Max(0.001f, Time.unscaledDeltaTime), 0.06f);
            if (paused)
            {
                // Look picker: the speed buttons (Q / E, LB / RB) or the D-pad cycle suits and Barry's outfits while paused.
                var pad = Gamepad.current;
                if (!controllerPanel && (input.Faster.WasPressedThisFrame() || pad != null && pad.dpad.right.wasPressedThisFrame)) ChangeSuit(1);
                if (!controllerPanel && (input.Slower.WasPressedThisFrame() || pad != null && pad.dpad.left.wasPressedThisFrame)) ChangeSuit(-1);
                return;
            }
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            followCamera.ReadLook(input.Look(dt));
            if (input.Reset.WasPressedThisFrame()) { circuit.Cancel(); ReturnTo(spawn); }
            if (input.Race.WasPressedThisFrame()) { ReturnTo(circuit.StartPosition); circuit.Start(); }
            perception = input.Perception.IsPressed();
            float worldScale = perception ? 0.12f : 1;
            world.TickTraffic(dt * worldScale);
            // Moving traffic colliders must be synchronized before controller casts.
            Physics.SyncTransforms();
            if (powers.Vitals.Down) runner.Locked = true;
            runner.Tick(input, followCamera.Yaw, dt);
            powers.Tick(input, dt, worldScale);
            bots.Tick(dt, worldScale);
            emergencies.Tick(dt, worldScale);
            fx.SetWorldSpeed(worldScale);
            noticeTimer = Mathf.Max(0, noticeTimer - dt);
            if (downTimer > 0 && (downTimer -= dt) <= 0)
            { powers.Vitals.Restore(); circuit.Cancel(); ReturnTo(spawn); }
            Vector3 p = runner.transform.position;
            if (p.y < -25 || p.x < -850 || p.x > 1510 || p.z > 850 || p.z < -1500)
            { circuit.Cancel(); ReturnTo(spawn); }
            circuit.Tick(runner.PreviousPosition, runner.transform.position, Time.unscaledDeltaTime);
            visual.Tick(runner.Speed, runner.Grounded, dt, trailsEnabled, runner.VerticalSpeed);
        }
        void LateUpdate()
        {
            if (runner != null && !paused) followCamera.Follow(runner, Mathf.Min(Time.unscaledDeltaTime, 0.05f));
        }
        void Notice(string text, float seconds = 3) { notice = text; noticeTimer = seconds; }
        // Running too slowly over water: back to the promenade, the nearest shore.
        void Sank()
        {
            Vector3 p = runner.transform.position;
            ReturnTo(new Vector3(758, .62f, Mathf.Clamp(p.z, -740, 740)));
            Notice("TOO SLOW \u2022 you sank. Water running needs 24 m/s (Super Speed).", 3.5f);
        }
        void ReturnTo(Vector3 position)
        {
            powers.ResetState();
            runner.Respawn(position); visual.ClearTrails(); followCamera.ResetView();
            followCamera.Follow(runner, 0.016f, true);
        }
        void ChangeSuit(int step)
        {
            visual.SetSkin(visual.Skin + step);
            FlashSkins.Saved = visual.Skin;
        }
        void SetPaused(bool value)
        {
            paused = value;
            if (!value) controllerPanel = false;
            if (visual != null) visual.SetPaused(value);
            if (bots != null) bots.SetPaused(value);
            if (fx != null) fx.SetWorldSpeed(value ? 0 : 1);
            if (paused) perception = false;
            Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = paused;
        }
        void OnApplicationFocus(bool focused) { if (!focused && input != null) SetPaused(true); }
        void OnDestroy()
        {
            input?.Dispose();
            world?.Dispose();
            if (postProfile != null) Destroy(postProfile);
            if (input != null)
            {
                Application.targetFrameRate = oldTargetFrameRate;
                QualitySettings.vSyncCount = oldVSync;
                QualitySettings.shadowDistance = oldShadowDistance;
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            }
        }
        void InitStyles()
        {
            title = new GUIStyle(GUI.skin.label) { fontSize = 23, fontStyle = FontStyle.Bold };
            title.normal.textColor = new Color(1, 0.75f, 0.25f);
            body = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            body.normal.textColor = new Color(0.9f, 0.93f, 0.96f);
            small = new GUIStyle(body) { fontSize = 12 };
            speedStyle = new GUIStyle(title) { fontSize = 48 };
            labelStyle = new GUIStyle(body) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
            suitStyle = new GUIStyle(body) { fontSize = 15, alignment = TextAnchor.MiddleCenter, richText = true };
            objectiveStyle = new GUIStyle(body) { fontSize = 13, richText = true };
        }
        static void Panel(Rect rect)
        {
            Color previous = GUI.color;
            GUI.color = new Color(0.025f, 0.045f, 0.075f, 0.94f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
        void OnGUI()
        {
            if (runner == null || circuit == null) return;
            if (title == null) InitStyles();
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
            float width = Screen.width / scale, height = Screen.height / scale;
            Panel(new Rect(24, 24, 330, 95));
            GUI.Label(new Rect(40, 33, 300, 32), "THE FLASH / CENTRAL CITY", title);
            GUI.Label(new Rect(40, 68, 300, 40), "S.T.A.R. Labs • Waterfront district\n" + Mathf.RoundToInt(fps) + " FPS   |   " + (Gamepad.current != null ? Gamepad.current.displayName : "Keyboard + mouse"), small);
            Panel(new Rect(24, height - 165, 295, 140));
            GUI.Label(new Rect(40, height - 156, 270, 28), SpeedsterMotor.TierNames[runner.Tier], title);
            GUI.Label(new Rect(40, height - 126, 190, 65), Mathf.RoundToInt(runner.Speed * 2.23694f).ToString("000"), speedStyle);
            GUI.Label(new Rect(200, height - 96, 95, 30), "MPH", body);
            GUI.Label(new Rect(40, height - 56, 260, 25), "LEVEL " + (runner.Tier + 1) + " / 4   •   " + (runner.DistanceTravelled / 1000f).ToString("F2") + " km", small);
            DrawVitals(height);
            string status = perception ? "SPEED PERCEPTION • World slowed" : "FREE ROAM • T / D-pad up starts the circuit";
            if (runner.Boosting) status = "SPEED BOOST";
            if (runner.Drifting) status = "SPEED DRIFT";
            if (runner.OnWater) status = "WATER RUNNING • Keep above 24 m/s";
            if (runner.CeilingRunning) status = "CEILING RUN • Space / A or brake to drop";
            if (runner.WallRunning) status = runner.Cresting ? "ROOFTOP • Cresting the ledge" : "WALL RUN • Space / A to jump off • boost to speed-climb";
            if (runner.ImpactTimer > 0) status = "IMPACT • Brake before tight corners";
            if (powers.Feedback != null) status = powers.Feedback;
            if (noticeTimer > 0) status = notice;
            Panel(new Rect(width / 2 - 260, 24, 520, 45));
            GUI.Label(new Rect(width / 2 - 250, 32, 500, 30), status, labelStyle);
            float right = 24;
            if (circuit.Active)
            {
                Panel(new Rect(width - 310, right, 286, 135));
                GUI.Label(new Rect(width - 292, right + 13, 258, 32), "CITY CIRCUIT", title);
                GUI.Label(new Rect(width - 292, right + 50, 258, 70), circuit.Elapsed.ToString("F2") + " s\nCheckpoint " + (circuit.Next + 1) + " / " + circuit.Gates.Length + "\n" + Mathf.RoundToInt(Vector3.Distance(runner.transform.position, circuit.Gates[circuit.Next])) + " m to next gate", body);
                DrawWorldLabel(circuit.Gates[circuit.Next] + Vector3.up * 7, "NEXT CHECKPOINT", width, height, scale, true);
                right += 145;
            }
            else if (circuit.Finished)
            {
                Panel(new Rect(width - 310, right, 286, 135));
                GUI.Label(new Rect(width - 292, right + 11, 260, 35), "CIRCUIT COMPLETE", title);
                GUI.Label(new Rect(width - 292, right + 52, 260, 70), "Time " + circuit.LastTime.ToString("F2") + " s  /  Best " + circuit.BestTime.ToString("F2") + " s\nT / D-pad up to try again", body);
                right += 145;
            }
            DrawObjectives(width, height, scale, right);
            DrawBotBars(width, height, scale);
            foreach (var label in world.Labels)
                if (Vector3.Distance(runner.transform.position, label.Position) < 160)
                    DrawWorldLabel(label.Position, label.Text, width, height, scale, false);
            DrawPowerBar(width, height);
            if (showHelp && !paused)
            {
                Panel(new Rect(width - 470, height - 214, 446, 190));
                GUI.Label(new Rect(width - 456, height - 205, 430, 180),
                    "WASD / L-stick  Move            Mouse / R-stick  Camera\n" +
                    "E, Q / RB, LB    Speed tier       Space / A   Jump (speed jump)\n" +
                    "L-Ctrl / L3       Boost, air dash   Shift / LT   Tap: stop  Hold+steer: drift\n" +
                    "LMB / X           Punch (hold: rapid)   RMB / B   Lightning (hold: slam)\n" +
                    "V / R3             Afterimages      G / Y       Special or context\n" +
                    "Z, X / D-pad     Pick special      F / RT       Speed perception\n" +
                    "R / View          Return to lab    Esc / Menu  Pause, suits, Barry\n" +
                    "Run circles: tornado  •  Over water above 24 m/s  •  Jump into ceilings", small);
            }
            var flash = fx.FlashAmount;
            if (flash > 0) Overlay(width, height, new Color(fx.FlashColor.r, fx.FlashColor.g, fx.FlashColor.b, flash * .85f));
            if (powers.Vitals.HurtFlash > 0) Overlay(width, height, new Color(.8f, 0, 0, powers.Vitals.HurtFlash * .22f));
            if (paused) DrawPause(width, height);
            GUI.matrix = previousMatrix;
        }
        static void Overlay(float width, float height, Color color)
        {
            Color previous = GUI.color; GUI.color = color;
            GUI.DrawTexture(new Rect(0, 0, width, height), Texture2D.whiteTexture);
            GUI.color = previous;
        }
        static void Bar(Rect rect, float fill, Color color)
        {
            Color previous = GUI.color;
            GUI.color = new Color(1, 1, 1, .12f); GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = color; GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fill), rect.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }
        void DrawVitals(float height)
        {
            var v = powers.Vitals;
            Panel(new Rect(24, height - 243, 295, 70));
            GUI.Label(new Rect(40, height - 238, 120, 18), "HEALTH", small);
            Bar(new Rect(130, height - 234, 172, 10), v.Health / RunnerVitals.MaxHealth, new Color(.9f, .2f, .18f));
            GUI.Label(new Rect(40, height - 220, 120, 18), "SPEED FORCE", small);
            Bar(new Rect(130, height - 216, 172, 10), v.Energy / RunnerVitals.MaxEnergy, visual.LightningGlow);
            GUI.Label(new Rect(40, height - 202, 120, 18), powers.InfiniteMassReady ? "INFINITE MASS!" : "INFINITE MASS", small);
            Bar(new Rect(130, height - 198, 172, 10), powers.InfiniteMassCharge, powers.InfiniteMassReady ? Color.white : new Color(.85f, .85f, .9f, .6f));
        }
        void DrawPowerBar(float width, float height)
        {
            var (_, name, hint, cost) = powers.SelectedPower;
            Panel(new Rect(width / 2 - 250, height - 78, 500, 54));
            if (powers.Context != null)
            {
                GUI.Label(new Rect(width / 2 - 240, height - 74, 480, 24), "G / Y  \u2022  " + powers.Context.Label, labelStyle);
                GUI.Label(new Rect(width / 2 - 240, height - 50, 480, 22), "Special: " + name + " (Z, X / D-pad to change)", labelStyle);
                return;
            }
            string fists = powers.LightningFists ? "   \u2022  FISTS CHARGED" : "";
            GUI.Label(new Rect(width / 2 - 240, height - 74, 480, 24), "\u25C4  SPECIAL: " + name.ToUpper() + "  (" + Mathf.RoundToInt(cost) + ")  \u25BA" + fists, labelStyle);
            GUI.Label(new Rect(width / 2 - 240, height - 50, 480, 22), hint + "  \u2022  G / Y", labelStyle);
        }
        void DrawObjectives(float width, float height, float scale, float top)
        {
            float h = 0;
            string text = "";
            if (bots.WaveActive)
            {
                text += "<b>BOT INCURSION \u2022 WAVE " + bots.Wave + "</b>\n" + bots.SiteName + "  \u2022  " + bots.Remaining + " bots  \u2022  "
                    + Mathf.RoundToInt(Vector3.Distance(runner.transform.position, bots.SiteCentre)) + " m\n";
                h += 46;
                DrawWorldLabel(bots.SiteCentre + Vector3.up * 8, "BOT INCURSION", width, height, scale, true);
            }
            else if (bots.NextWaveIn > 0) { text += "Next bot incursion in " + Mathf.CeilToInt(bots.NextWaveIn) + " s\n"; h += 24; }
            var e = emergencies.Active;
            if (e != null)
            {
                text += "<b>EMERGENCY \u2022 " + e.Title.ToUpper() + "</b>\n" + e.Progress + "  \u2022  " + Mathf.RoundToInt(Vector3.Distance(runner.transform.position, e.Location)) + " m\n<size=11>" + e.Instructions + "</size>\n";
                h += 92;
                DrawWorldLabel(e.Location + Vector3.up * 9, "EMERGENCY", width, height, scale, true);
            }
            if (emergencies.Banner != null) { text += "<b>" + emergencies.Banner + "</b>\n"; h += 24; }
            text += "Bots destroyed " + bots.Destroyed + "  \u2022  Civilians safe " + emergencies.Saved;
            h += 24;
            Panel(new Rect(width - 330, top, 306, h + 16));
            GUI.Label(new Rect(width - 316, top + 8, 284, h), text, objectiveStyle);
        }
        void DrawBotBars(float width, float height, float scale)
        {
            foreach (var bot in BotEnemy.All)
            {
                if (bot.Dead || Vector3.Distance(bot.transform.position, runner.transform.position) > 60) continue;
                Vector3 screen = view.WorldToScreenPoint(bot.transform.position + Vector3.up * 2.4f * bot.transform.localScale.y);
                if (screen.z <= 0) continue;
                float x = screen.x / scale, y = height - screen.y / scale;
                Bar(new Rect(x - 22, y, 44, 5), bot.Health / bot.MaxHealth, bot.Kind == BotKind.Heavy ? new Color(.85f, .2f, 1) : bot.Kind == BotKind.Gunner ? new Color(1, .55f, .1f) : new Color(1, .2f, .15f));
                if (bot.StateLabel.Length > 0) GUI.Label(new Rect(x - 60, y - 18, 120, 16), bot.StateLabel, labelStyle);
            }
        }
        void DrawWorldLabel(Vector3 position, string text, float width, float height, float scale, bool clamp)
        {
            Vector3 screen = view.WorldToScreenPoint(position);
            if (screen.z <= 0)
            {
                if (!clamp) return;
                if (text == "NEXT CHECKPOINT") GUI.Label(new Rect(width / 2 - 140, 90, 280, 40), "Turn around for the next gate", labelStyle);
                return;
            }
            float x = screen.x / scale, y = height - screen.y / scale;
            if (clamp) { x = Mathf.Clamp(x, 145, width - 145); y = Mathf.Clamp(y, 100, height - 220); }
            GUI.Label(new Rect(x - 140, y - 22, 280, 48), text, labelStyle);
        }
        void DrawPause(float width, float height)
        {
            if (controllerPanel)
            {
                DrawControllerPanel(width, height);
                return;
            }
            Rect box = new Rect(width / 2 - 230, height / 2 - 290, 460, 580);
            Panel(box);
            GUILayout.BeginArea(new Rect(box.x + 25, box.y + 20, 410, 540));
            GUILayout.Label("S.T.A.R. LABS / PAUSED", title);
            GUILayout.Space(10);
            GUILayout.Label("Explore, fight bot incursions, handle emergencies, or run the city circuit.", body);
            GUILayout.Space(10);
            followCamera.DynamicFov = GUILayout.Toggle(followCamera.DynamicFov, "  Speed-based field of view");
            trailsEnabled = GUILayout.Toggle(trailsEnabled, "  Lightning trails");
            showHelp = GUILayout.Toggle(showHelp, "  Show controls");
            GUILayout.Space(12);
            var skin = FlashSkins.All[visual.Skin];
            GUILayout.Label((skin.Character != null ? "BARRY  " : "SUIT  ") + (visual.Skin + 1) + " / " + FlashSkins.All.Length, small);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("\u25C4", GUILayout.Width(44), GUILayout.Height(48))) ChangeSuit(-1);
            GUILayout.Label("<b>" + skin.Name + "</b>\n" + skin.Source, suitStyle, GUILayout.Height(48));
            if (GUILayout.Button("\u25BA", GUILayout.Width(44), GUILayout.Height(48))) ChangeSuit(1);
            GUILayout.EndHorizontal();
            GUILayout.Label("Q / E, LB / RB or D-pad change suits (and Barry out of costume) while paused", small);
            GUILayout.Space(12);
            if (GUILayout.Button("Connect / test controller", GUILayout.Height(36))) controllerPanel = true;
            if (GUILayout.Button("Resume (Esc / Menu)", GUILayout.Height(36))) SetPaused(false);
            if (GUILayout.Button("Start / restart circuit (T / D-pad up)", GUILayout.Height(36)))
            { ReturnTo(circuit.StartPosition); circuit.Start(); SetPaused(false); }
            if (GUILayout.Button("Return to lab (R / View)", GUILayout.Height(36)))
            { circuit.Cancel(); ReturnTo(spawn); SetPaused(false); }
            GUILayout.Label("Best circuit: " + (circuit.BestTime > 0 ? circuit.BestTime.ToString("F2") + " s" : "No completed run yet"), small);
            GUILayout.EndArea();
        }
        void DrawControllerPanel(float width, float height)
        {
            Rect box = new Rect(width / 2 - 300, height / 2 - 320, 600, 640);
            Panel(box);
            GUILayout.BeginArea(new Rect(box.x + 24, box.y + 20, 552, 600));
            controllerScroll = GUILayout.BeginScrollView(controllerScroll);
            GUILayout.Label("CONNECT YOUR CONTROLLER", title);
            var pad = ControllerConnection.ConnectedPad;
            GUILayout.Label(pad != null ? "Connected: " + pad.displayName : "No controller detected", body);
            GUILayout.Space(12);
            GUILayout.Label("WIRED (Xbox, GameSir G7 Pro): connect the controller directly with a USB data cable (a charge-only cable will not work). No pairing is needed.\nWIRELESS (Xbox): turn it on, hold Pair until the Xbox light flashes, then select it in Bluetooth settings.\nReturn to the Game view and move a stick or press A.", body);
            GUILayout.Space(10);
            if (GUILayout.Button("Open Bluetooth settings (wireless)", GUILayout.Height(32))) ControllerConnection.OpenBluetoothSettings();
            if (GUILayout.Button("Check wired controller", GUILayout.Height(32))) ControllerConnection.CheckWired();
            GUILayout.Label(ControllerConnection.DeviceStatus, small);
            GUILayout.Space(12);
            GUILayout.Label("LIVE INPUT CHECK", title);
            if (pad == null)
                GUILayout.Label("Waiting for a controller…\nIf it is already paired, reconnect it and click the Game view.", body, GUILayout.Height(80));
            else
                GUILayout.Label(ControllerConnection.InputSummary(pad), body, GUILayout.Height(80));
            GUILayout.Space(8);
            GUILayout.Label("Left stick: move  •  Right stick: look  •  RB / LB: speed  •  A: jump\nX: punch  •  B: lightning  •  Y: special  •  L3: boost  •  R3: afterimages\nLT: brake / drift  •  RT: perception  •  D-pad: pick special, up = circuit\nView: return to lab  •  Menu: resume", body);
            GUILayout.Space(12);
            if (GUILayout.Button("Back to pause menu", GUILayout.Height(36))) controllerPanel = false;
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
