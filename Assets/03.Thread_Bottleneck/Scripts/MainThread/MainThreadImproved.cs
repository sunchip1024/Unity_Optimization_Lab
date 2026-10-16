namespace OptimizationLab
{
    public sealed class MainThreadImproved : SpawnWork
    {
        public override string Description => "Reuse spawn points until map input changes.";
        protected override void Tick(SurvivorGame game)
        {
            // 개선: 처음 또는 입력 변경 때만 갱신하고 같은 결과를 재사용한다.
            if (cachedRadius != mapRadius) Build(game);
        }
    }
}
