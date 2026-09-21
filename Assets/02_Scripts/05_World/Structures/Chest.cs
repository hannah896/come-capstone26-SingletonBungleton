using UnityEngine;

public class Chest : StorageStation
{
    public override StationType StationType => StationType.Chest;
    public override string DisplayName => "상자";

    protected override void OnInteract(InteractionContext context)
    {
        base.OnInteract(context);
    }
}
