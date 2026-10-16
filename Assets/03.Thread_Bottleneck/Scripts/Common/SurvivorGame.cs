using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using Unity.Profiling;

namespace OptimizationLab
{
    public sealed class SurvivorGame : MonoBehaviour
    {
        public LabGameSettings settings;
        public Transform player, orbitRoot, actorRoot;
        public Camera viewCamera;
        public LabHud hud;
        public bool AutoDemo;
        public bool PerformanceMode;
        [Tooltip("Verification only; normal gameplay keeps this off.")] public bool testInvulnerable;
        public float durationOverride;
        public float Elapsed { get; private set; }
        public float Health { get; private set; } = 100;
        public int Level { get; private set; } = 1;
        public int Experience { get; private set; }
        public int Kills { get; private set; }
        public int NextLevelExperience => 5 + Level * 3;
        public int ActiveEnemies { get; private set; }
        public bool Finished { get; private set; }
        public bool Victory { get; private set; }
        public float NovaCooldown { get; private set; }
        public float SmoothedFrameMs { get; private set; } = 16.7f;
        public double ExperimentWorkMs => experiment == null ? 0 : experiment.LastWorkMs;
        public string SceneLabel { get; private set; }
        public Vector3[] SpawnCandidates { get; set; }
        public Vector3[] LastOrbitPositions { get; private set; }
        public float[] FrameSamples => frameSamples;
        public int SampleCount => sampleCount;
        public int TotalSpawned { get; private set; }
        public int ShotsFired { get; private set; }
        public void ResetFrameMeasurements() { sampleCount = sampleCursor = 0; }

        struct Enemy { public Transform root; public Renderer body; public Vector3 position; public float hp, flash, dying; public bool active; }
        struct Bolt { public Transform root; public Vector3 position, velocity; public float life, damage; public bool active; }
        struct Gem { public Transform root; public Vector3 position; public bool active; }
        Enemy[] enemies;
        Bolt[] bolts;
        Gem[] gems;
        Transform[] blades;
        Transform nova;
        LabExperiment experiment;
        System.Random random;
        MaterialPropertyBlock hitBlock, clearBlock;
        float spawnAccumulator, shotAt, orbitDamageAt, hurtUntil;
        int hudLevel;
        readonly float[] frameSamples = new float[900];
        int sampleCount, sampleCursor;
        int originalVSync, originalTarget;
        bool originalBackground;
        bool started;
        Vector3 cameraOffset;
        static readonly ProfilerMarker simulationMarker = new ProfilerMarker("Lab.Game.Simulation");

        void Start()
        {
            started = true;
            originalVSync = QualitySettings.vSyncCount;
            originalTarget = Application.targetFrameRate;
            originalBackground = Application.runInBackground;
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            ApplyFrameLimit();
            SceneLabel = SceneManager.GetActiveScene().name.Replace("_", "  /  ");
            cameraOffset = viewCamera.transform.position - player.position;
            experiment = GetComponentInChildren<LabExperiment>();
            hitBlock = new MaterialPropertyBlock();
            hitBlock.SetColor("_Color", new Color(1f, 0.9f, 0.75f));
            clearBlock = new MaterialPropertyBlock();
            enemies = new Enemy[settings.maxEnemies];
            for (int i = 0; i < enemies.Length; i++)
            {
                Transform root = Instantiate(settings.enemyPrefab, actorRoot).transform;
                enemies[i] = new Enemy { root = root, body = root.GetComponentInChildren<Renderer>() };
                root.gameObject.SetActive(false);
            }
            bolts = new Bolt[settings.maxProjectiles];
            for (int i = 0; i < bolts.Length; i++)
            {
                bolts[i].root = Instantiate(settings.projectilePrefab, actorRoot).transform;
                bolts[i].root.gameObject.SetActive(false);
            }
            gems = new Gem[settings.maxGems];
            for (int i = 0; i < gems.Length; i++)
            {
                gems[i].root = Instantiate(settings.gemPrefab, actorRoot).transform;
                gems[i].root.gameObject.SetActive(false);
            }
            blades = new Transform[3];
            for (int i = 0; i < blades.Length; i++)
                blades[i] = Instantiate(settings.orbitPrefab, orbitRoot).transform;
            LastOrbitPositions = new Vector3[3];
            nova = transform.Find("NovaPulse");
            if (experiment != null) experiment.Initialize(this);
            Restart();
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
            if (System.Array.IndexOf(Environment.GetCommandLineArgs(), "--lab-verify") >= 0)
            {
                AutoDemo = true; testInvulnerable = true;
                gameObject.AddComponent<StandaloneVerification>();
            }
#endif
        }

        public void ApplyFrameLimit() { Application.targetFrameRate = PerformanceMode ? -1 : 60; }

        public void Restart()
        {
            if (enemies == null) return;
            random = new System.Random(settings.seed);
            Elapsed = 0; Health = 100; Level = 1; Experience = Kills = ActiveEnemies = 0;
            TotalSpawned = ShotsFired = 0; sampleCount = sampleCursor = 0;
            Finished = Victory = false; NovaCooldown = 0;
            spawnAccumulator = shotAt = orbitDamageAt = hurtUntil = 0;
            player.position = Vector3.zero;
            player.rotation = Quaternion.identity;
            viewCamera.transform.position = cameraOffset;
            for (int i = 0; i < enemies.Length; i++)
            {
                enemies[i].active = false; enemies[i].dying = 0;
                enemies[i].root.localScale = Vector3.one;
                enemies[i].root.gameObject.SetActive(false);
                enemies[i].body.SetPropertyBlock(clearBlock);
            }
            for (int i = 0; i < bolts.Length; i++) { bolts[i].active = false; bolts[i].root.gameObject.SetActive(false); }
            for (int i = 0; i < gems.Length; i++) { gems[i].active = false; gems[i].root.gameObject.SetActive(false); }
            if (nova != null) nova.gameObject.SetActive(false);
            hud.endPanel.SetActive(false);
            if (experiment != null) experiment.ResetExperiment();
        }

        void Update()
        {
            if (enemies == null) return;
            if (Input.GetKeyDown(KeyCode.R)) Restart();
            if (Input.GetKeyDown(KeyCode.F2)) AutoDemo = !AutoDemo;
            if (Input.GetKeyDown(KeyCode.F3)) hud.ToggleDiagnostics();
            if (Input.GetKeyDown(KeyCode.F4)) { PerformanceMode = !PerformanceMode; ApplyFrameLimit(); }
            if (Input.GetKeyDown(KeyCode.F5)) SwitchScene(-1);
            if (Input.GetKeyDown(KeyCode.F6)) SwitchScene(1);
            float dt = Time.deltaTime;
            SmoothedFrameMs = Mathf.Lerp(SmoothedFrameMs, Time.unscaledDeltaTime * 1000f, 0.07f);
            if (!Finished)
            {
                Elapsed += dt;
                if (experiment != null) experiment.Step(this);
                using (simulationMarker.Auto()) Simulate(dt);
                if (!Finished && Elapsed >= (durationOverride > 0 ? durationOverride : settings.duration))
                { Victory = true; Finished = true; }
                if (Elapsed > 4)
                {
                    frameSamples[sampleCursor] = Time.unscaledDeltaTime * 1000f;
                    sampleCursor = (sampleCursor + 1) % frameSamples.Length;
                    sampleCount = Mathf.Min(sampleCount + 1, frameSamples.Length);
                }
            }
            hud.Draw(this);
        }

        void Simulate(float dt)
        {
            Vector3 motion;
            if (AutoDemo)
            {
                Vector3 goal = new Vector3(Mathf.Sin(Elapsed * 0.23f) * 7f, 0, Mathf.Cos(Elapsed * 0.23f) * 6f);
                motion = Vector3.ClampMagnitude(goal - player.position, 1);
            }
            else
            {
                float x = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0) -
                          (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
                float z = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0) -
                          (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
                motion = Vector3.ClampMagnitude(new Vector3(x, 0, z), 1);
            }
            player.position += motion * settings.playerSpeed * dt;
            player.position = new Vector3(Mathf.Clamp(player.position.x, -17, 17), 0, Mathf.Clamp(player.position.z, -17, 17));
            if (motion.sqrMagnitude > 0.01f)
                player.rotation = Quaternion.Slerp(player.rotation, Quaternion.LookRotation(motion), dt * 14);
            viewCamera.transform.position = Vector3.Lerp(viewCamera.transform.position,
                player.position * 0.7f + cameraOffset, 1 - Mathf.Exp(-dt * 6f));
            spawnAccumulator += dt * (settings.spawnPerSecond + Elapsed * 0.025f);
            while (spawnAccumulator >= 1)
            {
                spawnAccumulator--;
                SpawnEnemy();
            }
            if (Elapsed >= shotAt) { Fire(); shotAt = Elapsed + Mathf.Max(0.12f, 0.42f - Level * 0.018f); }
            NovaCooldown = Mathf.Max(0, NovaCooldown - dt);
            if ((Input.GetKeyDown(KeyCode.Space) || AutoDemo) && NovaCooldown <= 0) CastNova();
            if (nova != null && nova.gameObject.activeSelf)
            {
                nova.localScale += Vector3.one * dt * 19;
                if (nova.localScale.x > 15) nova.gameObject.SetActive(false);
            }

            for (int i = 0; i < enemies.Length; i++)
            {
                if (!enemies[i].active)
                {
                    if (enemies[i].dying > 0)
                    {
                        enemies[i].dying = Mathf.Max(0, enemies[i].dying - dt);
                        enemies[i].root.localScale = Vector3.one * (enemies[i].dying / 0.18f);
                        if (enemies[i].dying <= 0) enemies[i].root.gameObject.SetActive(false);
                    }
                    continue;
                }
                Vector3 delta = player.position - enemies[i].position;
                float distance = delta.magnitude;
                if (distance > 0.1f)
                {
                    enemies[i].position += delta / distance * (1.5f + Elapsed * 0.006f) * dt;
                    enemies[i].root.rotation = Quaternion.LookRotation(delta);
                }
                enemies[i].root.position = enemies[i].position + Vector3.up * Mathf.Sin(Elapsed * 7 + i) * 0.045f;
                if (enemies[i].flash > 0)
                {
                    enemies[i].flash -= dt;
                    if (enemies[i].flash <= 0) enemies[i].body.SetPropertyBlock(clearBlock);
                }
                if (distance < 0.9f && Elapsed > hurtUntil)
                {
                    hurtUntil = Elapsed + 0.65f;
                    TakeDamage(10);
                    if (Finished) break;
                }
            }
            for (int i = 0; i < bolts.Length; i++)
            {
                if (!bolts[i].active) continue;
                Vector3 old = bolts[i].position;
                bolts[i].position += bolts[i].velocity * dt;
                bolts[i].life -= dt;
                bool hit = false;
                for (int j = 0; j < enemies.Length; j++)
                {
                    if (!enemies[j].active) continue;
                    Vector3 a = old - enemies[j].position;
                    Vector3 segment = bolts[i].position - old;
                    float t = Mathf.Clamp01(-Vector3.Dot(a, segment) / Mathf.Max(0.0001f, segment.sqrMagnitude));
                    if ((a + segment * t).sqrMagnitude > 0.5f) continue;
                    DamageEnemy(j, bolts[i].damage); hit = true; break;
                }
                bolts[i].root.position = bolts[i].position + Vector3.up * 0.8f;
                if (hit || bolts[i].life <= 0) { bolts[i].active = false; bolts[i].root.gameObject.SetActive(false); }
            }
            for (int i = 0; i < gems.Length; i++)
            {
                if (!gems[i].active) continue;
                Vector3 delta = player.position - gems[i].position;
                float distance = delta.magnitude;
                if (distance < 4) gems[i].position += delta / Mathf.Max(0.001f, distance) * dt * 10;
                gems[i].root.position = gems[i].position + Vector3.up * (0.3f + Mathf.Sin(Elapsed * 4 + i) * 0.09f);
                gems[i].root.Rotate(0, dt * 80, 0);
                if (distance < 0.9f)
                {
                    gems[i].active = false; gems[i].root.gameObject.SetActive(false); GainExperience(1);
                }
            }
            if (experiment == null || !experiment.DrivesOrbit || Elapsed < experiment.activationTime)
                UpdateOrbitPositions(Elapsed);
            if (Elapsed > orbitDamageAt)
            {
                orbitDamageAt = Elapsed + 0.32f;
                for (int j = 0; j < enemies.Length; j++)
                    if (enemies[j].active && (enemies[j].position - player.position).sqrMagnitude < 9)
                        DamageEnemy(j, 9 + Level * 2);
            }
        }

        public void UpdateOrbitPositions(float simulationTime)
        {
            for (int i = 0; i < blades.Length; i++)
            {
                float angle = simulationTime * 2 + i * Mathf.PI * 2 / blades.Length;
                LastOrbitPositions[i] = player.position + new Vector3(Mathf.Cos(angle) * 2.7f, 0.9f, Mathf.Sin(angle) * 2.7f);
                blades[i].position = LastOrbitPositions[i];
                blades[i].rotation = Quaternion.Euler(0, -angle * Mathf.Rad2Deg, 35);
            }
        }
        public void ApplyOrbitResults(Vector3[] positions)
        {
            for (int i = 0; i < Mathf.Min(blades.Length, positions.Length); i++)
            {
                LastOrbitPositions[i] = positions[i]; blades[i].position = positions[i];
                blades[i].rotation = Quaternion.Euler(0, -(Elapsed * 2 + i * Mathf.PI * 2 / blades.Length) * Mathf.Rad2Deg, 35);
            }
        }

        void SpawnEnemy()
        {
            for (int i = 0; i < enemies.Length; i++)
            {
                if (enemies[i].active || enemies[i].dying > 0) continue;
                Vector3 position;
                if (SpawnCandidates != null && SpawnCandidates.Length > 0)
                    position = SpawnCandidates[random.Next(SpawnCandidates.Length)];
                else
                {
                    float angle = (float)random.NextDouble() * Mathf.PI * 2;
                    position = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 17f;
                }
                enemies[i].position = position;
                enemies[i].hp = 24 + Elapsed * 0.08f; enemies[i].flash = 0;
                enemies[i].active = true; enemies[i].root.localScale = Vector3.one;
                enemies[i].body.SetPropertyBlock(clearBlock);
                enemies[i].root.position = position;
                enemies[i].root.gameObject.SetActive(true);
                ActiveEnemies++; TotalSpawned++; return;
            }
        }

        void Fire()
        {
            int target = -1;
            float best = 160;
            for (int i = 0; i < enemies.Length; i++)
            {
                if (!enemies[i].active) continue;
                float d = (enemies[i].position - player.position).sqrMagnitude;
                if (d < best) { best = d; target = i; }
            }
            if (target < 0) return;
            Vector3 dir = (enemies[target].position - player.position).normalized;
            for (int i = 0; i < bolts.Length; i++)
            {
                if (bolts[i].active) continue;
                bolts[i].active = true; bolts[i].position = player.position;
                bolts[i].velocity = dir * 23; bolts[i].life = 1.1f; bolts[i].damage = 20 + Level * 3;
                bolts[i].root.position = player.position + Vector3.up * 0.8f;
                bolts[i].root.rotation = Quaternion.LookRotation(dir);
                bolts[i].root.gameObject.SetActive(true); ShotsFired++; return;
            }
        }

        void DamageEnemy(int i, float damage)
        {
            if (!enemies[i].active) return;
            enemies[i].hp -= damage;
            if (enemies[i].hp > 0)
            {
                enemies[i].flash = 0.09f; enemies[i].body.SetPropertyBlock(hitBlock);
                return;
            }
            Vector3 at = enemies[i].position;
            enemies[i].active = false; enemies[i].dying = 0.18f;
            ActiveEnemies--; Kills++;
            for (int j = 0; j < gems.Length; j++)
            {
                if (gems[j].active) continue;
                gems[j].active = true; gems[j].position = at;
                gems[j].root.position = at + Vector3.up * 0.3f;
                gems[j].root.gameObject.SetActive(true); break;
            }
        }

        public void CastNova()
        {
            NovaCooldown = 8;
            if (nova != null)
            {
                nova.position = player.position + Vector3.up * 0.15f;
                nova.localScale = Vector3.one * 0.5f;
                nova.gameObject.SetActive(true);
            }
            for (int i = 0; i < enemies.Length; i++)
                if (enemies[i].active && (enemies[i].position - player.position).sqrMagnitude < 64)
                    DamageEnemy(i, 60 + Level * 5);
        }

        public void TakeDamage(float damage)
        {
            if (!testInvulnerable) Health = Mathf.Max(0, Health - Mathf.Max(0, damage));
            hud.FlashDamage();
            if (Health <= 0) { Finished = true; Victory = false; }
        }

        public void GainExperience(int value)
        {
            Experience += value;
            while (Experience >= NextLevelExperience)
            {
                Experience -= NextLevelExperience; Level++;
                Health = Mathf.Min(100, Health + 12);
            }
        }

        void SwitchScene(int offset)
        {
            int count = SceneManager.sceneCountInBuildSettings;
            if (count > 0) SceneManager.LoadScene((SceneManager.GetActiveScene().buildIndex + offset + count) % count);
        }

        [Serializable] public class Report
        {
            public string scene; public int frames, samples, kills, level, enemies, spawned, shots;
            public float elapsed, health, medianMs, p95Ms;
            public double experimentMs; public bool finished, victory, autoDemo;
        }
        public string Snapshot()
        {
            float[] sorted = new float[sampleCount];
            Array.Copy(frameSamples, sorted, sampleCount);
            Array.Sort(sorted);
            return JsonUtility.ToJson(new Report {
                scene = SceneManager.GetActiveScene().name, frames = Time.frameCount, samples = sampleCount,
                elapsed = Elapsed, health = Health, kills = Kills, level = Level, enemies = ActiveEnemies,
                spawned = TotalSpawned, shots = ShotsFired, finished = Finished, victory = Victory, autoDemo = AutoDemo,
                medianMs = sampleCount == 0 ? 0 : sorted[sampleCount / 2],
                p95Ms = sampleCount == 0 ? 0 : sorted[Mathf.Min(sampleCount - 1, Mathf.FloorToInt(sampleCount * 0.95f))],
                experimentMs = ExperimentWorkMs });
        }

        void OnDestroy()
        {
            if (!started) return;
            Application.targetFrameRate = originalTarget;
            QualitySettings.vSyncCount = originalVSync;
            Application.runInBackground = originalBackground;
        }
    }
}
