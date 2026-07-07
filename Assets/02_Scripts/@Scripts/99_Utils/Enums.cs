/// <summary>
/// 플레이어 캐릭터 성별 (대기방 선택 / 스폰 프리팹 분기용)
/// </summary>
public enum PlayerCharacter
{
    Female = 0,
    Male = 1,
}

/// <summary>
/// 플레이어가 상호작용 대상에 도달했을 때 수행할 액션 종류
/// </summary>
public enum ActionType
{
    None,
    Pick,       // 채집  — Kevin: HumanM@Gathering01
    Mine,       // 채굴  — Kevin: HumanM@Mining - Begin/Loop Wall/Stop
    Chop,       // 벌목  — Kevin: HumanM@TreeChopping - Begin/Loop/Stop
    Dig,        // 뽑기  — Kevin: HumanM@FarmingWithPlow01_R - Begin/Loop/Stop
    Ignite,     // 점화  — Kevin: HumanM@Opening01 - Begin/Loop/Stop
    Cook,       // 요리  — Kevin: HumanM@Watering01_R
    Inspect,    // 조사  — Kevin: HumanM@Loot01 - Begin/Loop/Stop
    Build,      // 설치  — Kevin: HumanM@HammeringWall01_R - Begin/Loop/Stop
}
