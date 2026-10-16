namespace OptimizationLab
{
    public sealed class WorkerThreadImproved : WeaponJobWork
    {
        public override string Description => "Overlap independent Main work with the Job.";
        protected override void Tick(SurvivorGame game)
        {
            Schedule(game);
            IndependentGameWork(); // 개선: 결과와 무관한 일을 먼저 겹쳐 실행한다.
            using (completeMarker.Auto()) handle.Complete();
            Consume(game); // 결과를 실제로 쓰기 직전 동기화한다.
        }
    }
}
