using UnityEngine;

[CreateAssetMenu(fileName = "MonsterStatData", menuName = "Scriptable Objects/MonsterStatData")]
public class MonsterStatData : EntityStatData
{
    [Header("인지")]
    [Tooltip("시야각(도). 360 이상이면 전방위 인지")]
    public float FOV = 360f;
    [Tooltip("플레이어를 감지/추적 유지하는 최대 거리")]
    public float DetectRange = 8f;

    [Header("공격")]
    [Tooltip("공격을 시작하는 근접 거리")]
    public float AttackRange = 1.5f;

    [Header("이동")]
    [Tooltip("초당 회전 각도(도). 클수록 타깃 쪽으로 빠르게 몸을 돌린다")]
    public float TurnSpeed = 540f;

    [Header("드롭")]
    [Tooltip("사망 시 떨어뜨릴 드롭 테이블. 스폰 시 스탯과 함께 주입된다")]
    public DropTableSO DropTable;
}
