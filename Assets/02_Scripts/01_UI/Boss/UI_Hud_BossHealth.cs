using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 최종 보스 체력바 HUD (기획 3.1 · 3.2). 화면 상단에 보스 이름 + 줄 단위 체력바 + 무력화(격추) 게이지를 띄운다.
///
/// - 체력을 HpLineCount줄로 나눠, 지금 깎고 있는 줄만 꽉 찬 막대로 보여준다. 한 줄을 다 깎으면 아래 줄 색이 드러난다
/// - 깎인 부분은 잔상(밝은 색)이 0.5초 남았다가 따라 내려간다
/// - 무력화 게이지: 지상은 주황, 공중(격추 게이지)은 하늘색. 그로기 중에는 "그로기"를 깜빡여 딜 타이밍을 알린다
///
/// 드래곤이 스폰되면 스스로 띄워지고(EnsureShown), 살아 있는 드래곤이 없으면 서서히 숨는다.
/// 페이즈 전환(서리룡 사망 → 망령룡 등장) 사이에는 숨었다가, 새 보스가 나오면 체력바가 차오르며 다시 나타난다.
/// 체력은 Monster.HpRatio라 멀티 클라도 호스트 값을 보여준다. (무력화 게이지는 아직 호스트만 정확 — 7단계에서 복제)
/// </summary>
public class UI_Hud_BossHealth : UI_Hud
{
    public const string AddressKey = "UI_Hud_BossHealth";

    #region Inspector
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _lineText;
    [SerializeField] private TextMeshProUGUI _stateText;
    [Tooltip("다음 줄 색 (현재 줄 아래에 깔린다)")]
    [SerializeField] private Image _hpUnder;
    [Tooltip("깎인 부분 잔상")]
    [SerializeField] private Image _hpGhost;
    [SerializeField] private Image _hpFill;
    [SerializeField] private Image _gaugeFill;

    [SerializeField] private Color _emptyColor = new Color(0.08f, 0.08f, 0.1f, 1f);
    [SerializeField] private Color _ghostColor = new Color(1f, 0.95f, 0.8f, 0.9f);
    [SerializeField] private Color _poiseColor = new Color(1f, 0.72f, 0.2f, 1f);
    [SerializeField] private Color _shootdownColor = new Color(0.45f, 0.85f, 1f, 1f);
    [SerializeField] private Color _groggyColor = new Color(1f, 0.35f, 0.25f, 1f);

    [Tooltip("잔상이 따라 내려가기 전에 머무는 시간(초)")]
    [SerializeField] private float _ghostHoldTime = 0.5f;
    [Tooltip("잔상이 따라 내려가는 속도 (초당 최대 체력 비율)")]
    [SerializeField] private float _ghostDrainPerSecond = 0.08f;
    [Tooltip("새 보스가 나타날 때 체력바가 차오르는 시간(초)")]
    [SerializeField] private float _introFillTime = 1.2f;
    [SerializeField] private float _fadeSpeed = 3f;
    #endregion

    private static UI_Hud_BossHealth s_instance;
    private static bool s_loading;

    private DragonBoss _boss;
    private float _shownHp;     // 표시 중인 체력 (등장 시 0에서 차오른다)
    private float _ghostHp;     // 잔상 체력
    private float _ghostHold;
    private float _introTimer;
    private bool _subscribed;

    /// <summary>보스 체력바를 띄운다 (이미 떠 있으면 아무것도 하지 않음). 드래곤이 스폰될 때 부른다.</summary>
    public static void EnsureShown()
    {
        if (s_instance != null || s_loading) return;
        if (Main.Instance == null || Main.UI == null) return;
        ShowAsync().Forget();
    }

    private static async UniTaskVoid ShowAsync()
    {
        s_loading = true;
        try
        {
            var hud = await Main.UI.ShowHudOverlay<UI_Hud_BossHealth>(AddressKey);
            if (hud != null) s_instance = hud;
        }
        finally
        {
            s_loading = false;
        }
    }

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;
        if (_group != null) _group.alpha = 0f;
        if (_stateText != null) _stateText.text = string.Empty;
        return true;
    }

    private void OnEnable()
    {
        if (_subscribed) return;
        Main.Loop.OnUpdate += OnUpdate;
        _subscribed = true;
    }

    // 구독 해제는 OnDisable이 아니라 파괴 시점에만 수행한다.
    private void OnDestroy()
    {
        if (s_instance == this) s_instance = null;
        if (_subscribed && Main.Instance != null && Main.Loop != null)
            Main.Loop.OnUpdate -= OnUpdate;
        _subscribed = false;
    }

    private void OnUpdate(float deltaTime)
    {
        var boss = FindBoss();
        if (boss != _boss) Bind(boss);

        float targetAlpha = _boss != null ? 1f : 0f;
        if (_group != null)
            _group.alpha = Mathf.MoveTowards(_group.alpha, targetAlpha, _fadeSpeed * deltaTime);

        if (_boss == null) return;
        Refresh(deltaTime);
    }

    // 살아 있는 드래곤 중 첫 번째. 방금 죽은 보스는 체력 0을 잠깐 보여주고 숨는다
    private DragonBoss FindBoss()
    {
        DragonBoss dying = null;
        foreach (var boss in DragonBoss.Active)
        {
            if (boss == null || !boss.isActiveAndEnabled) continue;
            if (boss.Status != null && !boss.Status.IsDead) return boss;
            dying ??= boss;
        }
        return dying != null && dying == _boss ? dying : null;
    }

    private void Bind(DragonBoss boss)
    {
        _boss = boss;
        if (boss == null) return;

        var d = boss.Data;
        if (_nameText != null) _nameText.text = d != null ? d.BossName : boss.name;
        _shownHp = 0f;
        _ghostHp = 0f;
        _ghostHold = 0f;
        _introTimer = _introFillTime;
    }

    private void Refresh(float deltaTime)
    {
        var d = _boss.Data;
        float maxHp = _boss.Status != null ? Mathf.Max(_boss.Status.MaxHp, 1f) : 1f;
        float hp = Mathf.Clamp01(_boss.HpRatio) * maxHp;

        // 등장: 0에서 현재 체력까지 차오른다
        if (_introTimer > 0f)
        {
            _introTimer -= deltaTime;
            float t = 1f - Mathf.Clamp01(_introTimer / Mathf.Max(_introFillTime, 0.01f));
            _shownHp = hp * Mathf.SmoothStep(0f, 1f, t);
            _ghostHp = _shownHp;
        }
        else
        {
            if (hp < _shownHp) _ghostHold = _ghostHoldTime; // 새로 깎였으면 잔상을 잠깐 붙잡는다
            _shownHp = hp;

            if (_ghostHp < _shownHp) _ghostHp = _shownHp;
            else if (_ghostHold > 0f) _ghostHold -= deltaTime;
            else _ghostHp = Mathf.Max(_ghostHp - _ghostDrainPerSecond * maxHp * deltaTime, _shownHp);
        }

        DrawHp(d, maxHp);
        DrawGauge();
    }

    // 줄 단위 체력바: 지금 줄 안에서의 비율만 막대로, 아래에는 다음 줄 색을 깐다
    private void DrawHp(DragonStatData d, float maxHp)
    {
        int lines = Mathf.Max(d != null ? d.HpLineCount : 1, 1);
        float perLine = maxHp / lines;

        int remaining = Mathf.Clamp(Mathf.CeilToInt(_shownHp / perLine - 0.0001f), 0, lines);
        float lineStart = Mathf.Max(remaining - 1, 0) * perLine;
        float fillRatio = remaining > 0 ? Mathf.Clamp01((_shownHp - lineStart) / perLine) : 0f;
        float ghostRatio = remaining > 0 ? Mathf.Clamp01((_ghostHp - lineStart) / perLine) : 0f;

        var colors = d != null && d.HpLineColors != null && d.HpLineColors.Length > 0 ? d.HpLineColors : null;
        // 첫 줄(가득 찬 상태)이 팔레트 첫 색이 되도록 위에서부터 센다
        int lineIndex = lines - Mathf.Max(remaining, 1);
        Color current = colors != null ? colors[lineIndex % colors.Length] : Color.red;
        Color under = remaining > 1 && colors != null ? colors[(lineIndex + 1) % colors.Length] : _emptyColor;
        // 아래 줄은 조금 어둡게 해서 지금 줄과 구분되게 한다
        under = remaining > 1 ? Color.Lerp(under, Color.black, 0.35f) : under;

        SetBar(_hpFill, fillRatio, current);
        SetBar(_hpGhost, ghostRatio, _ghostColor);
        SetBar(_hpUnder, 1f, under);

        if (_lineText != null) _lineText.text = remaining > 0 ? $"×{remaining}" : string.Empty;
    }

    private void DrawGauge()
    {
        bool shootdown = _boss.IsShootdownGauge;
        bool groggy = _boss.IsGroggy;
        Color color = groggy ? _groggyColor : (shootdown ? _shootdownColor : _poiseColor);
        SetBar(_gaugeFill, _boss.PoiseRatio, color);

        if (_stateText == null) return;
        if (groggy)
        {
            _stateText.text = "그로기";
            _stateText.color = new Color(_groggyColor.r, _groggyColor.g, _groggyColor.b, 0.6f + 0.4f * Mathf.PingPong(Time.time * 3f, 1f));
        }
        else if (shootdown)
        {
            _stateText.text = "격추";
            _stateText.color = _shootdownColor;
        }
        else
        {
            _stateText.text = string.Empty;
        }
    }

    // 막대는 스프라이트 없이 단색 Image의 오른쪽 앵커로 길이를 정한다
    private static void SetBar(Image image, float ratio, Color color)
    {
        if (image == null) return;
        var rt = image.rectTransform;
        rt.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
        image.color = color;
    }
}
