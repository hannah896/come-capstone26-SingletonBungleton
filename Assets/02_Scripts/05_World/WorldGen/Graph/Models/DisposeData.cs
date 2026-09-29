using UnityEngine;

/// <summary>
/// 실제 맵에 배치될 오브젝트의 정보 (예: "Tree", 타일 위치 (3,5), 로컬 오프셋 (0.2, -0.1), 회전 45도, 스케일 1.5)
/// </summary>
public class DisposeData
{
    public int instanceId;
    public string prefabName;
    // 월드에 배치한 스폰 오브젝트만 사용합니다. 저장 파일에는 배치 결과를 쓰지 않고 시드로 재생성합니다.
    public SourceSpawnRule monsterSpawnRule;
    public Vector2Int tilePosition;
    public Vector2 localOffset;     // 타일 중심에서의 로컬 오프셋
    public Quaternion rotation;
    public Vector3 scale;
}
