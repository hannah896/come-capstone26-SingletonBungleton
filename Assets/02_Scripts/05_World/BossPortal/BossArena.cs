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

    // 바닥 콜라이더(FloorCollider). 있으면 '아레나 안' 판정을 원이 아니라 실제 바닥(사각형) 기준으로 한다.
    private BoxCollider floorCollider;
    private bool floorSearched;

    // 바닥 가장자리 밖으로 이만큼(m)까지는 아레나 안으로 본다 (벽 위 등)
    private const float FloorMargin = 3f;

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
        if (Mathf.Abs(worldPosition.y - transform.position.y) > 20f) return false;

        // 바닥은 사각형이라 반경(원)으로만 판정하면 모서리가 '밖'으로 잡혀,
        // 용을 피해 모서리로 뛰어간 순간 전멸로 처리되어 아레나가 닫히고 떨어졌다.
        BoxCollider floor = FindFloor();
        if (floor != null)
        {
            Bounds b = floor.bounds;
            return Mathf.Abs(worldPosition.x - b.center.x) <= b.extents.x + FloorMargin
                && Mathf.Abs(worldPosition.z - b.center.z) <= b.extents.z + FloorMargin;
        }

        Vector3 d = worldPosition - transform.position;
        d.y = 0f;
        return d.magnitude <= radius;
    }

    private BoxCollider FindFloor()
    {
        if (floorSearched) return floorCollider;
        floorSearched = true;

        foreach (BoxCollider box in GetComponentsInChildren<BoxCollider>(true))
        {
            if (box.name == "FloorCollider") { floorCollider = box; break; }
        }
        return floorCollider;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.7f, 0.4f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
