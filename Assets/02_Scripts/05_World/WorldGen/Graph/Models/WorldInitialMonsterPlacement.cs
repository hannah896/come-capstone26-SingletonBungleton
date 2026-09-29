using UnityEngine;

/// <summary>월드 시드로 결정된 최초 몬스터 배치. 생존 여부는 별도의 저장 상태에 둡니다.</summary>
public sealed class WorldInitialMonsterPlacement
{
    public int id;
    public string monsterKey;
    public Vector2Int tile;
}
