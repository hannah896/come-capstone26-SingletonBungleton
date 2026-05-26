using UnityEngine;

// 추후 PhysicsBoxValidator 이외에도 다양한 배치 검증 로직이 
// 필요할 수 있으므로 인터페이스로 분리
public interface IPlacementValidator
{
    bool IsPlacementValid(ItemDataSO item, Vector3 position, Quaternion rotation, float gridSize);
    bool IsCellValid(Vector3 cellPosition, float cellSize);
}