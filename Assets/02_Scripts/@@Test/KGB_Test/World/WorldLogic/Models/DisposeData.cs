using UnityEngine;

public class DisposeData
{
    public string prefabName;
    public Vector2Int tilePosition;
    public Vector2 localOffset;     // 타일 중심에서의 로컬 오프셋

    public Quaternion rotation;
    public Vector3 scale;
    public int ownerNodeIndex;      // 이 오브젝트가 속한 룸 노드 인덱스

    public Vector3 GetFlatWorldPosition(float tileUnitSize)
    {
        float realX = (tilePosition.x + localOffset.x) * tileUnitSize;
        float realZ = (tilePosition.y + localOffset.y) * tileUnitSize;

        // Y축은 아직 모르니 일단 0으로 줍니다.
        return new Vector3(realX, 0f, realZ);
    }
}
