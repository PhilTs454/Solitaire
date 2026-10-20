using Solitaire.Systems.Perf;
using UnityEditor;
using UnityEngine;

namespace Solitaire.Editor
{
    /// <summary>
    /// Play Mode capture UI for the PerfBaselineSampler. Writes CSV under /PerfCaptures.
    /// </summary>
    public sealed class PerfBaselineWindow : EditorWindow
    {
        private PerfBaselineSampler sampler;
        private string notes = "";
        private string status = "Enter Play Mode, then Start.";
        private Vector2 scroll;
        private bool showLivePeek = true;
        private double nextRepaintTime;

        [MenuItem("Tools/Solitaire/Perf Baseline Capture")]
        public static void Open()
        {
            var window = GetWindow<PerfBaselineWindow>("Perf Baseline");
            window.minSize = new Vector2(360f, 480f);
            window.Show();
        }

        [MenuItem("Tools/Solitaire/Spawn Perf HUD In Play Mode")]
        public static void SpawnHudInPlayMode()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog(
                    "Perf Baseline",
                    "Enter Play Mode first, then run this again.",
                    "OK"
                );
                return;
            }

            if (Object.FindObjectOfType<PerfBaselineHud>() != null)
            {
                Debug.Log("[PerfBaseline] HUD already in the scene.");
                return;
            }

            var go = new GameObject("PerfBaselineHud");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<PerfBaselineHud>();
            Debug.Log("[PerfBaseline] HUD spawned for Editor Play Mode.");
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.update -= OnEditorUpdate;
            DisposeSampler();
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (
                state == PlayModeStateChange.ExitingPlayMode
                || state == PlayModeStateChange.EnteredEditMode
            )
            {
                DisposeSampler();
                status = "Play Mode ended. Sampler stopped.";
                Repaint();
            }
        }

        private void OnEditorUpdate()
        {
            if (!showLivePeek || !EditorApplication.isPlaying)
                return;
            if (EditorApplication.timeSinceStartup < nextRepaintTime)
                return;
            nextRepaintTime = EditorApplication.timeSinceStartup + 0.5;
            Repaint();
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.LabelField("Perf Baseline Capture", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Marks the same moments as the Android checklist (H/P/D/F/T/Fd/S/W).\n"
                    + "CSV → ProjectRoot/PerfCaptures/\n"
                    + "Editor numbers ≠ device — label Platform in the CSV.",
                MessageType.Info
            );

            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            {
                notes = EditorGUILayout.TextField("Notes", notes);
                showLivePeek = EditorGUILayout.Toggle("Live peek (0.5s)", showLivePeek);

                EditorGUILayout.Space(4f);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Start sampling", GUILayout.Height(28f)))
                    StartSampling();
                if (GUILayout.Button("Stop", GUILayout.Height(28f)))
                    StopSampling();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("Moments", EditorStyles.boldLabel);

                var canCapture = sampler != null && sampler.IsStarted;
                using (new EditorGUI.DisabledScope(!canCapture))
                {
                    for (var i = 0; i < PerfBaselineSampler.MomentIds.Length; i++)
                    {
                        var label =
                            $"{i + 1}. {PerfBaselineSampler.MomentLabels[i]}  ({PerfBaselineSampler.MomentIds[i]})";
                        if (GUILayout.Button(label, GUILayout.Height(26f)))
                            CaptureAt(i);
                    }
                }
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(status, MessageType.None);

            if (sampler != null && sampler.IsStarted)
            {
                EditorGUILayout.LabelField("CSV", sampler.CsvPath);
                EditorGUILayout.LabelField("Rows", sampler.RowsWritten.ToString());

                if (showLivePeek && EditorApplication.isPlaying)
                {
                    var peek = sampler.Peek();
                    EditorGUILayout.Space(4f);
                    EditorGUILayout.LabelField("Live peek", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(
                        $"Batches {peek.Batches} | Draw {peek.DrawCalls} | SetPass {peek.SetPass}"
                    );
                    EditorGUILayout.LabelField(
                        $"CPU {peek.CpuFrameMs:0.00} ms | FPS {peek.Fps:0.0} | Tris {peek.Tris}"
                    );
                    EditorGUILayout.LabelField(
                        $"Particles sys {peek.ParticleSystems} count {peek.ParticleCount} | Pulse {peek.PulseCount} | Stars {peek.StarsCount}"
                    );
                }

                if (GUILayout.Button("Reveal CSV folder"))
                {
                    EditorUtility.RevealInFinder(sampler.CsvPath);
                }
            }

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Spawn Game View HUD (optional)"))
                SpawnHudInPlayMode();

            EditorGUILayout.EndScrollView();

            HandleHotkeys();
        }

        private void HandleHotkeys()
        {
            var e = Event.current;
            if (e == null || e.type != EventType.KeyDown)
                return;
            if (sampler == null || !sampler.IsStarted || !EditorApplication.isPlaying)
                return;

            var index = -1;
            if (e.keyCode >= KeyCode.Alpha1 && e.keyCode <= KeyCode.Alpha8)
                index = (int)e.keyCode - (int)KeyCode.Alpha1;
            else if (e.keyCode >= KeyCode.Keypad1 && e.keyCode <= KeyCode.Keypad8)
                index = (int)e.keyCode - (int)KeyCode.Keypad1;

            if (index < 0)
                return;

            CaptureAt(index);
            e.Use();
        }

        private void StartSampling()
        {
            if (!EditorApplication.isPlaying)
            {
                status = "Enter Play Mode first.";
                return;
            }

            DisposeSampler();
            sampler = new PerfBaselineSampler();
            sampler.Start();
            status = sampler.IsStarted
                ? $"Recording → {sampler.CsvPath}"
                : "Failed to start sampler.";
        }

        private void StopSampling()
        {
            DisposeSampler();
            status = "Stopped.";
        }

        private void CaptureAt(int index)
        {
            if (sampler == null || !sampler.IsStarted)
            {
                status = "Start sampling first.";
                return;
            }

            var id = PerfBaselineSampler.MomentIds[index];
            var ok = sampler.Capture(id, notes);
            status = ok
                ? $"Captured {id} (row {sampler.RowsWritten})"
                : $"Failed: {sampler.LastError}";
            Repaint();
        }

        private void DisposeSampler()
        {
            if (sampler == null)
                return;
            sampler.Dispose();
            sampler = null;
        }
    }
}
