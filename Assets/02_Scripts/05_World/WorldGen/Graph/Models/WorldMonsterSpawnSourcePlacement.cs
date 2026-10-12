/// <summary>
/// 배치된 스폰 오브젝트와 프리팹에서 읽은 생성/배치 설정을 연결하는 논리 데이터입니다.
/// 실제 GameObject가 로드되지 않은 청크의 스폰도 중앙 디렉터가 관리합니다.
/// </summary>
public sealed class WorldMonsterSpawnSourcePlacement
{
    public DisposeData Placement;
    public MonsterSpawnSourceDefinition Definition;
}
