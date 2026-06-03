using UnityEngine;

public class Chest : StorageStation
{
    public override StationType StationType => StationType.Chest;
    protected override void OnInteract(InteractionContext context)
    {
        base.OnInteract(context);
    }
}
