using System;
using System.IO;
using System.Linq;
using UnityEngine.SceneManagement;
using UnityEngine;
namespace OptimizationLab
{
    // Development Player integration test, opt-in via --lab-verify. Normal play never enables it.
    [DefaultExecutionOrder(1000)]
    public sealed class StandaloneVerification : MonoBehaviour
    {
        SurvivorGame game;
        int initialPoolSize;
        bool done;
        bool captured;
        string requestedScene;
        [Serializable] class Result
        {
            public bool passed;
            public string playedRun;
            public bool victory, gameOver, restart, levelUp, fixedPools;
            public string graphicsApi;
            public float realSeconds;
        }
        void Start()
        {
            game = GetComponent<SurvivorGame>();
            string[] args = Environment.GetCommandLineArgs();
            string sceneArgument = args.FirstOrDefault(x => x.StartsWith("--lab-scene="));
            requestedScene = sceneArgument == null ? null : sceneArgument.Substring("--lab-scene=".Length);
            if (!string.IsNullOrEmpty(requestedScene) && SceneManager.GetActiveScene().name != requestedScene)
            {
                SceneManager.LoadScene(requestedScene);
                return;
            }
            string durationArgument = args.FirstOrDefault(x => x.StartsWith("--lab-duration="));
            if (durationArgument != null && float.TryParse(durationArgument.Substring("--lab-duration=".Length),
                System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float duration))
                game.durationOverride = Mathf.Max(6, duration);
            initialPoolSize = game.actorRoot.childCount;
        }
        void Update()
        {
            if (done || game == null) return;
            if (!captured && game.Elapsed > 5)
            {
                captured = true;
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Application.dataPath, "..", SceneManager.GetActiveScene().name + "-player.png")));
            }
            if (!game.Finished) return;
            done = true;
            var result = new Result {
                playedRun = game.Snapshot(), victory = game.Victory && game.Elapsed >= (game.durationOverride > 0 ? game.durationOverride : game.settings.duration),
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(), realSeconds = Time.realtimeSinceStartup };
            game.testInvulnerable = false;
            game.TakeDamage(100);
            result.gameOver = game.Finished && !game.Victory && game.Health == 0;
            game.Restart();
            result.restart = !game.Finished && game.Kills == 0 && game.Level == 1 && game.Health == 100;
            result.fixedPools = game.actorRoot.childCount == initialPoolSize;
            game.GainExperience(30);
            result.levelUp = game.Level > 1;
            result.passed = result.victory && result.gameOver && result.restart && result.fixedPools && result.levelUp;
            string filename = string.IsNullOrEmpty(requestedScene) ? "verification.json" : "verification-" + requestedScene + ".json";
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", filename));
            File.WriteAllText(path, JsonUtility.ToJson(result, true));
            Debug.Log("Standalone integration verification: " + (result.passed ? "PASS" : "FAIL"));
            Application.Quit(result.passed ? 0 : 1);
        }
    }
}
