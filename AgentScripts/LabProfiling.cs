using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using OptimizationLab;

public static class LabProfiling
{
    const string Root = "Assets/03.Thread_Bottleneck/Scenes/";

    [Serializable] public class MarkerSummary { public string thread, marker; public float medianMs, maxMs; public int frames; }
    [Serializable] public class ProfileSummary {
        public string scene, note, profilePath; public int frames; public float medianFrameMs, medianGpuMs;
        public int firstFrameIndex, lastFrameIndex;
        public List<MarkerSummary> markers = new List<MarkerSummary>();
    }
    public static async Task<string> Capture(string sceneName, float seconds = 2)
    {
        if (!EditorApplication.isPlaying) throw new Exception("Enter Play first.");
        UnityEngine.SceneManagement.SceneManager.LoadScene(Root + sceneName + ".unity");
        await Task.Delay(150);
        var game = UnityEngine.Object.FindFirstObjectByType<SurvivorGame>();
        game.AutoDemo = true; game.testInvulnerable = true;
        game.PerformanceMode = true; game.ApplyFrameLimit();
        float deadline = Time.realtimeSinceStartup + 30;
        while (game.Elapsed < 5) {
            await Task.Delay(100);
            if (Time.realtimeSinceStartup > deadline) throw new Exception("Game did not advance.");
        }
        bool previousEnabled = ProfilerDriver.enabled;
        bool previousEditor = ProfilerDriver.profileEditor;
        bool previousGpu = ProfilerDriver.profileGPU;
        bool previousDeep = ProfilerDriver.deepProfiling;
        bool previousCpuArea = ProfilerDriver.IsAreaEnabled(UnityEngine.Profiling.ProfilerArea.CPU);
        bool previousGpuArea = ProfilerDriver.IsAreaEnabled(UnityEngine.Profiling.ProfilerArea.GPU);
        var result = new ProfileSummary { scene = sceneName, note = "Editor CPU/GPU capture, no FPS cap, Deep Profile off. Profiling adds overhead; marker times are not additive across nested samples." };
        try
        {
            ProfilerDriver.profileEditor = false;
            ProfilerDriver.deepProfiling = false;
            ProfilerDriver.SetAreaEnabled(UnityEngine.Profiling.ProfilerArea.CPU, true);
            ProfilerDriver.SetAreaEnabled(UnityEngine.Profiling.ProfilerArea.GPU, sceneName.StartsWith("04_GPU"));
            // The lab targets Windows/D3D11; unavailable GPU samples are reported as zero.
            ProfilerDriver.profileGPU = sceneName.StartsWith("04_GPU");
            int captureStart = ProfilerDriver.lastFrameIndex;
            // Earlier captures belong to this tool and are already saved on disk.
            ProfilerDriver.ClearAllFrames();
            captureStart = -1;
            ProfilerDriver.enabled = true;
            await Task.Delay((int)(seconds * 1000));
            ProfilerDriver.enabled = false;
            int last = ProfilerDriver.lastFrameIndex;
            int first = Math.Max(Math.Max(ProfilerDriver.firstFrameIndex, captureStart + 1), last - 59);
            result.firstFrameIndex = first; result.lastFrameIndex = last;
            var frameTimes = new List<float>();
            var gpuTimes = new List<float>();
            var markers = new Dictionary<string, List<float>>();
            for (int frame = first; frame <= last; frame++)
            {
                using (var main = ProfilerDriver.GetRawFrameDataView(frame, 0))
                {
                    if (!main.valid) continue;
                    frameTimes.Add((float)main.frameTimeMs);
                    if (main.frameGpuTimeMs > 0) gpuTimes.Add((float)main.frameGpuTimeMs);
                }
                for (int thread = 0; thread < 128; thread++)
                {
                    using (var raw = ProfilerDriver.GetRawFrameDataView(frame, thread))
                    {
                        if (!raw.valid) continue;
                        var perFrame = new Dictionary<string, float>();
                        for (int sample = 0; sample < raw.sampleCount; sample++)
                        {
                            string name = raw.GetSampleName(sample);
                            if (string.IsNullOrEmpty(name)) continue;
                            if (!name.StartsWith("Lab.") && raw.threadName != "Main Thread" && raw.threadName != "Render Thread") continue;
                            if (!(name.StartsWith("Lab.") || name.Contains("WaitFor") || name.Contains("PresentFrame") ||
                                  name.Contains("Camera.Render") || name.Contains("DrawRenderers") || name.Contains("JobHandle") ||
                                  name == "PlayerLoop" || name.Contains("Gfx.ProcessCommands"))) continue;
                            string key = raw.threadName + "|" + name;
                            float ms = raw.GetSampleTimeMs(sample);
                            if (perFrame.ContainsKey(key)) perFrame[key] += ms; else perFrame[key] = ms;
                        }
                        foreach (var pair in perFrame)
                        {
                            if (!markers.ContainsKey(pair.Key)) markers[pair.Key] = new List<float>();
                            markers[pair.Key].Add(pair.Value);
                        }
                    }
                }
            }
            result.frames = frameTimes.Count;
            if (result.frames == 0) throw new Exception("Profiler captured no frames.");
            result.medianFrameMs = Median(frameTimes);
            result.medianGpuMs = gpuTimes.Count == 0 ? 0 : Median(gpuTimes);
            foreach (var pair in markers)
            {
                string[] split = pair.Key.Split('|');
                result.markers.Add(new MarkerSummary { thread = split[0], marker = split[1],
                    medianMs = Median(pair.Value), maxMs = pair.Value.Max(), frames = pair.Value.Count });
            }
            result.markers = result.markers.OrderByDescending(x => x.medianMs).Take(40).ToList();
            string folder = Path.GetFullPath("workspace/2026-10-16_Chapter03_Thread_Bottleneck_Lab/Captures");
            Directory.CreateDirectory(folder);
            result.profilePath = Path.Combine(folder, sceneName + ".data");
            if (!ProfilerDriver.SaveProfile(result.profilePath)) throw new Exception("Profiler SaveProfile failed.");
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(Path.Combine(folder, sceneName + "-profile.json"), json);
            return json;
        }
        finally
        {
            ProfilerDriver.enabled = previousEnabled;
            ProfilerDriver.profileEditor = previousEditor;
            ProfilerDriver.profileGPU = previousGpu;
            ProfilerDriver.deepProfiling = previousDeep;
            ProfilerDriver.SetAreaEnabled(UnityEngine.Profiling.ProfilerArea.CPU, previousCpuArea);
            ProfilerDriver.SetAreaEnabled(UnityEngine.Profiling.ProfilerArea.GPU, previousGpuArea);
        }
    }
    static float Median(List<float> values) { values.Sort(); return values[values.Count / 2]; }

    public static string CalibratePresets()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop first.");
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) throw new Exception("Save edits first.");
        foreach (string name in new[] { "03_RenderThread_Problem", "03_RenderThread_Improved" })
        {
            var scene = EditorSceneManager.OpenScene(Root + name + ".unity");
            var crowd = UnityEngine.Object.FindFirstObjectByType<CrowdWork>();
            crowd.crowdCount = 40000;
            PrefabUtility.RecordPrefabInstancePropertyModifications(crowd);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        var gpuScene = EditorSceneManager.OpenScene(Root + "04_GPU_Problem.unity");
        var gpu = UnityEngine.Object.FindFirstObjectByType<GpuProblem>();
        gpu.shaderIterations = 8192;
        PrefabUtility.RecordPrefabInstancePropertyModifications(gpu);
        EditorSceneManager.MarkSceneDirty(gpuScene); EditorSceneManager.SaveScene(gpuScene);
        EditorSceneManager.OpenScene(Root + "00_Template.unity");
        return "Scene presets: 40000 render-only enemies in both scenes; 8192 GPU samples.";
    }
}
