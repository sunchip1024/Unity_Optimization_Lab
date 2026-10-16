using UnityEngine;
namespace OptimizationLab
{
    [RequireComponent(typeof(Camera))]
    public sealed class GpuProblem : LabExperiment
    {
        [Range(16, 8192)] public int shaderIterations = 8192;
        Material effect;
        bool activeEffect;
        public override string Description => "Full-screen ultimate effect: actual GPU sampling work.";
        public override void Initialize(SurvivorGame game) { effect = new Material(game.settings.ultimateMaterial); }
        protected override void Tick(SurvivorGame game)
        {
            activeEffect = true;
            effect.SetInt("_Iterations", shaderIterations);
        }
        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (activeEffect && effect != null) Graphics.Blit(source, destination, effect);
            else Graphics.Blit(source, destination);
        }
        public override void ResetExperiment() { activeEffect = false; }
        void OnDestroy() { if (effect != null) Destroy(effect); }
    }
}
