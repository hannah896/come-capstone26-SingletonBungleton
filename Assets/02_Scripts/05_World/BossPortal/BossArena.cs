using UnityEngine;

/// <summary>
/// 월문 안 보스 아레나. 바닥/벽 같은 눈에 보이는 구조물은 프리팹에 있고,
/// 이 컴포넌트는 플레이어 입장 위치와 보스 스폰 위치만 알려준다.
/// (MoonGate가 입장 시 모든 피어에서 같은 월드 좌표에 인스턴스화한다)
/// </summary>
public class BossArena : MonoBehaviour
{
    [Tooltip("플레이어 입장 위치. 파티 인원 순서대로 쓰고, 넘치면 처음부터 다시 쓴다.")]
    [SerializeField] private Transform[] playerSpawns;

    [SerializeField] private Transform bossSpawn;

    [Tooltip("이 반경(m) 안에 있으면 '아레나 안'으로 본다 (전멸 판정용)")]
    [SerializeField] private float radius = 16f;

    public float Radius => radius;
    public Vector3 BossSpawn => bossSpawn != null ? bossSpawn.position : transform.position;

    public Vector3 GetPlayerSpawn(int index)
    {
        if (playerSpawns == null || playerSpawns.Length == 0) return transform.position + Vector3.up;
        return playerSpawns[Mathf.Abs(index) % playerSpawns.Length].position;
    }

    /// <summary>월드 좌표가 아레나 안(수평 반경 + 위아래 여유)인지.</summary>
    public bool Contains(Vector3 worldPosition)
    {
        Vector3 d = worldPosition - transform.position;
        d.y = 0f;
        return d.magnitude <= radius && Mathf.Abs(worldPosition.y - transform.position.y) <= 20f;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.7f, 0.4f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
