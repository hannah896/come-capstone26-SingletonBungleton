using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// <see cref="WorldClock"/>의 상태를 화면에 표시하는 View다.
/// 텍스트는 이벤트로, 해와 달의 회전은 매 프레임 하루 진행률로 갱신한다.
/// </summary>
public class UI_Panel_WorldClock : UI_Panel, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Day Tooltip")]
    [SerializeField] private UI_Text _dayText;
    [SerializeField] private RectTransform _dayTooltip;

    [Header("Sun / Moon Orbit")]
    [SerializeField] private RectTransform _sunMoonParent;
    [SerializeField] private RectTransform _sun;
    [SerializeField] private RectTransform _moon;

    [SerializeField] private bool _keepIconsUpright = true;

    [Header("Clock Phase")]
    [SerializeField] private RectTransform _dayPhase;
    [SerializeField] private RectTransform _nightPhase;

    [Header("Formats")]
    [SerializeField] private string _dayFormat = "Day: {0}";

    private WorldClock _worldClock;
    private bool _isPointerOverClock;
    private LoopManager _subscribedLoop;
    private Quaternion _sunInitialRotation;
    private Quaternion _moonInitialRotation;
    private Graphic _sunGraphic;
    private Graphic _moonGraphic;
    private Graphic _dayPhaseGraphic;

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;

        // 날짜 오버레이가 Clock의 포인터 판정을 가로채거나 범위를 넓히지 않게 한다.
        if (_dayTooltip != null)
        {
            foreach (Graphic graphic in _dayTooltip.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
        }
        RefreshDayVisibility();

        if (_dayPhase != null)
            _dayPhaseGraphic = _dayPhase.GetComponent<Graphic>();

        if (_sun != null)
        {
            _sunInitialRotation = _sun.localRotation;
            _sunGraphic = _sun.GetComponent<Graphic>();
        }
        if (_moon != null)
        {
            _moonInitialRotation = _moon.localRotation;
            _moonGraphic = _moon.GetComponent<Graphic>();
        }
        return true;
    }

    private void OnEnable()
    {
        Initialize();
        _isPointerOverClock = false;
        RefreshDayVisibility();
        TryBind();
        _subscribedLoop = Main.Loop;
        if (_subscribedLoop != null)
            _subscribedLoop.OnLateUpdate += HandleLateUpdate;
    }

    private void HandleLateUpdate(float deltaTime)
    {
        if (_worldClock == null)
        {
            TryBind();
        }

        RefreshOrbit();
        RefreshDayVisibility();
    }

    private void OnDisable()
    {
        _isPointerOverClock = false;
        RefreshDayVisibility();
        UnsubscribeLoop();
        Unbind();
    }

    private void OnDestroy()
    {
        UnsubscribeLoop();
        Unbind();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isPointerOverClock = true;
        RefreshDayVisibility();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isPointerOverClock = false;
        RefreshDayVisibility();
    }

    private void RefreshDayVisibility()
    {
        if (_dayTooltip == null) return;

        bool visible = _isPointerOverClock && _worldClock != null;
        if (_dayTooltip.gameObject.activeSelf != visible)
            _dayTooltip.gameObject.SetActive(visible);
    }

    private void UnsubscribeLoop()
    {
        if (_subscribedLoop == null) return;

        _subscribedLoop.OnLateUpdate -= HandleLateUpdate;
        _subscribedLoop = null;
    }

    /// <summary>표시할 월드 시계를 바인딩한다.</summary>
    public void Bind(WorldClock worldClock)
    {
        if (_worldClock == worldClock)
        {
            RefreshAll();
            return;
        }

        Unbind();
        if (worldClock == null) return;

        _worldClock = worldClock;
        _worldClock.OnDayPassed += HandleDayPassed;
        _worldClock.OnPhaseChanged += HandlePhaseChanged;

        RefreshAll();
    }

    /// <summary>현재 바인딩을 해제한다.</summary>
    public void Unbind()
    {
        if (_worldClock == null) return;

        _worldClock.OnDayPassed -= HandleDayPassed;
        _worldClock.OnPhaseChanged -= HandlePhaseChanged;
        _worldClock = null;
        RefreshDayVisibility();
    }

    private void TryBind()
    {
        if (WorldClock.Instance != null)
        {
            Bind(WorldClock.Instance);
        }
    }

    private void RefreshAll()
    {
        if (_worldClock == null) return;

        SetDay(_worldClock.CurrentDay);
        SetPhase(_worldClock.CurrentTimePhase);
        RefreshOrbit();
        RefreshDayVisibility();
    }

    private void RefreshOrbit()
    {
        if (_worldClock == null || _sunMoonParent == null) return;

        // 회전량을 누적하지 않아 일시정지, 수면, 저장 시간 로드에도 현재 시간과 일치한다.
        float hour = _worldClock.NormalizedTimeOfDay * 24f;
        float angle = EvaluateOrbitAngle(hour);
        Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
        _sunMoonParent.localRotation = rotation;

        // 0~6시는 달이 위쪽 반원을 지나고, 6~24시는 해가 위쪽 반원을 지난다.
        bool isNight = hour < 6f;
        SetAlpha(_sunGraphic, isNight ? 50f / 255f : 1f);
        SetAlpha(_moonGraphic, isNight ? 1f : 50f / 255f);

        // 부모의 회전을 상쇄해 아이콘이 뒤집히지 않도록 한다.
        Quaternion counterRotation = _keepIconsUpright ? Quaternion.Inverse(rotation) : Quaternion.identity;
        if (_sun != null) _sun.localRotation = counterRotation * _sunInitialRotation;
        if (_moon != null) _moon.localRotation = counterRotation * _moonInitialRotation;
    }

    /// <summary>6시 0도 → 12시 60도 → 18시 120도 → 자정 180도 → 6시 360도.</summary>
    private static float EvaluateOrbitAngle(float hour)
    {
        // 밤의 6시간 동안 아래쪽 180도를 돌아 해를 다음 날 시작 위치로 되돌린다.
        if (hour < 6f) return 180f + hour / 6f * 180f;

        // 낮의 18시간 동안 위쪽 180도를 일정하게 돌아 60도 간격의 Line에 맞춘다.
        return (hour - 6f) / 18f * 180f;
    }

    private static void SetAlpha(Graphic graphic, float alpha)
    {
        if (graphic == null || graphic.color.a == alpha) return;

        Color color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }

    private void HandleDayPassed(int _)
    {
        // 이벤트 인수는 0부터 누적된 일수이므로 표시용 CurrentDay를 사용한다.
        SetDay(_worldClock.CurrentDay);
    }

    private void HandlePhaseChanged(TimePhase phase) => SetPhase(phase);

    private void SetDay(int day)
    {
        if (_dayText != null)
            _dayText.Text = string.Format(_dayFormat, day);
    }


    private void SetPhase(TimePhase phase)
    {
        bool isNight = phase == TimePhase.Dawn;
        if (!isNight && _dayPhaseGraphic != null)
        {
            Color color = phase switch
            {
                TimePhase.Afternoon => new Color32(0xFF, 0x78, 0x07, 0xFF),
                TimePhase.Dusk => new Color32(0xCC, 0x24, 0x31, 0xFF),
                _ => new Color32(0xFF, 0xC1, 0x07, 0xFF),
            };
            // 색상만 바꾸고 프리팹에서 설정한 투명도는 유지한다.
            color.a = _dayPhaseGraphic.color.a;
            _dayPhaseGraphic.color = color;
        }

        // 새벽에는 Night만, 나머지 시간대에는 Day만 표시한다.
        if (_dayPhase != null) _dayPhase.gameObject.SetActive(!isNight);
        if (_nightPhase != null) _nightPhase.gameObject.SetActive(isNight);
    }

}
