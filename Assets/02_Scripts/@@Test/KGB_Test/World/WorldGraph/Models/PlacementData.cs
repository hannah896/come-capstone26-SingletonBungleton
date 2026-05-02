using UnityEngine;

/// <summary>
/// 실제 맵에 배치될 오브젝트의 정보 (예: "Pig King Statue", 타일 위치 (3,5), 로컬 오프셋 (0.2, -0.1), 회전 45도, 스케일 1.5)
/// </summary>
public class PlacementData
{
    public string prefabName;
    public Vector2Int tilePosition;
    public Vector2 localOffset;     // 타일 중심에서의 로컬 오프셋
    public Quaternion rotation;
    public Vector3 scale;
    

    public Vector3 GetFlatWorldPosition(float tileUnitSize)
    {
        float realX = (tilePosition.x + localOffset.x) * tileUnitSize;
        float realZ = (tilePosition.y + localOffset.y) * tileUnitSize;

        // Y축은 아직 모르니 일단 0으로 줍니다.
        return new Vector3(realX, 0f, realZ);
    }
}
