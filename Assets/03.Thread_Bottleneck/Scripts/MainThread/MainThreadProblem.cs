namespace OptimizationLab
{
    public sealed class MainThreadProblem : SpawnWork
    {
        public override string Description => "Spawn points are rebuilt every frame.";
        protected override void Tick(SurvivorGame game)
        {
            Build(game); // 문제: 입력이 같은데 매 프레임 비싼 준비 작업을 반복한다.
        }
    }
}
