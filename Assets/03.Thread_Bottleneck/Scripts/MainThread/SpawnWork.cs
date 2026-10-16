using System.Threading;
using UnityEngine;
using Unity.Profiling;
namespace OptimizationLab
{
    public abstract class SpawnWork : LabExperiment
    {
        [Range(0, 2000)] public int delayMilliseconds = 30;
        [Tooltip("Changing this invalidates the improved cache.")] public float mapRadius = 17;
        public int BuildCount { get; private set; }
        protected Vector3[] points = new Vector3[32];
        protected float cachedRadius = -1;
        static readonly ProfilerMarker marker = new ProfilerMarker("Lab.Main.BuildSpawnPoints");
        protected void Build(SurvivorGame game)
        {
            using (marker.Auto())
            {
                // 고정 맵의 스폰 후보 생성이 오래 걸리는 상황을 단순화한다.
                // Sleep은 CPU 연산량이 아닌 작업 지연 모형이다.
                Thread.Sleep(delayMilliseconds);
                for (int i = 0; i < points.Length; i++)
                {
                    float angle = i * Mathf.PI * 2 / points.Length;
                    points[i] = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * mapRadius;
                }
                cachedRadius = mapRadius; BuildCount++;
                game.SpawnCandidates = points;
            }
        }
        public override void ResetExperiment() { cachedRadius = -1; BuildCount = 0; }
    }
}
