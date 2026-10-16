using UnityEngine;
namespace OptimizationLab
{
    public sealed class RenderThreadProblem : CrowdWork
    {
        public override string Description => "Late-wave enemies: one draw command per enemy.";
        protected override void Prepare(SurvivorGame game)
        {
            material.enableInstancing = false;
            // 문제: 같은 메시를 적 수만큼 개별 DrawMesh 명령으로 제출한다.
            // 준비 단계에서 명령을 기록해 Main의 개별 Renderer/컬링 비용을 분리한다.
            foreach (Matrix4x4 matrix in matrices)
                commands.DrawMesh(mesh, matrix, material, 0, 0);
        }
        protected override void Tick(SurvivorGame game) { Show(); }
    }
}
