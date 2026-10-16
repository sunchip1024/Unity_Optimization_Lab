namespace OptimizationLab
{
    public sealed class WorkerThreadProblem : WeaponJobWork
    {
        public override string Description => "Complete immediately, then do independent work.";
        protected override void Tick(SurvivorGame game)
        {
            Schedule(game);
            using (completeMarker.Auto()) handle.Complete(); // 문제: Main이 바로 기다린다.
            IndependentGameWork();
            Consume(game);
        }
    }
}
