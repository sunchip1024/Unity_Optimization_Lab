using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Threading.Tasks;
using OptimizationLab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LabVerification
{
    const string Root = "Assets/03.Thread_Bottleneck";
    public static string ConfigureGameView()
    {
        var assembly = typeof(Editor).Assembly;
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var sizeType = assembly.GetType("UnityEditor.GameViewSize");
        var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
        var sizeKind = assembly.GetType("UnityEditor.GameViewSizeType");
        var sizes = sizesType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).GetValue(null);
        var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { Enum.Parse(groupType, "Standalone") });
        var names = (string[])group.GetType().GetMethod("GetDisplayTexts").Invoke(group, null);
        int index = Array.FindIndex(names, x => x.Contains("Optimization Lab 1280"));
        if (index < 0)
        {
            var size = Activator.CreateInstance(sizeType, Enum.Parse(sizeKind, "FixedResolution"), 1280, 720, "Optimization Lab 1280");
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1;
        }
        var window = EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
        window.GetType().GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(window, index);
        window.Repaint();
        return "Game view fixed to 1280x720 (index " + index + ")";
    }

    public static string ValidateAssets()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play mode first.");
        string[] paths = AssetDatabase.FindAssets("t:Scene", new[] { Root + "/Scenes" }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(x => x).ToArray();
        if (paths.Length != 9) throw new Exception("Expected Template plus eight scenes, found " + paths.Length);
        var config = AssetDatabase.LoadAssetAtPath<LabGameSettings>(Root + "/Settings/GameSettings.asset");
        foreach (string path in paths)
        {
            var scene = EditorSceneManager.OpenScene(path);
            var game = UnityEngine.Object.FindFirstObjectByType<SurvivorGame>();
            if (game == null || game.settings != config || game.hud == null || game.player == null)
                throw new Exception("Missing shared references: " + path);
            if (!PrefabUtility.IsPartOfPrefabInstance(game.gameObject)) throw new Exception("Not a shared prefab instance: " + path);
            int count = game.GetComponentsInChildren<LabExperiment>(true).Length;
            if (count != (path.Contains("Template") ? 0 : 1)) throw new Exception("Wrong experiment count: " + path);
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) > 0)
                        throw new Exception("Missing script: " + child.name);
            if (game.hud.timerText.font == null || game.hud.healthFill.sprite == null)
                throw new Exception("Missing HUD font/sprite.");
        }
        EditorSceneManager.OpenScene(Root + "/Scenes/00_Template.unity");
        return "PASS: 9 scenes, shared GameRoot/config, expected components, HUD references, no missing scripts.";
    }

    [Serializable] public class Measurements { public string note; public List<SurvivorGame.Report> scenes = new List<SurvivorGame.Report>(); }
    public static async Task<string> CompareRange(int start, int count, float captureSeconds = 6)
    {
        if (!EditorApplication.isPlaying) throw new Exception("Enter Play mode first.");
        var output = new Measurements { note = "Editor Game view 1280x720; target 60; automated movement; god mode only during verification. Editor timings include tooling overhead." };
        string[] names = {
            "00_Template", "01_MainThread_Problem", "01_MainThread_Improved",
            "02_WorkerThread_Problem", "02_WorkerThread_Improved",
            "03_RenderThread_Problem", "03_RenderThread_Improved",
            "04_GPU_Problem", "04_GPU_Improved" };
        string captureDirectory = Path.GetFullPath("workspace/2026-10-16_Chapter03_Thread_Bottleneck_Lab/Captures");
        Directory.CreateDirectory(captureDirectory);
        foreach (string name in names.Skip(start).Take(count))
        {
            SceneManager.LoadScene(Root + "/Scenes/" + name + ".unity");
            await Task.Delay(100);
            var game = UnityEngine.Object.FindFirstObjectByType<SurvivorGame>();
            if (game == null) throw new Exception("Game missing in " + name);
            game.AutoDemo = true; game.testInvulnerable = true;
            float deadline = Time.realtimeSinceStartup + 45;
            while (game.Elapsed < 5)
            {
                await Task.Delay(100);
                if (Time.realtimeSinceStartup > deadline) throw new Exception("Timed out advancing " + name);
            }
            game.ResetFrameMeasurements();
            while (game.Elapsed < 5 + captureSeconds)
            {
                await Task.Delay(100);
                if (Time.realtimeSinceStartup > deadline) throw new Exception("Timed out sampling " + name);
            }
            output.scenes.Add(JsonUtility.FromJson<SurvivorGame.Report>(game.Snapshot()));
            ScreenCapture.CaptureScreenshot(Path.Combine(captureDirectory, name + ".png"));
            File.WriteAllText(Path.Combine(captureDirectory, "measurements-" + start + ".json"), JsonUtility.ToJson(output, true));
            await Task.Delay(150);
        }
        return JsonUtility.ToJson(output, true);
    }


    public static async Task<string> VerifyAlgorithms()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Enter Play first.");
        Vector3[] mainBaseline = null, workerBaseline = null;
        foreach (string name in new[] { "01_MainThread_Problem", "01_MainThread_Improved", "02_WorkerThread_Problem", "02_WorkerThread_Improved" })
        {
            SceneManager.LoadScene(Root + "/Scenes/" + name + ".unity");
            await Task.Delay(150);
            var game = UnityEngine.Object.FindFirstObjectByType<SurvivorGame>();
            game.enabled = false;
            game.Restart();
            var work = game.GetComponentInChildren<LabExperiment>();
            work.activationTime = 0;
            work.Step(game);
            if (name.StartsWith("01"))
            {
                if (mainBaseline == null) mainBaseline = (Vector3[])game.SpawnCandidates.Clone();
                else
                {
                    for (int i = 0; i < mainBaseline.Length; i++)
                        if ((mainBaseline[i] - game.SpawnCandidates[i]).sqrMagnitude > 0.000001f)
                            throw new Exception("Spawn candidates differ.");
                    var improved = (MainThreadImproved)work;
                    int count = improved.BuildCount;
                    work.Step(game);
                    if (improved.BuildCount != count) throw new Exception("Cache rebuilt without changed input.");
                    improved.mapRadius += 1;
                    work.Step(game);
                    if (improved.BuildCount != count + 1) throw new Exception("Cache did not invalidate.");
                }
            }
            else
            {
                if (workerBaseline == null) workerBaseline = (Vector3[])game.LastOrbitPositions.Clone();
                else
                    for (int i = 0; i < workerBaseline.Length; i++)
                        if ((workerBaseline[i] - game.LastOrbitPositions[i]).sqrMagnitude > 0.000001f)
                            throw new Exception("Job results differ for the same input/time.");
            }
        }
        SceneManager.LoadScene(Root + "/Scenes/00_Template.unity");
        await Task.Delay(150);
        return "PASS: spawn results match; cache reuse/invalidation; Job positions match for identical time and input.";
    }

    public static async Task<string> VerifyGameRules()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Enter Play first.");
        var game = UnityEngine.Object.FindFirstObjectByType<SurvivorGame>();
        int pooled = game.actorRoot.childCount;
        game.Restart();
        game.AutoDemo = true; game.testInvulnerable = true;
        game.GainExperience(30);
        if (game.Level < 2) throw new Exception("Level-up failed.");
        await Task.Delay(300);
        Vector3 before = game.player.position;
        await Task.Delay(500);
        if ((game.player.position - before).sqrMagnitude < 0.01f) throw new Exception("Movement is frozen.");
        game.durationOverride = game.Elapsed + 0.2f;
        await Task.Delay(600);
        if (!game.Finished || !game.Victory) throw new Exception("Victory transition failed.");
        game.durationOverride = 0;
        game.Restart();
        if (game.Level != 1 || game.Kills != 0 || game.Health != 100 || game.Finished) throw new Exception("Restart state failed.");
        if (game.actorRoot.childCount != pooled) throw new Exception("Restart grew the object pools.");
        game.testInvulnerable = false;
        game.TakeDamage(100);
        await Task.Delay(200);
        if (!game.Finished || game.Victory || game.Health != 0) throw new Exception("Game-over transition failed.");
        game.Restart();
        return "PASS: movement, level-up, victory, game-over, restart, fixed pool size.";
    }
}
