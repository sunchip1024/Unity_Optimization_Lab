using UnityEngine;
namespace OptimizationLab
{
    [RequireComponent(typeof(Camera))]
    public sealed class GpuImproved : LabExperiment
    {
        public override string Description => "Low-quality preset: skip the expensive ultimate effect.";
        protected override void Tick(SurvivorGame game) { }
        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            // 개선: 저사양 모드에서는 화면 효과를 생략한다. 시각 품질과 성능의 절충.
            Graphics.Blit(source, destination);
        }
    }
}
