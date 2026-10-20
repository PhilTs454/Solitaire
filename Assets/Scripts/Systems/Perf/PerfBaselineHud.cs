using UnityEngine;

namespace Solitaire.Systems.Perf
{
    /// <summary>
    /// On-device (Development Build) overlay: mark baseline moments and write CSV.
    /// Auto-spawns in DEVELOPMENT_BUILD only — does not modify the scene asset.
    /// </summary>
    public sealed class PerfBaselineHud : MonoBehaviour
    {
        private const float PanelWidth = 320f;
        private const float ButtonHeight = 28f;

        private PerfBaselineSampler sampler;
        private string notes = "";
        private string status = "Stopped";
        private bool showPanel = true;
        private Vector2 scroll;
        private GUIStyle boxStyle;
        private GUIStyle labelStyle;
        private bool stylesReady;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
            if (FindObjectOfType<PerfBaselineHud>() != null)
                return;

            var go = new GameObject("PerfBaselineHud");
            DontDestroyOnLoad(go);
            go.AddComponent<PerfBaselineHud>();
            Debug.Log("[PerfBaseline] HUD spawned (Development Build).");
#endif
        }

        private void Awake()
        {
            sampler = new PerfBaselineSampler();
        }

        private void OnDestroy()
        {
            sampler?.Dispose();
            sampler = null;
        }

        private void Update()
        {
            if (!showPanel)
            {
                if (Input.GetKeyDown(KeyCode.F8))
                    showPanel = true;
                return;
            }

            if (Input.GetKeyDown(KeyCode.F8))
                showPanel = false;

            if (sampler == null || !sampler.IsStarted)
                return;

            // Number keys 1–8 match the moment list (phone USB keyboards / Bluetooth).
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
                CaptureAt(0);
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
                CaptureAt(1);
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
                CaptureAt(2);
            else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
                CaptureAt(3);
            else if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5))
                CaptureAt(4);
            else if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6))
                CaptureAt(5);
            else if (Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Keypad7))
                CaptureAt(6);
            else if (Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Keypad8))
                CaptureAt(7);
        }

        private void OnGUI()
        {
            if (!showPanel)
                return;

            EnsureStyles();

            var height = Mathf.Min(Screen.height * 0.7f, 520f);
            GUILayout.BeginArea(new Rect(12f, 12f, PanelWidth, height), boxStyle);
            scroll = GUILayout.BeginScrollView(scroll);

            GUILayout.Label("Perf Baseline", labelStyle);
            GUILayout.Label(status);

            notes = GUILayout.TextField(notes ?? "", GUILayout.Height(22f));

            if (sampler != null && sampler.IsStarted)
            {
                if (GUILayout.Button("Stop sampling", GUILayout.Height(ButtonHeight)))
                    StopSampling();
            }
            else
            {
                if (GUILayout.Button("Start sampling", GUILayout.Height(ButtonHeight)))
                    StartSampling();
            }

            GUILayout.Space(6f);
            GUILayout.Label("Tap moment (or keys 1–8)");

            for (var i = 0; i < PerfBaselineSampler.MomentIds.Length; i++)
            {
                var label = $"{i + 1}. {PerfBaselineSampler.MomentLabels[i]}";
                GUI.enabled = sampler != null && sampler.IsStarted;
                if (GUILayout.Button(label, GUILayout.Height(ButtonHeight)))
                    CaptureAt(i);
                GUI.enabled = true;
            }

            GUILayout.Space(6f);
            if (sampler != null && !string.IsNullOrEmpty(sampler.CsvPath))
                GUILayout.Label(sampler.CsvPath, labelStyle);

            if (GUILayout.Button("Hide (F8)", GUILayout.Height(ButtonHeight)))
                showPanel = false;

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void StartSampling()
        {
            sampler?.Start();
            status =
                sampler != null && sampler.IsStarted
                    ? $"Recording… rows={sampler.RowsWritten}"
                    : "Failed to start";
        }

        private void StopSampling()
        {
            sampler?.Stop();
            status = "Stopped";
        }

        private void CaptureAt(int index)
        {
            if (sampler == null || !sampler.IsStarted)
                return;
            if (index < 0 || index >= PerfBaselineSampler.MomentIds.Length)
                return;

            var ok = sampler.Capture(PerfBaselineSampler.MomentIds[index], notes);
            status = ok
                ? $"OK {PerfBaselineSampler.MomentIds[index]} (rows={sampler.RowsWritten})"
                : $"FAIL: {sampler.LastError}";
        }

        private void EnsureStyles()
        {
            if (stylesReady)
                return;

            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = MakeTex(2, 2, new Color(0f, 0f, 0f, 0.72f));
            boxStyle.padding = new RectOffset(8, 8, 8, 8);

            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 12;
            labelStyle.wordWrap = true;

            stylesReady = true;
        }

        private static Texture2D MakeTex(int width, int height, Color color)
        {
            var pixels = new Color[width * height];
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = color;
            var tex = new Texture2D(width, height);
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
