using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace FlashGame
{
    // Explicit scene component: never injects gameplay into other scenes.
    public sealed class FlashPrototype : MonoBehaviour
    {
        [SerializeField] Shader prototypeShader;
        RunnerInput input;
        PrototypeWorld world;
        SpeedsterMotor runner;
        RunnerVisual visual;
        RunnerCamera followCamera;
        CheckpointCircuit circuit;
        Camera view;
        bool paused, perception, trailsEnabled = true, showHelp = true;
        float fps, oldShadowDistance;
        int oldTargetFrameRate, oldVSync;
        GUIStyle title, body, small, speedStyle, labelStyle;
        readonly Vector3 spawn = new Vector3(0, 0.12f, -90);
        void Start()
        {
            if (prototypeShader == null) { Debug.LogError("FlashPrototype needs its prototype shader assigned."); enabled = false; return; }
            oldTargetFrameRate = Application.targetFrameRate;
            oldVSync = QualitySettings.vSyncCount;
            oldShadowDistance = QualitySettings.shadowDistance;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            QualitySettings.shadowDistance = 65;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.6f, 0.68f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.32f, 0.42f, 0.52f);
            RenderSettings.fogStartDistance = 220;
            RenderSettings.fogEndDistance = 950;
            var environment = new GameObject("Central City training district");
            environment.transform.SetParent(transform, false);
            world = new PrototypeWorld(environment.transform, prototypeShader);
            world.Build();
            var player = new GameObject("Barry - placeholder runner");
            player.transform.SetParent(transform, false);
            player.layer = 2; // Camera casts ignore the player.
            runner = player.AddComponent<SpeedsterMotor>();
            runner.Respawn(spawn);
            visual = player.AddComponent<RunnerVisual>();
            visual.Build(world);
            var cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(transform, false);
            cameraObject.tag = "MainCamera";
            view = cameraObject.AddComponent<Camera>();
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = RenderSettings.fogColor;
            cameraObject.AddComponent<AudioListener>();
            followCamera = cameraObject.AddComponent<RunnerCamera>();
            followCamera.Follow(runner, 0.016f, true);
            circuit = new CheckpointCircuit(world, transform);
            input = new RunnerInput();
            SetPaused(false);
        }
        void Update()
        {
            if (input == null) return;
            if (input.Pause.WasPressedThisFrame()) SetPaused(!paused);
            fps = Mathf.Lerp(fps, 1f / Mathf.Max(0.001f, Time.unscaledDeltaTime), 0.06f);
            if (paused) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            followCamera.ReadLook(input.Look(dt));
            if (input.Reset.WasPressedThisFrame()) { circuit.Cancel(); ReturnTo(spawn); }
            if (input.Race.WasPressedThisFrame()) { ReturnTo(circuit.StartPosition); circuit.Start(); }
            perception = input.Perception.IsPressed();
            world.TickTraffic(dt * (perception ? 0.12f : 1));
            // Moving traffic colliders must be synchronized before controller casts.
            Physics.SyncTransforms();
            runner.Tick(input, followCamera.Yaw, dt);
            Vector3 p = runner.transform.position;
            if (p.y < -25 || Mathf.Abs(p.x) > 338 || p.z > 338 || p.z < -990)
            { circuit.Cancel(); ReturnTo(spawn); }
            circuit.Tick(runner.PreviousPosition, runner.transform.position, Time.unscaledDeltaTime);
            visual.Tick(runner.Speed, runner.Grounded, dt, trailsEnabled);
        }
        void LateUpdate()
        {
            if (runner != null && !paused) followCamera.Follow(runner, Mathf.Min(Time.unscaledDeltaTime, 0.05f));
        }
        void ReturnTo(Vector3 position)
        {
            runner.Respawn(position); visual.ClearTrails(); followCamera.ResetView();
            followCamera.Follow(runner, 0.016f, true);
        }
        void SetPaused(bool value)
        {
            paused = value;
            if (paused) perception = false;
            Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = paused;
        }
        void OnApplicationFocus(bool focused) { if (!focused && input != null) SetPaused(true); }
        void OnDestroy()
        {
            input?.Dispose();
            world?.Dispose();
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
            GUI.Label(new Rect(40, 33, 300, 32), "THE FLASH / PROTOTYPE 01", title);
            GUI.Label(new Rect(40, 68, 300, 40), "Central City • Movement playground\n" + Mathf.RoundToInt(fps) + " FPS   |   " + (Gamepad.current != null ? "Controller connected" : "Keyboard + mouse"), small);
            Panel(new Rect(24, height - 165, 295, 140));
            GUI.Label(new Rect(40, height - 156, 270, 28), SpeedsterMotor.TierNames[runner.Tier], title);
            GUI.Label(new Rect(40, height - 126, 190, 65), Mathf.RoundToInt(runner.Speed * 2.23694f).ToString("000"), speedStyle);
            GUI.Label(new Rect(200, height - 96, 95, 30), "MPH", body);
            GUI.Label(new Rect(40, height - 56, 260, 25), "LEVEL " + (runner.Tier + 1) + " / 4   •   " + (runner.DistanceTravelled / 1000f).ToString("F2") + " km", small);
            string status = perception ? "SPEED PERCEPTION • Traffic slowed" : "FREE ROAM • T / Y starts the circuit";
            if (runner.ImpactTimer > 0) status = "IMPACT • Brake before tight corners";
            Panel(new Rect(width / 2 - 235, 24, 470, 45));
            GUI.Label(new Rect(width / 2 - 225, 32, 450, 30), status, labelStyle);
            if (circuit.Active)
            {
                Panel(new Rect(width - 310, 24, 286, 135));
                GUI.Label(new Rect(width - 292, 37, 258, 32), "CITY CIRCUIT", title);
                GUI.Label(new Rect(width - 292, 74, 258, 70), circuit.Elapsed.ToString("F2") + " s\nCheckpoint " + (circuit.Next + 1) + " / " + circuit.Gates.Length + "\n" + Mathf.RoundToInt(Vector3.Distance(runner.transform.position, circuit.Gates[circuit.Next])) + " m to next gate", body);
                DrawWorldLabel(circuit.Gates[circuit.Next] + Vector3.up * 7, "NEXT CHECKPOINT", width, height, scale, true);
            }
            else if (circuit.Finished)
            {
                Panel(new Rect(width - 310, 24, 286, 135));
                GUI.Label(new Rect(width - 292, 35, 260, 35), "CIRCUIT COMPLETE", title);
                GUI.Label(new Rect(width - 292, 76, 260, 70), "Time " + circuit.LastTime.ToString("F2") + " s  /  Best " + circuit.BestTime.ToString("F2") + " s\nT / Y to try again", body);
            }
            foreach (var label in world.Labels)
                if (Vector3.Distance(runner.transform.position, label.Position) < 160)
                    DrawWorldLabel(label.Position, label.Text, width, height, scale, false);
            if (showHelp && !paused)
            {
                Panel(new Rect(width - 370, height - 200, 346, 176));
                GUI.Label(new Rect(width - 354, height - 187, 320, 160),
                    "WASD / left stick     Move\nMouse / right stick   Camera\nE, Q / RB, LB             Speed level\nSpace / A                  Jump\nShift / LT                   Brake\nHold F / RT                Speed perception\nR / View                     Return to lab\nEsc / Menu                Pause & settings", small);
            }
            if (paused) DrawPause(width, height);
            GUI.matrix = previousMatrix;
        }
        void DrawWorldLabel(Vector3 position, string text, float width, float height, float scale, bool clamp)
        {
            Vector3 screen = view.WorldToScreenPoint(position);
            if (screen.z <= 0)
            {
                if (!clamp) return;
                GUI.Label(new Rect(width / 2 - 140, 90, 280, 40), "Turn around for the next gate", labelStyle);
                return;
            }
            float x = screen.x / scale, y = height - screen.y / scale;
            if (clamp) { x = Mathf.Clamp(x, 145, width - 145); y = Mathf.Clamp(y, 100, height - 220); }
            GUI.Label(new Rect(x - 140, y - 22, 280, 48), text, labelStyle);
        }
        void DrawPause(float width, float height)
        {
            Rect box = new Rect(width / 2 - 230, height / 2 - 210, 460, 420);
            Panel(box);
            GUILayout.BeginArea(new Rect(box.x + 25, box.y + 20, 410, 380));
            GUILayout.Label("S.T.A.R. LABS / PAUSED", title);
            GUILayout.Space(10);
            GUILayout.Label("Movement prototype. Explore, enter the lab, or run the city circuit.", body);
            GUILayout.Space(10);
            followCamera.DynamicFov = GUILayout.Toggle(followCamera.DynamicFov, "  Speed-based field of view");
            trailsEnabled = GUILayout.Toggle(trailsEnabled, "  Lightning trails");
            showHelp = GUILayout.Toggle(showHelp, "  Show controls");
            GUILayout.Space(15);
            if (GUILayout.Button("Resume (Esc / Menu)", GUILayout.Height(36))) SetPaused(false);
            if (GUILayout.Button("Start / restart circuit (T / Y)", GUILayout.Height(36)))
            { ReturnTo(circuit.StartPosition); circuit.Start(); SetPaused(false); }
            if (GUILayout.Button("Return to lab (R / View)", GUILayout.Height(36)))
            { circuit.Cancel(); ReturnTo(spawn); SetPaused(false); }
            GUILayout.Label("Best circuit: " + (circuit.BestTime > 0 ? circuit.BestTime.ToString("F2") + " s" : "No completed run yet"), small);
            GUILayout.EndArea();
        }
    }
}
