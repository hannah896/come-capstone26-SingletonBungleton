using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보스 기믹 경고 문구 종류. 네트워크로 byte로 보내므로 값을 바꾸지 말고 뒤에만 추가한다.
/// </summary>
public enum BossAlert : byte
{
    /// <summary>월식의 응시 예고 (카운트다운).</summary>
    GazeWarning = 0,
    /// <summary>응시를 피했다.</summary>
    GazeAvoided = 1,
    /// <summary>응시에 걸려 공포.</summary>
    Feared = 2,
    /// <summary>내가 망령의 각인 대상 (카운트다운).</summary>
    Marked = 3,
    /// <summary>각인 정화 성공.</summary>
    MarkCleansed = 4,
    /// <summary>각인 폭발.</summary>
    MarkExploded = 5,
    /// <summary>광폭화 시작.</summary>
    Enraged = 6,
}

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
///
/// 화면 가운데 위쪽에는 기믹 경고 문구(ShowAlert: 응시·각인)를, 게이지 아래에는 월식의 어둠 안내(SetInDarkness)를 띄운다.
/// 화면 효과: 가장자리 비네트(어둠=검보라, 각인=보라 맥동, 응시 예고=붉은 맥동 + 점점 세지는 흔들림),
/// 순간 플래시(공포·정화·폭발)와 카메라 흔들림.
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
    [Tooltip("기믹 경고 문구 (화면 가운데 위쪽)")]
    [SerializeField] private TextMeshProUGUI _alertText;
    [Tooltip("광폭화까지 남은 시간 (체력바 오른쪽 위)")]
    [SerializeField] private TextMeshProUGUI _timerText;
    [Tooltip("월식의 어둠 안내 (게이지 아래)")]
    [SerializeField] private TextMeshProUGUI _hintText;
    [Tooltip("화면 가장자리 비네트 (스프라이트가 비어 있으면 런타임에 만든다)")]
    [SerializeField] private Image _vignette;
    [Tooltip("화면 전체 순간 플래시")]
    [SerializeField] private Image _flash;

    [SerializeField] private Color _darknessTint = new Color(0.06f, 0.01f, 0.12f, 0.8f);
    [SerializeField] private Color _markTint = new Color(0.55f, 0.1f, 0.8f, 0.75f);
    [SerializeField] private Color _gazeTint = new Color(0.7f, 0.04f, 0.04f, 0.8f);

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

    private BossAlert _alert;
    private float _alertTime;       // 남은 표시 시간
    private float _alertDuration;
    private static bool s_inDarkness;

    private Color _vignetteColor = Color.clear;
    private Color _flashColor = Color.clear;
    private float _flashTime;
    private float _flashDuration;
    private static Sprite s_vignetteSprite;

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

    /// <summary>
    /// 기믹 경고 문구를 이 피어 화면에 띄운다. 새 문구가 이전 문구를 덮는다.
    /// 카운트다운이 붙는 문구(응시 예고·각인)는 duration을 초 단위로 세어 보여준다.
    /// </summary>
    public static void ShowAlert(BossAlert alert, float duration)
    {
        if (s_instance == null) return;
        s_instance._alert = alert;
        s_instance._alertDuration = Mathf.Max(duration, 0.1f);
        s_instance._alertTime = s_instance._alertDuration;

        // 문구가 뜨는 순간의 연출
        switch (alert)
        {
            case BossAlert.Feared:
                s_instance.Flash(new Color(0.6f, 0.1f, 0.9f, 0.7f), 0.9f);
                PlayerFirstPersonCameraController.Shake(2.2f, 0.9f);
                break;
            case BossAlert.Marked:
                s_instance.Flash(new Color(0.6f, 0.15f, 0.9f, 0.35f), 0.4f);
                PlayerFirstPersonCameraController.Shake(0.6f, 0.3f);
                break;
            case BossAlert.MarkCleansed:
                s_instance.Flash(new Color(0.7f, 0.9f, 1f, 0.45f), 0.6f);
                break;
            case BossAlert.MarkExploded:
                s_instance.Flash(new Color(1f, 0.2f, 0.15f, 0.6f), 0.6f);
                PlayerFirstPersonCameraController.Shake(1.8f, 0.6f);
                break;
            case BossAlert.Enraged:
                s_instance.Flash(new Color(1f, 0.1f, 0.05f, 0.5f), 1f);
                PlayerFirstPersonCameraController.Shake(1.5f, 1f);
                break;
        }
    }

    /// <summary>화면 전체를 color로 번쩍였다가 duration 동안 사라지게 한다.</summary>
    public void Flash(Color color, float duration)
    {
        _flashColor = color;
        _flashDuration = Mathf.Max(duration, 0.05f);
        _flashTime = _flashDuration;
    }

    /// <summary>응시 예고 카운트다운 중인지 (보스 눈 빛내기 연출용). 0~1 진행도는 GazeWarningProgress.</summary>
    public static bool IsGazeWarning
        => s_instance != null && s_instance._alert == BossAlert.GazeWarning && s_instance._alertTime > 0f;

    public static float GazeWarningProgress
        => IsGazeWarning ? 1f - Mathf.Clamp01(s_instance._alertTime / s_instance._alertDuration) : 0f;

    /// <summary>로컬 플레이어가 월식의 어둠 속(빛 밖)에 있는지. 2페이즈 보스가 매 판정마다 갱신한다.</summary>
    public static void SetInDarkness(bool value) => s_inDarkness = value;

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;
        if (_group != null) _group.alpha = 0f;
        if (_stateText != null) _stateText.text = string.Empty;
        if (_alertText != null) _alertText.text = string.Empty;
        if (_hintText != null) _hintText.text = string.Empty;
        if (_timerText != null) _timerText.text = string.Empty;
        if (_vignette != null)
        {
            if (_vignette.sprite == null) _vignette.sprite = GetVignetteSprite();
            _vignette.color = Color.clear;
        }
        if (_flash != null) _flash.color = Color.clear;
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

        if (_boss == null)
        {
            s_inDarkness = false;
            _alertTime = 0f;
            DrawAlert(deltaTime);
            DrawScreenFx(deltaTime);
            return;
        }
        Refresh(deltaTime);
        DrawScreenFx(deltaTime);
        DrawAlert(deltaTime);
    }

    // 비네트: 지금 가장 급한 상태(응시 예고 > 각인 > 어둠) 색으로 서서히 바뀐다. 플래시는 위에 덮인다
    private void DrawScreenFx(float deltaTime)
    {
        bool alerting = _alertTime > 0f && _boss != null;
        Color target = Color.clear;
        if (alerting && _alert == BossAlert.GazeWarning)
        {
            // 남은 시간이 줄수록 맥동이 빨라지고 흔들림이 세진다
            float progress = 1f - Mathf.Clamp01(_alertTime / _alertDuration);
            target = _gazeTint;
            target.a *= 0.55f + 0.45f * Mathf.PingPong(Time.time * (2f + 6f * progress), 1f);
            PlayerFirstPersonCameraController.Shake(0.15f + 0.6f * progress, 0.15f);
        }
        else if (alerting && _alert == BossAlert.Marked)
        {
            float urgency = 1f - Mathf.Clamp01(_alertTime / _alertDuration);
            target = _markTint;
            target.a *= 0.5f + 0.5f * Mathf.PingPong(Time.time * (1.5f + 4f * urgency), 1f);
        }
        else if (s_inDarkness && _boss != null)
        {
            target = _darknessTint;
        }

        _vignetteColor = Color.Lerp(_vignetteColor, target, 1f - Mathf.Exp(-6f * deltaTime));
        if (_vignette != null) _vignette.color = _vignetteColor;

        if (_flash != null)
        {
            if (_flashTime > 0f)
            {
                _flashTime -= deltaTime;
                Color c = _flashColor;
                c.a *= Mathf.Clamp01(_flashTime / _flashDuration);
                _flash.color = c;
            }
            else if (_flash.color.a > 0f)
            {
                _flash.color = Color.clear;
            }
        }
    }

    // 가운데는 투명하고 가장자리로 갈수록 진해지는 원형 그라데이션
    private static Sprite GetVignetteSprite()
    {
        if (s_vignetteSprite != null) return s_vignetteSprite;
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / 1.4142f;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 1f, r));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        s_vignetteSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return s_vignetteSprite;
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
        DrawEnrageTimer();
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

    private void DrawAlert(float deltaTime)
    {
        if (_alertText != null)
        {
            if (_alertTime > 0f)
            {
                _alertTime -= deltaTime;
                _alertText.text = AlertText(_alert, _alertTime);
                Color c = AlertColor(_alert);
                // 끝나기 직전 0.3초 동안 사라진다
                c.a = Mathf.Clamp01(_alertTime / 0.3f);
                _alertText.color = c;
            }
            else if (_alertText.text.Length > 0)
            {
                _alertText.text = string.Empty;
            }
        }

        if (_hintText != null)
        {
            bool show = s_inDarkness && _boss != null;
            _hintText.text = show ? "어둠 속에서 정신이 깎인다 — 횃불이나 월광 기둥 곁으로" : string.Empty;
            if (show) _hintText.color = new Color(0.75f, 0.6f, 1f, 0.55f + 0.45f * Mathf.PingPong(Time.time * 1.5f, 1f));
        }
    }

    private static string AlertText(BossAlert alert, float timeLeft)
    {
        int sec = Mathf.Max(Mathf.CeilToInt(timeLeft), 0);
        return alert switch
        {
            BossAlert.GazeWarning  => $"그것이 당신을 본다 — 눈을 돌려라  {sec}",
            BossAlert.GazeAvoided  => "시선을 피했다",
            BossAlert.Feared       => "공포 — 몸이 굳었다",
            BossAlert.Marked       => $"망령의 각인 — 월광 기둥 곁에 머물러라  {sec}",
            BossAlert.MarkCleansed => "각인이 정화되었다",
            BossAlert.MarkExploded => "각인이 터졌다",
            BossAlert.Enraged      => "광폭화 — 더는 버틸 수 없다",
            _ => string.Empty,
        };
    }

    private static Color AlertColor(BossAlert alert) => alert switch
    {
        BossAlert.GazeWarning  => new Color(1f, 0.3f, 0.25f, 1f),
        BossAlert.GazeAvoided  => new Color(0.85f, 0.85f, 0.9f, 1f),
        BossAlert.Feared       => new Color(0.85f, 0.35f, 1f, 1f),
        BossAlert.Marked       => new Color(0.8f, 0.4f, 1f, 1f),
        BossAlert.MarkCleansed => new Color(0.7f, 0.9f, 1f, 1f),
        BossAlert.MarkExploded => new Color(1f, 0.35f, 0.3f, 1f),
        BossAlert.Enraged      => new Color(1f, 0.2f, 0.1f, 1f),
        _ => Color.white,
    };

    // 광폭화까지 남은 시간. 1분 아래면 붉게, 광폭화 후에는 "광폭화"를 깜빡인다
    private void DrawEnrageTimer()
    {
        if (_timerText == null) return;
        if (_boss.IsEnraged)
        {
            _timerText.text = "광폭화";
            _timerText.color = new Color(1f, 0.25f, 0.15f, 0.6f + 0.4f * Mathf.PingPong(Time.time * 3f, 1f));
            return;
        }

        float left = _boss.EnrageTimeLeft;
        if (left < 0f)
        {
            _timerText.text = string.Empty;
            return;
        }
        int sec = Mathf.CeilToInt(left);
        _timerText.text = $"{sec / 60:00}:{sec % 60:00}";
        _timerText.color = left <= 60f ? new Color(1f, 0.35f, 0.25f, 1f) : new Color(0.85f, 0.85f, 0.9f, 1f);
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
