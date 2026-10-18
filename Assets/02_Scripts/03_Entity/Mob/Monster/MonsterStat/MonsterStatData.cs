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

    [Header("근접 리듬 (돈스타브 키다리새처럼)")]
    [Tooltip("한 묶음 안에서 근접 공격 사이에 쉬는 시간(초). 개수+1번 연달아 치고 나면 묶음이 끝나고 길게 쉰다. " +
             "비우면 매 공격이 한 묶음(매번 길게 쉼). 묶음 사이 휴식은 종류별 근접 쿨(MinAttackPeriod 등)")]
    public float[] MeleeRhythmGaps = { 0.1f, 0.05f };
    [Tooltip("묶음 사이 휴식에 더하는 무작위 폭(초). 박자가 메트로놈처럼 일정하게 느껴지지 않게 한다")]
    public float MeleeRestJitter = 0.5f;

    [Header("이동")]
    [Tooltip("초당 회전 각도(도). 클수록 타깃 쪽으로 빠르게 몸을 돌린다")]
    public float TurnSpeed = 540f;

    [Header("드롭")]
    [Tooltip("사망 시 떨어뜨릴 드롭 테이블. 스폰 시 스탯과 함께 주입된다")]
    public DropTableSO DropTable;
}
