using UnityEngine;

/// <summary>
/// 냉장고. 상자와 동일하게 16칸을 보관하지만, 안에 든 음식의 소비기한이 3배 느리게 간다.
/// </summary>
public class Refrigerator : StorageStation
{
    private const int RefrigeratorSlotCount = 16;
    private const float RefrigeratorExpirationMultiplier = 3f;

    public override StationType StationType => StationType.Refrigerator;
    public override string DisplayName => "냉장고";

    protected override float ExpirationMultiplier => RefrigeratorExpirationMultiplier;

    protected override void Awake()
    {
        base.Awake();
        SetSlotCount(RefrigeratorSlotCount);
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        SetSlotCount(RefrigeratorSlotCount);
    }

    protected override void OnInteract(InteractionContext context)
    {
        base.OnInteract(context);
    }
}
