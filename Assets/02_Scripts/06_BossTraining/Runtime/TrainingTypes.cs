namespace SingletonBungleton.BossTraining
{
    /// <summary>스파링 상대(규칙 기반 플레이어)의 행동 성향. 에피소드마다 바뀌어 한 가지 패턴에만 과적합되는 것을 막는다.</summary>
    public enum SparringStyle
    {
        Aggressive,   // 곧장 달려들어 공격
        HitAndRun,    // 한 번 치고 물러남
        Circling,     // 보스 주위를 돌다가 틈을 봐서 파고듦
    }

    public enum EpisodeResult
    {
        BossWon,
        BossLost,
        TimeOut,
    }
}
