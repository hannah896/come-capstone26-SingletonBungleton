using UnityEngine;
using UnityEngine.UI;

/// <summary>월드 시계의 8단계 달 위상을 아이콘으로 표시한다.</summary>
public class UI_Panel_MoonShape : UI_Panel
{
    [SerializeField] private Image _moonImage;
    [Tooltip("삭, 초승, 상현, 상현망간, 보름, 하현망간, 하현, 그믐 순서")]
    [SerializeField] private Sprite[] _phaseSprites = new Sprite[WorldClock.MoonCycleDays];

    private WorldClock _worldClock;
    private LoopManager _subscribedLoop;

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;

        // 슬롯 배경 대신 별도 자식 아이콘만 갱신한다.
        if (_moonImage == null) _moonImage = transform.Find("MoonShape")?.GetComponent<Image>();
        if (_moonImage != null) _moonImage.preserveAspect = true;
        return true;
    }

    private void OnEnable()
    {
        Initialize();
        TryBind();
        _subscribedLoop = Main.Loop;
        if (_subscribedLoop != null)
            _subscribedLoop.OnLateUpdate += HandleLateUpdate;
    }

    private void HandleLateUpdate(float deltaTime)
    {
        // HUD가 시계보다 먼저 생성되었거나 월드가 교체된 경우 다시 연결한다.
        if (_worldClock != WorldClock.Instance) Bind(WorldClock.Instance);
    }

    private void OnDisable()
    {
        UnsubscribeLoop();
        Unbind();
    }

    private void OnDestroy()
    {
        UnsubscribeLoop();
        Unbind();
    }

    private void UnsubscribeLoop()
    {
        if (_subscribedLoop == null) return;

        _subscribedLoop.OnLateUpdate -= HandleLateUpdate;
        _subscribedLoop = null;
    }

    /// <summary>현재 위상을 즉시 표시하고 이후 변경 이벤트를 구독한다.</summary>
    public void Bind(WorldClock worldClock)
    {
        Initialize();
        if (_worldClock == worldClock)
        {
            if (_worldClock != null) SetPhase(_worldClock.CurrentMoonPhase);
            return;
        }

        Unbind();
        if (worldClock == null) return;

        _worldClock = worldClock;
        _worldClock.OnMoonPhaseChanged += SetPhase;
        SetPhase(_worldClock.CurrentMoonPhase);
    }

    public void Unbind()
    {
        if (_worldClock == null) return;

        _worldClock.OnMoonPhaseChanged -= SetPhase;
        _worldClock = null;
    }

    private void TryBind()
    {
        if (WorldClock.Instance != null) Bind(WorldClock.Instance);
    }

    private void SetPhase(MoonPhase phase)
    {
        int index = (int)phase;
        if (_moonImage == null || _phaseSprites == null ||
            index < 0 || index >= _phaseSprites.Length || _phaseSprites[index] == null) return;

        _moonImage.sprite = _phaseSprites[index];
    }
}
