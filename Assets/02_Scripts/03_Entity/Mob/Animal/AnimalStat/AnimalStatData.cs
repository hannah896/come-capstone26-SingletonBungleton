using UnityEngine;

[CreateAssetMenu(fileName = "AnimalStatData", menuName = "Scriptable Objects/AnimalStatData")]
public class AnimalStatData : EntityStatData
{
    [Header("배회")]
    [Tooltip("Idle 상태로 머무는 시간(초). 끝나면 주변 랜덤 위치로 걸어간다")]
    public float IdleDuration = 5f;
    [Tooltip("현재 위치 기준 배회 목적지를 고르는 반경(m)")]
    public float WanderRadius = 8f;

    [Header("도망")]
    [Tooltip("공격한 플레이어와 이 거리 이상 벌어지면 도망을 멈춘다(최소 도망 시간이 지난 뒤)")]
    public float FleeRange = 12f;
    [Tooltip("피격 후 최소한 이 시간(초)은 무조건 도망친다")]
    public float MinFleeDuration = 4f;
    [Tooltip("도망칠 때 이동 속도. 걷기는 MoveSpeed를 쓴다")]
    public float RunSpeed = 6f;

    [Header("피격 넉백")]
    [Tooltip("피격 시 위로 튀어 오르는 속도(m/s). 질량과 무관하게 적용된다")]
    public float KnockbackUpForce = 4f;
    [Tooltip("피격 시 공격자 반대 방향으로 밀리는 속도(m/s)")]
    public float KnockbackBackForce = 3f;
}
