using UnityEngine;

/// <summary>만월마다 #1부터 #8까지 한 종류씩 순환시키는 보스 스폰 규칙입니다.</summary>
[CreateAssetMenu(fileName = "BossSpawnRule", menuName = "Scriptable Objects/TestWorld/BossSpawnRule")]
public sealed class BossSpawnRule : ScriptableObject
{
    public const int BossCount = 8;

    [Header("만월 보스 순서")]
    [Tooltip("저장 시 보스 일정을 식별하는 고유 ID. 에셋 이름 변경 후에도 유지하세요.")]
    public string ruleId;

    [Tooltip("Element 0~7에 보스 #1~#8의 MonsterCatalog 키를 순서대로 입력합니다. (만월 번호 - 1) % 8로 선택하며, #8 다음에는 #1로 돌아갑니다. 빈 슬롯은 생성하지 않고 다음 만월까지 예약을 유지합니다. 다른 슬롯으로 대체하거나 순서를 압축하지 않습니다.")]
    public string[] bossMonsterKeys = new string[BossCount];

    [Tooltip("만월 보스를 생성할 수 있는 하루 중 시간대입니다. 달 위상은 만월로 고정됩니다.")]
    public SpawnTimePhaseMask allowedTimePhases = SpawnTimePhaseMask.All;

    [Header("플레이어 주변 생성 위치")]
    [Tooltip("무작위로 선택한 살아 있는 플레이어와 보스 생성 위치 사이의 최소 거리(m)입니다. 만월마다 선택된 보스 한 마리를 생성합니다.")]
    [Min(0f)] public float minDistanceFromPlayer = 25f;

    [Tooltip("선택된 기준 플레이어와 보스 생성 위치 사이의 최대 거리(m)입니다.")]
    [Min(0f)] public float maxDistanceFromPlayer = 40f;

    /// <summary>표시 일차가 만월이면 보스 배열 인덱스(0~7), 아니면 -1을 반환합니다.</summary>
    public static int GetBossIndex(int fullMoonDay)
    {
        if (fullMoonDay < 1 || (fullMoonDay - 1) % WorldClock.MoonCycleDays != (int)MoonPhase.Full)
            return -1;

        return ((fullMoonDay - 1) / WorldClock.MoonCycleDays) % BossCount;
    }

    /// <summary>해당 만월에 지정된 키를 반환합니다. 빈 순서나 만월이 아닌 날짜는 생성 대상이 없습니다.</summary>
    public string GetMonsterKey(int fullMoonDay)
    {
        int index = GetBossIndex(fullMoonDay);
        if (index < 0 || bossMonsterKeys == null || index >= bossMonsterKeys.Length)
            return null;

        string monsterKey = bossMonsterKeys[index];
        return string.IsNullOrWhiteSpace(monsterKey) ? null : monsterKey;
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(ruleId)) ruleId = System.Guid.NewGuid().ToString("N");
        if (bossMonsterKeys == null || bossMonsterKeys.Length != BossCount)
            System.Array.Resize(ref bossMonsterKeys, BossCount);
    }
}
