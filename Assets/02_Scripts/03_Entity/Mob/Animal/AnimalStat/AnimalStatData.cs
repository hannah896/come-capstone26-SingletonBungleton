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
    [Tooltip("이 거리 안에 플레이어가 있으면 계속 도망친다. 벗어나면 Idle로 복귀")]
    public float FleeRange = 12f;
    [Tooltip("도망칠 때 이동 속도. 걷기는 MoveSpeed를 쓴다")]
    public float RunSpeed = 6f;

    [Header("피격 넉백")]
    [Tooltip("피격 시 위로 튀어 오르는 속도(m/s). 질량과 무관하게 적용된다")]
    public float KnockbackUpForce = 4f;
    [Tooltip("피격 시 공격자 반대 방향으로 밀리는 속도(m/s)")]
    public float KnockbackBackForce = 3f;
}
