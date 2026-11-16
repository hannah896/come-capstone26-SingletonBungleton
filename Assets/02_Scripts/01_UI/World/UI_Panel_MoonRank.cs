using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>만월 보스의 능력치 배율을 글자 없는 계급장으로 표시한다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(UI_MoonRankGraphic))]
public sealed class UI_Panel_MoonRank : UI_Panel, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private UI_MoonRankGraphic _rankGraphic;

    private LoopManager _subscribedLoop;
    private bool _isPointerOver;
    private bool _hasState;
    private bool _isFullMoon;
    private string _tooltipText;

    public int TargetFullMoonDay { get; private set; }
    public float DisplayedMultiplier { get; private set; }
    public bool HasBoss { get; private set; }

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;
        if (_rankGraphic == null) _rankGraphic = GetComponent<UI_MoonRankGraphic>();
        return true;
    }

    private void OnEnable()
    {
        Initialize();
        if (!Application.isPlaying) return;

        RefreshState();
        _subscribedLoop = Main.Loop;
        if (_subscribedLoop != null) _subscribedLoop.OnLateUpdate += HandleLateUpdate;
    }

    private void OnDisable()
    {
        if (_subscribedLoop != null) _subscribedLoop.OnLateUpdate -= HandleLateUpdate;
        _subscribedLoop = null;
        HideTooltip();
    }

    private void HandleLateUpdate(float deltaTime) => RefreshState();

    private void RefreshState()
    {
        // HUD가 먼저 생성되거나 저장 복원/멀티 동기화로 월드가 바뀌어도 현재 시계와 규칙을 따른다.
        WorldClock clock = WorldClock.Instance;
        WorldGenManager world = WorldGenManager.Instance;
        BossSpawnRule rule = clock != null && world != null && world.WorldSettings != null
            ? world.WorldSettings.BossSpawnRule : null;
        SetState(rule, clock != null ? clock.CurrentDay : 1);
    }

    /// <summary>평소에는 다음 만월, 만월 당일에는 그날 생성 규칙의 배율을 표시한다.</summary>
    public void SetState(BossSpawnRule rule, int currentDay)
    {
        Initialize();
        int targetDay = GetTargetFullMoonDay(currentDay);
        bool hasBoss = rule != null && !string.IsNullOrEmpty(rule.GetMonsterKey(targetDay));
        float multiplier = hasBoss ? rule.GetStatMultiplier(targetDay) : 1f;
        bool isFullMoon = currentDay == targetDay;
        if (_hasState && TargetFullMoonDay == targetDay && HasBoss == hasBoss &&
            DisplayedMultiplier == multiplier && _isFullMoon == isFullMoon) return;

        _hasState = true;
        TargetFullMoonDay = targetDay;
        HasBoss = hasBoss;
        DisplayedMultiplier = multiplier;
        _isFullMoon = isFullMoon;
        _rankGraphic.SetRank(hasBoss ? GetRank(multiplier) : 0);

        string heading = isFullMoon ? "이번 만월 보스" : "다음 만월 보스";
        _tooltipText = hasBoss
            ? $"{heading}\n최대 체력 · 주는 피해 ×{multiplier:0.##}"
            : "만월 보스 정보 없음";
        if (_isPointerOver) ItemNameTooltip.Show(_tooltipText, null);
    }

    public static int GetTargetFullMoonDay(int currentDay)
    {
        currentDay = Mathf.Max(1, currentDay);
        int phase = (currentDay - 1) % WorldClock.MoonCycleDays;
        int remaining = ((int)MoonPhase.Full - phase + WorldClock.MoonCycleDays) % WorldClock.MoonCycleDays;
        return currentDay + remaining;
    }

    /// <summary>1배 / 1.5배 / 2배 이상을 문양 1 / 2 / 3개로 구분한다. 정확한 수치는 툴팁에 표시한다.</summary>
    public static int GetRank(float multiplier) => multiplier >= 2f ? 3 : multiplier >= 1.5f ? 2 : 1;

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isPointerOver = true;
        if (_hasState) ItemNameTooltip.Show(_tooltipText, null);
    }

    public void OnPointerExit(PointerEventData eventData) => HideTooltip();

    private void HideTooltip()
    {
        if (_isPointerOver) ItemNameTooltip.Hide();
        _isPointerOver = false;
    }
}
