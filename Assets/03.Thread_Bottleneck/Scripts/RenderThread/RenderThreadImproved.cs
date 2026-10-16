using UnityEngine;
namespace OptimizationLab
{
    public sealed class RenderThreadImproved : CrowdWork
    {
        public override string Description => "Same enemies: grouped instanced draw commands.";
        protected override void Prepare(SurvivorGame game)
        {
            material.enableInstancing = true;
            var batch = new Matrix4x4[511];
            // 개선: 같은 메시/머티리얼/변환을 묶어 그린다. 적 수를 줄이지 않는다.
            for (int start = 0; start < matrices.Length; start += batch.Length)
            {
                int count = Mathf.Min(batch.Length, matrices.Length - start);
                System.Array.Copy(matrices, start, batch, 0, count);
                commands.DrawMeshInstanced(mesh, 0, material, 0, batch, count);
            }
        }
        protected override void Tick(SurvivorGame game) { Show(); }
    }
}
