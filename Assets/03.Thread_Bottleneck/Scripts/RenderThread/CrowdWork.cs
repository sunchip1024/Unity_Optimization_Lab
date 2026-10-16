using UnityEngine;
using UnityEngine.Rendering;
namespace OptimizationLab
{
    public abstract class CrowdWork : LabExperiment
    {
        [Range(100, 40000)] public int crowdCount = 40000;
        protected Matrix4x4[] matrices;
        protected Material material;
        protected Mesh mesh;
        protected CommandBuffer commands;
        Camera renderCamera;
        bool attached;
        protected bool initialized;
        public override void Initialize(SurvivorGame game)
        {
            mesh = game.settings.crowdMesh;
            renderCamera = game.viewCamera;
            commands = new CommandBuffer { name = "Lab Late-Wave Draw Commands" };
            material = new Material(game.settings.crowdMaterial);
            matrices = new Matrix4x4[crowdCount];
            var random = new System.Random(game.settings.seed + 9);
            for (int i = 0; i < matrices.Length; i++)
            {
                float x = (float)random.NextDouble() * 36 - 18;
                float z = (float)random.NextDouble() * 28 - 14;
                if (x * x + z * z < 16) x += x < 0 ? -5 : 5;
                matrices[i] = Matrix4x4.TRS(new Vector3(x, 0.05f, z),
                    Quaternion.Euler(0, (float)random.NextDouble() * 360, 0), Vector3.one * 0.32f);
            }
            Prepare(game);
            initialized = true;
        }
        protected abstract void Prepare(SurvivorGame game);
        protected void Show()
        {
            if (attached) return;
            renderCamera.AddCommandBuffer(CameraEvent.BeforeForwardAlpha, commands);
            attached = true;
        }
        public override void ResetExperiment()
        {
            if (attached && renderCamera != null) renderCamera.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha, commands);
            attached = false;
        }
        protected virtual void OnDestroy()
        {
            ResetExperiment();
            if (commands != null) commands.Release();
            if (material != null) Destroy(material);
        }
    }
}
