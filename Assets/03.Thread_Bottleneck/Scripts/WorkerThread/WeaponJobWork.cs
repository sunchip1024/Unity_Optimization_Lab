using System.Threading;
using Unity.Collections;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine;
namespace OptimizationLab
{
    public abstract class WeaponJobWork : LabExperiment
    {
        [Range(0, 2000)] public int jobDelayMilliseconds = 12;
        [Range(0, 2000)] public int independentDelayMilliseconds = 12;
        public float ResultChecksum { get; private set; }
        public override bool DrivesOrbit => true;
        protected JobHandle handle;
        NativeArray<Vector3> positions;
        readonly Vector3[] display = new Vector3[3];
        protected static readonly ProfilerMarker completeMarker = new ProfilerMarker("Lab.Worker.Complete");
        static readonly ProfilerMarker independentMarker = new ProfilerMarker("Lab.Main.IndependentWork");
        struct WeaponPositionsJob : IJob
        {
            public int delay;
            public float time;
            public Vector3 center;
            public NativeArray<Vector3> output;
            static readonly ProfilerMarker marker = new ProfilerMarker("Lab.Worker.Work");
            public void Execute()
            {
                using (marker.Auto())
                {
                    // 보조 무기 계산 비용을 지연으로 대체한다. Burst는 사용하지 않는다.
                    Thread.Sleep(delay);
                    for (int i = 0; i < output.Length; i++)
                    {
                        float angle = time * 2 + i * Mathf.PI * 2 / output.Length;
                        output[i] = center + new Vector3(Mathf.Cos(angle) * 2.7f, 0.9f, Mathf.Sin(angle) * 2.7f);
                    }
                }
            }
        }
        public override void Initialize(SurvivorGame game)
        {
            positions = new NativeArray<Vector3>(3, Allocator.Persistent);
        }
        protected void Schedule(SurvivorGame game)
        {
            handle = new WeaponPositionsJob {
                delay = jobDelayMilliseconds, time = game.Elapsed,
                center = game.player.position, output = positions }.Schedule();
            // 두 씬에 동일하게 적용: 작업을 워커가 실행할 수 있도록 전달한다.
            JobHandle.ScheduleBatchedJobs();
        }
        protected void IndependentGameWork()
        {
            using (independentMarker.Auto())
            {
                // 경험치/웨이브 상태 갱신처럼 Job 결과와 독립적인 Main 작업의 지연 모형.
                Thread.Sleep(independentDelayMilliseconds);
            }
        }
        protected void Consume(SurvivorGame game)
        {
            float sum = 0;
            for (int i = 0; i < positions.Length; i++) { display[i] = positions[i]; sum += positions[i].x; }
            ResultChecksum = sum;
            game.ApplyOrbitResults(display);
        }
        public override void ResetExperiment() { handle.Complete(); }
        void OnDisable()
        {
            handle.Complete();
            if (positions.IsCreated) positions.Dispose();
        }
    }
}
