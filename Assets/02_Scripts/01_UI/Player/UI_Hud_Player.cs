using UnityEngine;

public class UI_Hud_Player : UI_Hud
{
    [SerializeField] private UI_Panel_PlayerStatus UI_PlayerStatus;
    [SerializeField] private UI_Panel_PlayerInventory UI_PlayerInventory;

    public UI_Panel_PlayerStatus PlayerStatus => UI_PlayerStatus;
    public UI_Panel_PlayerInventory PlayerInventory => UI_PlayerInventory;

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;

        ResolveReferences();
        return true;
    }

    public void Set(Player player)
    {
        Initialize();

        if (UI_PlayerStatus != null)
        {
            UI_PlayerStatus.gameObject.SetActive(true);
            UI_PlayerStatus.Set(player);
        }

        Set(player != null ? player.Inventory : null);
    }

    private void Set(PlayerInventory inventory)
    {
        Initialize();

        if (UI_PlayerInventory == null) return;

        UI_PlayerInventory.gameObject.SetActive(true);
        UI_PlayerInventory.Set(inventory);
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        UI_PlayerStatus ??= GetComponentInChildren<UI_Panel_PlayerStatus>(true);
        UI_PlayerInventory ??= GetComponentInChildren<UI_Panel_PlayerInventory>(true);
    }
}
