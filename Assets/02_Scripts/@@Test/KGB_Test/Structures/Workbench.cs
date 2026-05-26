using UnityEngine;

public class Workbench : CraftingStation
{
    public override StationType StationType => StationType.Workbench;


    // TODO: player에 CraftingContext 멤버로 추가 필요.
    //protected override void OnPlayerEnter(Player player)
    //    => player.CraftingContext.AddStation(this);

    //protected override void OnPlayerExit(Player player)
    //    => player.CraftingContext.RemoveStation(this);

}
