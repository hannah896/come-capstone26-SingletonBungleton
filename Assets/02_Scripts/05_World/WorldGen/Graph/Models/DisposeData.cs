using UnityEngine;

/// <summary>
/// 실제 맵에 배치될 오브젝트의 정보 (예: "Tree", 타일 위치 (3,5), 로컬 오프셋 (0.2, -0.1), 회전 45도, 스케일 1.5)
/// </summary>
public class DisposeData
{
    public int instanceId;
    public string prefabName;
    public Vector2Int tilePosition;
    public Vector2 localOffset;     // 타일 중심에서의 로컬 오프셋
    public Quaternion rotation;
    public Vector3 scale;
}
