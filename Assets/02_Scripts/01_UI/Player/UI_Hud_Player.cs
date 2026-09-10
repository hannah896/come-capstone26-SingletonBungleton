using UnityEngine;

public class UI_Hud_Player : UI_Hud
{
    #region Fields
    [SerializeField] private UI_Panel_PlayerStatus UI_PlayerStatus;
    [SerializeField] private UI_Panel_PlayerInventory UI_PlayerInventory;
    [SerializeField] private UI_Image UI_Image_Focus;

    private Player player;
    #endregion

    #region Crosshair Settings
    // 평소 크로스헤어 색/크기
    private static readonly Color FocusNormalColor = new(1f, 1f, 1f, 0.5f);
    private const float FocusNormalScale = 1f;

    // 채취 가능한 자원을 조준했을 때 강조 색/크기
    private static readonly Color FocusHighlightColor = new(1f, 0.85f, 0.2f, 1f);
    private const float FocusHighlightScale = 1.3f;
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

    private void OnEnable()
    {
        if (Main.Instance != null && Main.Loop != null)
            Main.Loop.OnUpdate += UpdateFocus;
    }

    private void OnDestroy()
    {
        if (Main.Instance != null && Main.Loop != null)
            Main.Loop.OnUpdate -= UpdateFocus;
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        UI_PlayerStatus ??= GetComponentInChildren<UI_Panel_PlayerStatus>(true);
        UI_PlayerInventory ??= GetComponentInChildren<UI_Panel_PlayerInventory>(true);
        UI_Image_Focus ??= gameObject.FindChild<UI_Image>("UI_Image_Focus");
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

    /// <summary>
    /// 화면 중앙(크로스헤어)이 조준한 대상이 현재 장착 도구로 채취 가능한 자원이거나
    /// G키로 주울 수 있는 아이템이면 크로스헤어를 강조 색/크기로 바꾼다.
    /// </summary>
    private void UpdateFocus(float deltaTime)
    {
        if (UI_Image_Focus == null || player == null) return;

        bool highlight = player.Inventory != null
            && (player.Inventory.TryGetToolActionType(out _)
                || player.Inventory.HasPickupTargetFocused());

        UI_Image_Focus.SetColor(highlight ? FocusHighlightColor : FocusNormalColor);
        UI_Image_Focus.transform.localScale =
            Vector3.one * (highlight ? FocusHighlightScale : FocusNormalScale);
    }
}
