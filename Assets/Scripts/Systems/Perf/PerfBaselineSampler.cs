using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Solitaire.Systems.Perf
{
    /// <summary>
    /// Samples render/CPU counters and appends one CSV row per marked moment
    /// (Home idle, Play idle, Deal, Flip, etc.). Shared by the Editor window and Dev HUD.
    /// </summary>
    public sealed class PerfBaselineSampler : IDisposable
    {
        public const string CsvHeader =
            "Moment,Timestamp,Platform,Scene,Resolution,Fps,CpuFrameMs,Batches,DrawCalls,SetPass,Tris,Verts,ParticleSystems,ParticleCount,PulseCount,StarsCount,Notes";

        public static readonly string[] MomentIds =
        {
            "H_HomeIdle",
            "P_PlayIdle",
            "D_Deal",
            "F_Flip",
            "T_TableauLand",
            "Fd_FoundationLand",
            "S_Streak5",
            "W_Win"
        };

        public static readonly string[] MomentLabels =
        {
            "H Home idle",
            "P Play idle",
            "D Deal (peak)",
            "F Flip (peak)",
            "T Tableau land (peak)",
            "Fd Foundation land (peak)",
            "S Streak-5 (peak)",
            "W Win (peak)"
        };

        private readonly List<ProfilerRecorder> recorders = new List<ProfilerRecorder>(8);
        private ProfilerRecorder batchesRecorder;
        private ProfilerRecorder drawCallsRecorder;
        private ProfilerRecorder setPassRecorder;
        private ProfilerRecorder trisRecorder;
        private ProfilerRecorder vertsRecorder;
        private ProfilerRecorder mainThreadRecorder;
        private ProfilerRecorder gcAllocRecorder;
        private bool isStarted;
        private string csvPath;
        private string lastError;

        public bool IsStarted => isStarted;
        public string CsvPath => csvPath;
        public string LastError => lastError;
        public int RowsWritten { get; private set; }

        public void Start(string optionalNotesFolderName = null)
        {
            lastError = null;
            if (isStarted)
                Stop();

            TryStartRecorder(ref batchesRecorder, ProfilerCategory.Render, "Batches Count");
            TryStartRecorder(ref drawCallsRecorder, ProfilerCategory.Render, "Draw Calls Count");
            TryStartRecorder(ref setPassRecorder, ProfilerCategory.Render, "SetPass Calls Count");
            TryStartRecorder(ref trisRecorder, ProfilerCategory.Render, "Triangles Count");
            TryStartRecorder(ref vertsRecorder, ProfilerCategory.Render, "Vertices Count");
            TryStartRecorder(ref mainThreadRecorder, ProfilerCategory.Internal, "Main Thread", 15);
            TryStartRecorder(ref gcAllocRecorder, ProfilerCategory.Memory, "GC Allocated In Frame");

            csvPath = BuildCsvPath(optionalNotesFolderName);
            EnsureCsvHeader(csvPath);
            isStarted = true;
            Debug.Log($"[PerfBaseline] Sampling → {csvPath}");
        }

        public void Stop()
        {
            for (var i = 0; i < recorders.Count; i++)
            {
                var recorder = recorders[i];
                if (recorder.Valid)
                    recorder.Dispose();
            }

            recorders.Clear();
            batchesRecorder = default;
            drawCallsRecorder = default;
            setPassRecorder = default;
            trisRecorder = default;
            vertsRecorder = default;
            mainThreadRecorder = default;
            gcAllocRecorder = default;
            isStarted = false;
        }

        public void Dispose()
        {
            Stop();
        }

        /// <summary>
        /// Captures one row. Call on a representative idle frame, or on the spike frame for peaks.
        /// </summary>
        public bool Capture(string momentId, string notes = "")
        {
            lastError = null;
            if (!isStarted)
            {
                lastError = "Sampler not started.";
                Debug.LogWarning($"[PerfBaseline] {lastError}");
                return false;
            }

            if (string.IsNullOrEmpty(momentId))
            {
                lastError = "Moment id is empty.";
                return false;
            }

            try
            {
                var sample = BuildSample(momentId, notes);
                AppendRow(csvPath, sample);
                RowsWritten++;
                Debug.Log(
                    $"[PerfBaseline] {momentId}  batches={sample.Batches} setPass={sample.SetPass} "
                        + $"draw={sample.DrawCalls} cpuMs={sample.CpuFrameMs:0.00} fps={sample.Fps:0.0} "
                        + $"pulse={sample.PulseCount} stars={sample.StarsCount} → {csvPath}"
                );
                return true;
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                Debug.LogError($"[PerfBaseline] Capture failed: {ex}");
                return false;
            }
        }

        public PerfSample Peek()
        {
            return BuildSample("PEEK", "");
        }

        private PerfSample BuildSample(string momentId, string notes)
        {
            var width = Screen.width;
            var height = Screen.height;
            var dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            var fps = 1f / dt;

            var cpuMs = ReadMainThreadMs();
            if (cpuMs <= 0f)
                cpuMs = dt * 1000f;

            var batches = ReadLong(batchesRecorder);
            var drawCalls = ReadLong(drawCallsRecorder);
            var setPass = ReadLong(setPassRecorder);
            var tris = ReadLong(trisRecorder);
            var verts = ReadLong(vertsRecorder);

#if UNITY_EDITOR
            // Editor Game View stats fill gaps when ProfilerRecorder counters are unavailable.
            if (batches <= 0)
                batches = UnityStats.batches;
            if (drawCalls <= 0)
                drawCalls = UnityStats.drawCalls;
            if (setPass <= 0)
                setPass = UnityStats.setPassCalls;
            if (tris <= 0)
                tris = UnityStats.triangles;
            if (verts <= 0)
                verts = UnityStats.vertices;
#endif

            CountParticles(out var systemCount, out var particleCount);
            CountNamedObjects(out var pulseCount, out var starsCount);

            return new PerfSample
            {
                Moment = momentId,
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                Platform = Application.platform.ToString(),
                Scene = SceneManager.GetActiveScene().name,
                Resolution = $"{width}x{height}",
                Fps = fps,
                CpuFrameMs = cpuMs,
                Batches = batches,
                DrawCalls = drawCalls,
                SetPass = setPass,
                Tris = tris,
                Verts = verts,
                ParticleSystems = systemCount,
                ParticleCount = particleCount,
                PulseCount = pulseCount,
                StarsCount = starsCount,
                Notes = notes ?? string.Empty
            };
        }

        private float ReadMainThreadMs()
        {
            if (!mainThreadRecorder.Valid || mainThreadRecorder.Count <= 0)
                return 0f;

            // Main Thread recorder values are nanoseconds.
            long total = 0;
            var count = Math.Min(mainThreadRecorder.Count, 15);
            for (var i = 0; i < count; i++)
                total += mainThreadRecorder.GetSample(i).Value;

            if (count <= 0)
                return 0f;

            return (total / (float)count) * 1e-6f;
        }

        private static long ReadLong(ProfilerRecorder recorder)
        {
            if (!recorder.Valid)
                return 0;
            return recorder.LastValue;
        }

        private void TryStartRecorder(
            ref ProfilerRecorder field,
            ProfilerCategory category,
            string counterName,
            int capacity = 1
        )
        {
            try
            {
                var recorder = ProfilerRecorder.StartNew(category, counterName, capacity);
                field = recorder;
                recorders.Add(recorder);
                if (!recorder.Valid)
                {
                    Debug.LogWarning(
                        $"[PerfBaseline] Recorder not valid for '{counterName}' ({category})."
                    );
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[PerfBaseline] Could not start recorder '{counterName}': {ex.Message}"
                );
            }
        }

        private static void CountParticles(out int systemCount, out int particleCount)
        {
            systemCount = 0;
            particleCount = 0;
            var systems = UnityEngine.Object.FindObjectsOfType<ParticleSystem>();
            systemCount = systems.Length;
            for (var i = 0; i < systems.Length; i++)
            {
                var system = systems[i];
                if (system == null)
                    continue;
                particleCount += system.particleCount;
            }
        }

        private static void CountNamedObjects(out int pulseCount, out int starsCount)
        {
            pulseCount = 0;
            starsCount = 0;
            var transforms = UnityEngine.Object.FindObjectsOfType<Transform>();
            for (var i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == null)
                    continue;
                var name = t.name;
                if (name == "ParticlePulse" || name.StartsWith("ParticlePulse", StringComparison.Ordinal))
                    pulseCount++;
                else if (name == "Stars" || name.StartsWith("Stars", StringComparison.Ordinal))
                    starsCount++;
            }
        }

        private static string BuildCsvPath(string optionalNotesFolderName)
        {
            string root;
#if UNITY_EDITOR
            root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "PerfCaptures"));
#else
            root = Path.Combine(Application.persistentDataPath, "PerfCaptures");
#endif
            if (!string.IsNullOrEmpty(optionalNotesFolderName))
                root = Path.Combine(root, optionalNotesFolderName);

            Directory.CreateDirectory(root);
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            var platform = Application.platform.ToString();
            return Path.Combine(root, $"baseline_{platform}_{stamp}.csv");
        }

        private static void EnsureCsvHeader(string path)
        {
            if (File.Exists(path) && new FileInfo(path).Length > 0)
                return;

            File.WriteAllText(path, CsvHeader + Environment.NewLine, Encoding.UTF8);
        }

        private static void AppendRow(string path, PerfSample sample)
        {
            var line = string.Join(
                ",",
                Escape(sample.Moment),
                Escape(sample.Timestamp),
                Escape(sample.Platform),
                Escape(sample.Scene),
                Escape(sample.Resolution),
                sample.Fps.ToString("0.00", CultureInfo.InvariantCulture),
                sample.CpuFrameMs.ToString("0.00", CultureInfo.InvariantCulture),
                sample.Batches.ToString(CultureInfo.InvariantCulture),
                sample.DrawCalls.ToString(CultureInfo.InvariantCulture),
                sample.SetPass.ToString(CultureInfo.InvariantCulture),
                sample.Tris.ToString(CultureInfo.InvariantCulture),
                sample.Verts.ToString(CultureInfo.InvariantCulture),
                sample.ParticleSystems.ToString(CultureInfo.InvariantCulture),
                sample.ParticleCount.ToString(CultureInfo.InvariantCulture),
                sample.PulseCount.ToString(CultureInfo.InvariantCulture),
                sample.StarsCount.ToString(CultureInfo.InvariantCulture),
                Escape(sample.Notes)
            );
            File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0)
                return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        public struct PerfSample
        {
            public string Moment;
            public string Timestamp;
            public string Platform;
            public string Scene;
            public string Resolution;
            public float Fps;
            public float CpuFrameMs;
            public long Batches;
            public long DrawCalls;
            public long SetPass;
            public long Tris;
            public long Verts;
            public int ParticleSystems;
            public int ParticleCount;
            public int PulseCount;
            public int StarsCount;
            public string Notes;
        }
    }
}
