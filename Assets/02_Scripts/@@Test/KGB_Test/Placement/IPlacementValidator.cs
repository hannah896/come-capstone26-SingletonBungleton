using UnityEngine;

public interface IPlacementValidator
{
    bool IsPlacementValid(ItemDataSO item, Vector3 position, Quaternion rotation, float gridSize);
    bool IsCellValid(Vector3 cellPosition, float cellSize);
}