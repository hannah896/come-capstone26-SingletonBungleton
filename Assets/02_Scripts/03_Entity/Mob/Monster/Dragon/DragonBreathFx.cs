using UnityEngine;

/// <summary>
/// 드래곤 브레스 파티클. 매 프레임(애니메이션 이후) 입 위치로 옮기고 주둥이 방향 + 아래로 숙인 각도로 돌린다.
/// 주둥이 방향은 목 → 머리 벡터라 클립의 좌우 흔들림과 몸통 회전이 그대로 따라온다.
/// 판정과 무관한 연출이며, DragonBoss가 애니메이터 상태를 보고 켜고 끈다(클라도 각자).
/// </summary>
public class DragonBreathFx : MonoBehaviour
{
    [SerializeField] private DragonBoss boss;
    [Tooltip("켜고 끌 파티클 (자식 포함)")]
    [SerializeField] private ParticleSystem[] systems;
    [Tooltip("브레스가 이 거리 앞 땅에 닿도록 아래로 숙인다(m)")]
    [SerializeField] private float aimGroundDistance = 12f;

    private bool emitting;
    private bool subscribed;

    private void Awake()
    {
        if (systems == null || systems.Length == 0)
            systems = GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in systems)
        {
            var main = ps.main;
            main.playOnAwake = false;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        Main.Loop.OnLateUpdate += HandleLateUpdate;
        subscribed = true;
    }

    // 구독 해제는 OnDisable이 아니라 파괴 시점에만 수행한다.
    private void OnDestroy()
    {
        if (subscribed && Main.Instance != null && Main.Loop != null)
            Main.Loop.OnLateUpdate -= HandleLateUpdate;
    }

    public void SetEmitting(bool on)
    {
        if (emitting == on) return;
        emitting = on;
        foreach (var ps in systems)
        {
            if (ps == null) continue;
            if (on) ps.Play(true);
            else ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void HandleLateUpdate(float deltaTime)
    {
        if (!emitting || boss == null || !isActiveAndEnabled) return;

        Vector3 mouth = boss.MouthPosition;
        Vector3 flat = boss.SnoutDirection;
        float drop = Mathf.Max(mouth.y - boss.GroundY, 0f);
        float pitch = Mathf.Atan2(drop, Mathf.Max(aimGroundDistance, 1f)) * Mathf.Rad2Deg;

        Quaternion rot = Quaternion.LookRotation(flat) * Quaternion.Euler(pitch, 0f, 0f);
        transform.SetPositionAndRotation(mouth, rot);
    }
}
