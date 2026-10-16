using System;
using System.Collections.Generic;
using System.Linq;
using OptimizationLab;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class LabSceneBuilder
{
    const string Root = "Assets/03.Thread_Bottleneck";
    static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
    static readonly Color Gold = new Color(0.85f, 0.68f, 0.39f);
    static readonly Color Pale = new Color(0.91f, 0.91f, 0.86f);
    static TMP_FontAsset font;
    static Mesh cone, ring;
    static LabGameSettings settings;
    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        Folder(path.Substring(0, slash));
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }

    public static string BuildTemplate()
    {
        string scenePath = Root + "/Scenes/00_Template.unity";
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null)
            throw new InvalidOperationException("Template exists; refusing to overwrite it. Edit the common prefab or use explicit rebuild.");
        if (SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("The current scene has unsaved edits. Save it before building.");
        foreach (string name in new[] { "Scenes", "Prefabs", "Materials", "Meshes", "Settings", "Documentation" })
            Folder(Root + "/" + name);
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (font == null) throw new InvalidOperationException("TMP essentials must finish importing first.");
        font.atlasPopulationMode = AtlasPopulationMode.Static;
        cone = SaveMesh(MakeCone(8), "Cone");
        ring = SaveMesh(MakeRing(64, 0.93f, 1), "Ring");
        settings = ScriptableObject.CreateInstance<LabGameSettings>();
        AssetDatabase.CreateAsset(settings, Root + "/Settings/GameSettings.asset");
        CreateActorAssets();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientLight = new Color(0.25f, 0.26f, 0.35f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.045f, 0.052f, 0.075f);
        RenderSettings.fogStartDistance = 32;
        RenderSettings.fogEndDistance = 70;
        var root = new GameObject("GameRoot");
        var game = root.AddComponent<SurvivorGame>();
        game.settings = settings;
        game.actorRoot = Child("RuntimeActors", root.transform);
        CreateEnvironment(root.transform);
        game.player = (Transform)InstantiatePrefab(settings.playerPrefab, root.transform);
        game.player.name = "Player";
        game.orbitRoot = Child("OrbitingWeapons", root.transform);
        var cam = Child("Main Camera", root.transform).gameObject.AddComponent<Camera>();
        cam.tag = "MainCamera";
        cam.orthographic = true;
        cam.orthographicSize = 12.5f;
        cam.transform.position = new Vector3(0, 23, -18);
        cam.transform.rotation = Quaternion.Euler(52, 0, 0);
        cam.backgroundColor = RenderSettings.fogColor;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.farClipPlane = 100;
        cam.allowHDR = false;
        cam.gameObject.AddComponent<AudioListener>();
        game.viewCamera = cam;
        var light = Child("Moonlight", root.transform).gameObject.AddComponent<Light>();
        light.type = LightType.Directional; light.intensity = 1;
        light.transform.rotation = Quaternion.Euler(55, -35, 0);
        light.shadows = LightShadows.None;
        Shape("NovaPulse", root.transform, ring, new Vector3(0, 0.1f, 0), Vector3.one,
            Mat("Nova", new Color(0.2f, 0.75f, 0.65f), new Color(0.08f, 0.2f, 0.15f))).gameObject.SetActive(false);
        game.hud = CreateHud(root.transform);
        var eventSystem = Child("EventSystem", root.transform).gameObject;
        eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
        eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, Root + "/Prefabs/GameRoot.prefab", InteractionMode.AutomatedAction);
        EditorSceneManager.SaveScene(scene, scenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        return "Created playable Template and shared GameRoot/actor prefabs: " + prefab.name;
    }

    public static string CreateExperimentScenes()
    {
        string template = Root + "/Scenes/00_Template.unity";
        if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save scene edits first.");
        string[] names = {
            "01_MainThread_Problem", "01_MainThread_Improved",
            "02_WorkerThread_Problem", "02_WorkerThread_Improved",
            "03_RenderThread_Problem", "03_RenderThread_Improved",
            "04_GPU_Problem", "04_GPU_Improved" };
        Type[] types = { typeof(MainThreadProblem), typeof(MainThreadImproved),
            typeof(WorkerThreadProblem), typeof(WorkerThreadImproved),
            typeof(RenderThreadProblem), typeof(RenderThreadImproved),
            typeof(GpuProblem), typeof(GpuImproved) };
        foreach (string name in names)
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Root + "/Scenes/" + name + ".unity") != null)
                throw new InvalidOperationException("Scene already exists; refusing to overwrite: " + name);
        var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(template, true) };
        for (int i = 0; i < names.Length; i++)
        {
            string path = Root + "/Scenes/" + names[i] + ".unity";
            if (!AssetDatabase.CopyAsset(template, path)) throw new Exception("Could not copy Template: " + path);
            var scene = EditorSceneManager.OpenScene(path);
            var game = UnityEngine.Object.FindFirstObjectByType<SurvivorGame>();
            GameObject host = i >= 6 ? game.viewCamera.gameObject : new GameObject("Experiment");
            if (i < 6) host.transform.SetParent(game.transform, false);
            host.AddComponent(types[i]);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            scenes.Add(new EditorBuildSettingsScene(path, true));
        }
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(template);
        return "Copied Template to eight Problem/Improved scenes; shared prefab instances preserved.";
    }

    static Transform InstantiatePrefab(GameObject prefab, Transform parent)
    {
        return ((GameObject)PrefabUtility.InstantiatePrefab(prefab, parent)).transform;
    }
    static Transform Child(string name, Transform parent)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform;
    }
    static Mesh SaveMesh(Mesh mesh, string name)
    {
        mesh.name = name; AssetDatabase.CreateAsset(mesh, Root + "/Meshes/" + name + ".asset"); return mesh;
    }
    static Mesh PrimitiveMesh(PrimitiveType type)
    {
        var go = GameObject.CreatePrimitive(type);
        Mesh mesh = go.GetComponent<MeshFilter>().sharedMesh;
        UnityEngine.Object.DestroyImmediate(go);
        return mesh;
    }
    static Material Mat(string name, Color color, Color? glow = null)
    {
        if (materials.TryGetValue(name, out var material)) return material;
        string path = Root + "/Materials/" + name + ".mat";
        material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("OptimizationLab/Stylized"));
            material.SetColor("_Color", color); material.SetColor("_EmissionColor", glow ?? Color.black);
            AssetDatabase.CreateAsset(material, path);
        }
        materials[name] = material; return material;
    }
    static Transform Shape(string name, Transform parent, Mesh mesh, Vector3 position, Vector3 scale, Material material)
    {
        var child = Child(name, parent);
        child.localPosition = position; child.localScale = scale;
        child.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = child.gameObject.AddComponent<MeshRenderer>(); r.sharedMaterial = material;
        r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        return child;
    }
    static GameObject SaveActor(GameObject actor, string name)
    {
        var result = PrefabUtility.SaveAsPrefabAsset(actor, Root + "/Prefabs/" + name + ".prefab");
        UnityEngine.Object.DestroyImmediate(actor); return result;
    }

    static void CreateActorAssets()
    {
        Mesh sphere = PrimitiveMesh(PrimitiveType.Sphere), capsule = PrimitiveMesh(PrimitiveType.Capsule);
        Mesh cube = PrimitiveMesh(PrimitiveType.Cube), cylinder = PrimitiveMesh(PrimitiveType.Cylinder);
        var cyan = Mat("SoulGlow", new Color(0.18f, 0.8f, 0.7f), new Color(0.08f, 0.45f, 0.35f));
        var gold = Mat("AntiqueGold", Gold);
        var dark = Mat("Ink", new Color(0.035f, 0.035f, 0.07f));
        var robe = Mat("WardenRobe", new Color(0.29f, 0.24f, 0.48f));
        var skin = Mat("Bone", new Color(0.77f, 0.78f, 0.7f));
        var player = new GameObject("Player");
        Shape("Cloak", player.transform, cone, new Vector3(0, 0.05f, 0), new Vector3(0.9f, 1.25f, 0.9f), robe);
        Shape("Head", player.transform, sphere, new Vector3(0, 1.25f, 0.06f), Vector3.one * 0.48f, dark);
        Shape("Hood", player.transform, cone, new Vector3(0, 1.25f, -0.05f), new Vector3(0.57f, 0.75f, 0.57f), robe);
        Shape("Face", player.transform, cube, new Vector3(0, 1.31f, 0.22f), new Vector3(0.25f, 0.12f, 0.07f), cyan);
        Shape("Staff", player.transform, cylinder, new Vector3(0.64f, 0.9f, 0), new Vector3(0.045f, 0.95f, 0.045f), gold);
        Shape("SoulLantern", player.transform, sphere, new Vector3(0.64f, 1.94f, 0), Vector3.one * 0.28f, cyan);
        Shape("SelectionRing", player.transform, ring, new Vector3(0, 0.04f, 0), Vector3.one * 0.82f, cyan);
        settings.playerPrefab = SaveActor(player, "Player");

        var enemy = new GameObject("Enemy");
        Shape("Body", enemy.transform, cone, new Vector3(0, 0.06f, 0), new Vector3(0.7f, 0.9f, 0.7f),
            Mat("Wraith", new Color(0.43f, 0.16f, 0.24f)));
        Shape("Skull", enemy.transform, sphere, new Vector3(0, 0.92f, 0.06f), new Vector3(0.43f, 0.48f, 0.4f), skin);
        var eyes = Mat("BloodGlow", new Color(0.9f, 0.18f, 0.17f), new Color(0.55f, 0.05f, 0.03f));
        Shape("EyeL", enemy.transform, cube, new Vector3(-0.09f, 0.98f, 0.24f), new Vector3(0.09f, 0.05f, 0.05f), eyes);
        Shape("EyeR", enemy.transform, cube, new Vector3(0.09f, 0.98f, 0.24f), new Vector3(0.09f, 0.05f, 0.05f), eyes);
        settings.enemyPrefab = SaveActor(enemy, "Enemy");

        var bolt = new GameObject("SoulBolt");
        Shape("Core", bolt.transform, sphere, Vector3.zero, new Vector3(0.19f, 0.19f, 0.65f), cyan);
        Shape("Trail", bolt.transform, cone, new Vector3(0, 0, -0.4f), new Vector3(0.17f, 0.75f, 0.17f), cyan)
            .localRotation = Quaternion.Euler(90, 0, 0);
        settings.projectilePrefab = SaveActor(bolt, "SoulBolt");
        var gem = new GameObject("Experience");
        var shard = Shape("Crystal", gem.transform, cube, Vector3.zero, new Vector3(0.19f, 0.3f, 0.19f),
            Mat("ExperienceGold", new Color(0.95f, 0.77f, 0.3f), new Color(0.2f, 0.14f, 0.02f)));
        shard.localRotation = Quaternion.Euler(0, 45, 35);
        settings.gemPrefab = SaveActor(gem, "Experience");
        var blade = new GameObject("OrbitBlade");
        Shape("Blade", blade.transform, cube, Vector3.zero, new Vector3(0.13f, 0.09f, 0.95f), skin);
        Shape("Guard", blade.transform, cube, new Vector3(0, 0, -0.25f), new Vector3(0.4f, 0.12f, 0.12f), gold);
        Shape("Soul", blade.transform, sphere, new Vector3(0, 0, 0.25f), Vector3.one * 0.2f, cyan);
        settings.orbitPrefab = SaveActor(blade, "OrbitBlade");
        settings.crowdMesh = cone;
        settings.crowdMaterial = Mat("CrowdWraith", new Color(0.52f, 0.19f, 0.24f));
        settings.crowdMaterial.enableInstancing = true; // Keep the instancing variant in Player builds.
        settings.ultimateMaterial = new Material(Shader.Find("OptimizationLab/Ultimate"));
        AssetDatabase.CreateAsset(settings.ultimateMaterial, Root + "/Materials/Ultimate.mat");
    }

    static void CreateEnvironment(Transform parent)
    {
        var env = Child("CryptArena", parent);
        Mesh cube = PrimitiveMesh(PrimitiveType.Cube), cylinder = PrimitiveMesh(PrimitiveType.Cylinder);
        var random = new System.Random(41);
        Material[] floor = Enumerable.Range(0, 5).Select(i =>
            Mat("Slate" + i, new Color(0.14f + i * 0.01f, 0.16f + i * 0.01f, 0.19f + i * 0.012f))).ToArray();
        for (int x = -11; x <= 11; x++)
            for (int z = -11; z <= 11; z++)
            {
                var tile = Shape("Flagstone", env, cube, new Vector3(x * 2, -0.2f, z * 2),
                    new Vector3(1.96f, 0.32f, 1.96f), floor[random.Next(floor.Length)]);
            }
        var stone = Mat("RuinedStone", new Color(0.23f, 0.24f, 0.29f));
        var trim = Mat("RuneStone", new Color(0.12f, 0.17f, 0.19f));
        var soul = Mat("ArenaSoul", new Color(0.12f, 0.55f, 0.5f), new Color(0.04f, 0.19f, 0.17f));
        for (int i = 0; i < 32; i++)
        {
            float angle = i * Mathf.PI * 2 / 32;
            Vector3 p = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 20.5f;
            Shape("Boundary", env, cube, p + Vector3.up * 0.2f, new Vector3(1.4f, 0.8f, 1.4f), stone)
                .localRotation = Quaternion.Euler(0, -angle * Mathf.Rad2Deg, 0);
            if (i % 4 == 0)
            {
                Shape("Pillar", env, cylinder, p + Vector3.up * 1.2f, new Vector3(0.6f, 1.7f, 0.6f), stone);
                Shape("Brazier", env, cone, p + Vector3.up * 2.95f, new Vector3(0.6f, 0.7f, 0.6f), soul);
            }
        }
        for (int i = 0; i < 38; i++)
        {
            float x = (float)random.NextDouble() * 38 - 19;
            float z = (float)random.NextDouble() * 38 - 19;
            if (x * x + z * z < 35) continue;
            var slab = Shape("Gravestone", env, cube, new Vector3(x, 0.23f, z),
                new Vector3(0.55f, 0.6f, 0.23f), stone);
            slab.localRotation = Quaternion.Euler(0, random.Next(360), random.Next(-15, 15));
        }
        for (int i = 0; i < 3; i++)
            Shape("SummoningCircle", env, ring, new Vector3(0, 0.005f + i * 0.002f, 0),
                Vector3.one * (3.5f + i * 0.3f), i == 1 ? soul : trim);
        MergeEnvironment(env);
    }

    static void MergeEnvironment(Transform env)
    {
        var groups = new Dictionary<Material, List<CombineInstance>>();
        var filters = env.GetComponentsInChildren<MeshFilter>();
        foreach (var filter in filters)
        {
            var material = filter.GetComponent<MeshRenderer>().sharedMaterial;
            if (!groups.ContainsKey(material)) groups[material] = new List<CombineInstance>();
            groups[material].Add(new CombineInstance {
                mesh = filter.sharedMesh, transform = env.worldToLocalMatrix * filter.transform.localToWorldMatrix });
        }
        foreach (var group in groups)
        {
            var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(group.Value.ToArray());
            mesh = SaveMesh(mesh, "Arena_" + group.Key.name);
            Shape("Static_" + group.Key.name, env, mesh, Vector3.zero, Vector3.one, group.Key);
        }
        foreach (var filter in filters) UnityEngine.Object.DestroyImmediate(filter.gameObject);
    }

    static Mesh MakeCone(int sides)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        for (int i = 0; i < sides; i++)
        {
            float a = i * Mathf.PI * 2 / sides, b = (i + 1) * Mathf.PI * 2 / sides;
            Vector3 p = new Vector3(Mathf.Cos(a) * 0.5f, 0, Mathf.Sin(a) * 0.5f);
            Vector3 q = new Vector3(Mathf.Cos(b) * 0.5f, 0, Mathf.Sin(b) * 0.5f);
            int n = vertices.Count;
            vertices.Add(p); vertices.Add(Vector3.up); vertices.Add(q);
            vertices.Add(p); vertices.Add(q); vertices.Add(Vector3.zero);
            for (int j = 0; j < 6; j++) triangles.Add(n + j);
        }
        var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }
    static Mesh MakeRing(int segments, float inner, float outer)
    {
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2 / segments;
            Vector3 d = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            vertices.Add(d * inner); vertices.Add(d * outer);
            if (i == segments) continue;
            int n = i * 2;
            triangles.AddRange(new[] { n, n + 2, n + 1, n + 1, n + 2, n + 3 });
        }
        var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); return mesh;
    }

    static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 size, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor; rect.pivot = anchor;
        rect.sizeDelta = size; rect.anchoredPosition = position;
        return rect;
    }
    static UnityEngine.UI.Image Panel(string name, Transform parent, Vector2 anchor, Vector2 size, Vector2 pos, Color color)
    {
        var rect = Rect(name, parent, anchor, size, pos);
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.color = color; image.raycastTarget = false; return image;
    }
    static TextMeshProUGUI Label(string name, Transform parent, string text, Vector2 anchor, Vector2 size, Vector2 pos, float point, Color color, TextAlignmentOptions align = TextAlignmentOptions.Left)
    {
        var rect = Rect(name, parent, anchor, size, pos);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font; label.text = text; label.fontSize = point; label.color = color;
        label.alignment = align; label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }
    static UnityEngine.UI.Image Bar(string name, Transform parent, Vector2 anchor, Vector2 size, Vector2 pos, Color color)
    {
        var image = Panel(name, parent, anchor, size, pos, color);
        image.type = UnityEngine.UI.Image.Type.Filled;
        image.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
        // Filled Images require a sprite; the built-in white sprite avoids importing artwork.
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        return image;
    }
    static LabHud CreateHud(Transform parent)
    {
        var canvasGo = new GameObject("HUD", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        canvasGo.transform.SetParent(parent, false);
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
        var hud = canvasGo.AddComponent<LabHud>();
        Color panel = new Color(0.035f, 0.04f, 0.06f, 0.94f);
        var top = Panel("TopBand", canvasGo.transform, new Vector2(0.5f, 1), new Vector2(1920, 116), Vector2.zero, panel);
        Label("Brand", top.transform, "N I G H T F A L L", new Vector2(0, 1), new Vector2(260, 48), new Vector2(38, -18), 25, Gold);
        Label("Subtitle", top.transform, "S U R V I V O R S", new Vector2(0, 1), new Vector2(260, 30), new Vector2(40, -65), 15, Pale);
        Panel("HealthTrack", top.transform, new Vector2(0, 1), new Vector2(305, 12), new Vector2(335, -57), new Color(0.2f, 0.12f, 0.17f));
        hud.healthFill = Bar("Health", top.transform, new Vector2(0, 1), new Vector2(305, 12), new Vector2(335, -57), new Color(0.78f, 0.26f, 0.32f));
        Label("Warden", top.transform, "THE LAST WARDEN", new Vector2(0, 1), new Vector2(320, 28), new Vector2(335, -22), 17, Pale);
        hud.healthText = Label("HealthNumber", top.transform, "100 / 100", new Vector2(0, 1), new Vector2(305, 25), new Vector2(335, -78), 15, Pale);
        hud.timerText = Label("Timer", top.transform, "00:00", new Vector2(0.5f, 1), new Vector2(260, 57), new Vector2(0, -12), 44, Pale, TextAlignmentOptions.Center);
        Label("Objective", top.transform, "S U R V I V E   U N T I L   D A W N", new Vector2(0.5f, 1), new Vector2(400, 28), new Vector2(0, -76), 13, Gold, TextAlignmentOptions.Center);
        hud.levelText = Label("Level", top.transform, "LV. 1", new Vector2(1, 1), new Vector2(150, 35), new Vector2(-240, -28), 25, Gold, TextAlignmentOptions.Right);
        hud.killsText = Label("Kills", top.transform, "0 SLAIN", new Vector2(1, 1), new Vector2(200, 40), new Vector2(-40, -27), 23, Pale, TextAlignmentOptions.Right);
        Panel("XPTrack", top.transform, new Vector2(0.5f, 0), new Vector2(1840, 4), new Vector2(0, 0), new Color(0.2f, 0.23f, 0.26f));
        hud.xpFill = Bar("ExperienceBar", top.transform, new Vector2(0, 0), new Vector2(1840, 4), new Vector2(40, 0), Gold);
        hud.sceneText = Label("SceneLabel", canvasGo.transform, "TEMPLATE", new Vector2(0, 1), new Vector2(900, 35), new Vector2(40, -135), 16, new Color(0.65f, 0.75f, 0.76f));
        var bottom = Panel("BottomBand", canvasGo.transform, new Vector2(0.5f, 0), new Vector2(1920, 90), Vector2.zero, panel);
        var spells = Rect("SpellSlots", bottom.transform, new Vector2(0, 0.5f), new Vector2(520, 64), new Vector2(40, 0));
        var layout = spells.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        layout.spacing = 12; layout.childControlWidth = false; layout.childControlHeight = false;
        layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
        for (int i = 0; i < 3; i++)
        {
            var slot = Panel("Spell" + i, spells, Vector2.zero, new Vector2(150, 56), Vector2.zero, new Color(0.09f, 0.12f, 0.14f));
            Label("SpellLabel", slot.transform, new[] { "I   SOUL BOLT", "II   ORBIT", "III   NOVA" }[i], new Vector2(0.5f, 0.5f), new Vector2(145, 50), Vector2.zero, 16, Gold, TextAlignmentOptions.Center);
        }
        hud.novaText = Label("NovaHint", bottom.transform, "SPACE   SOUL NOVA / READY", new Vector2(0.5f, 0.5f), new Vector2(570, 40), Vector2.zero, 18, Pale, TextAlignmentOptions.Center);
        hud.modeText = Label("DemoMode", bottom.transform, "F2  AUTO PILOT OFF", new Vector2(1, 0.5f), new Vector2(310, 45), new Vector2(-40, 5), 16, Gold, TextAlignmentOptions.Right);
        Label("Controls", canvasGo.transform, "WASD / ARROWS  MOVE      SPACE  NOVA      R  RESTART", new Vector2(0.5f, 0), new Vector2(900, 35), new Vector2(0, 108), 16, new Color(0.7f, 0.75f, 0.75f), TextAlignmentOptions.Center);
        hud.diagnosticPanel = Panel("Diagnostics", canvasGo.transform, new Vector2(1, 1), new Vector2(390, 115), new Vector2(-40, -140), panel).gameObject;
        hud.diagnosticText = Label("Stats", hud.diagnosticPanel.transform, "", new Vector2(0.5f, 0.5f), new Vector2(365, 105), Vector2.zero, 17, Pale);
        hud.diagnosticPanel.SetActive(false);
        hud.damageFlash = Panel("DamageFlash", canvasGo.transform, new Vector2(0.5f, 0.5f), new Vector2(3000, 2000), Vector2.zero, Color.clear);
        hud.endPanel = Panel("EndOverlay", canvasGo.transform, new Vector2(0.5f, 0.5f), new Vector2(3000, 2000), Vector2.zero, new Color(0.02f, 0.025f, 0.04f, 0.88f)).gameObject;
        hud.endTitle = Label("EndTitle", hud.endPanel.transform, "", new Vector2(0.5f, 0.5f), new Vector2(1400, 140), new Vector2(0, 60), 50, Gold, TextAlignmentOptions.Center);
        hud.endDetails = Label("EndDetails", hud.endPanel.transform, "", new Vector2(0.5f, 0.5f), new Vector2(900, 130), new Vector2(0, -75), 25, Pale, TextAlignmentOptions.Center);
        hud.endPanel.SetActive(false);
        return hud;
    }
}
