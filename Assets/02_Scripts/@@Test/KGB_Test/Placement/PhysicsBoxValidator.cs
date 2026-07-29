using UnityEngine;

public class PhysicsBoxValidator : IPlacementValidator
{
    private readonly LayerMask blockingMask;
    public PhysicsBoxValidator(LayerMask blockingMask)
    {
        this.blockingMask = blockingMask;
    }

    public bool IsPlacementValid(ItemDataSO item, Vector3 position, Quaternion rotation, float gridSize)
    {
        if (item == null) return false;

        var fp = item.placementFootprint;
        int fx = Mathf.Max(1, fp.x);
        int fz = Mathf.Max(1, fp.y);

        Vector3 half = new(
            fx * gridSize * 0.5f,
            item.placementCheckHeight * 0.5f,
            fz * gridSize * 0.5f);

        Vector3 center = position + item.placementCheckCenterOffset;
        return !Physics.CheckBox(center, half, rotation, blockingMask, QueryTriggerInteraction.Ignore);
    }

    public bool IsCellValid(Vector3 cellPosition, float cellSize)
    {
        Vector3 half = new(cellSize * 0.5f, 0.5f, cellSize * 0.5f);
        return !Physics.CheckBox(cellPosition, half, Quaternion.identity, blockingMask, QueryTriggerInteraction.Ignore);
    }
}
