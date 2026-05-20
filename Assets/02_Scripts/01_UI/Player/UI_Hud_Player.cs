using UnityEngine;

public class UI_Hud_Player : UI_Hud
{
    #region Fields
    [SerializeField] private UI_Panel_PlayerStatus UI_PlayerStatus;
    [SerializeField] private UI_Panel_PlayerInventory UI_PlayerInventory;

    private Player player;
    #endregion

    #region Properties
    public UI_Panel_PlayerStatus PlayerStatus => UI_PlayerStatus;
    public UI_Panel_PlayerInventory PlayerInventory => UI_PlayerInventory;
    public Player Player
    {
        get => player;
        set
        {
            player = value;
            ApplyPlayer();
        }
    }
    #endregion

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;

        ResolveReferences();
        SetInitialPanelState();
        return true;
    }

    protected override void Start()
    {
        base.Start();
        ApplyPlayer();
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

    private void SetInitialPanelState()
    {
        if (UI_PlayerStatus != null)
            UI_PlayerStatus.gameObject.SetActive(false);

        if (UI_PlayerInventory != null)
            UI_PlayerInventory.gameObject.SetActive(false);
    }

    private void ApplyPlayer()
    {
        ResolveReferences();

        if (UI_PlayerStatus != null)
        {
            UI_PlayerStatus.gameObject.SetActive(player != null);
            UI_PlayerStatus.Player = player;
        }

        if (UI_PlayerInventory != null)
        {
            UI_PlayerInventory.gameObject.SetActive(player != null);
            UI_PlayerInventory.Player = player;
        }
    }
}
