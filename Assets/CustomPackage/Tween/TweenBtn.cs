using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 버튼 클릭 시 스케일 및 컬러 트윈 애니메이션을 적용하는 컴포넌트.
/// </summary>
public class TweenBtn : InitBehaviour, IPointerDownHandler, IPointerUpHandler
{
    #region Fields

    [Header("Scale 설정")]
    [SerializeField] private Transform[] _targets; // 스케일을 변경할 Transform 배열 (비어있으면 자기 자신)
    [SerializeField] private float _scaleMultiplier = 0.9f; // 목표 스케일 비율 (1.0 = 원본, 0.9 = 90%)
    [SerializeField] private float _duration = 0.1f; // 애니메이션 지속 시간
    [SerializeField] private Ease _ease = Ease.Linear; // 애니메이션 이징

    [Header("Color 설정")]
    [SerializeField] private bool _useColor = true; // 컬러 변경 사용 여부
    [SerializeField] private Color _targetColor = new Color(0.6f, 0.6f, 0.6f, 1f); // 목표 컬러

    [Header("Sound 설정")]
    [SerializeField] private bool _useClickSound = true; // 클릭 사운드 재생 여부
    [SerializeField] private AudioLibrarySounds _audioLibrarySounds; // 재생할 클릭 사운드

    private Dictionary<Transform, Vector3> _originalScales = new(); // 캐싱된 원본 스케일
    private List<Graphic> _cachedGraphics = new(); // 캐싱된 Graphic 컴포넌트들
    private Dictionary<Graphic, Color> _originalColors = new(); // 캐싱된 원본 컬러

    #endregion

    #region Initialization

    // 초기화: 원본 스케일 및 Graphic 컴포넌트 캐싱
    public override bool Initialize()
    {
        if (!base.Initialize()) return false;

        CacheOriginalScales();
        CacheGraphicComponents();

        return true;
    }

    // 원본 스케일 캐싱
    private void CacheOriginalScales()
    {
        _originalScales.Clear();

        var targets = GetTargets();
        foreach (var target in targets)
        {
            if (target != null)
            {
                _originalScales[target] = target.localScale;
            }
        }
    }

    // Graphic 컴포넌트 캐싱 (Image, TMP_Text 등)
    private void CacheGraphicComponents()
    {
        _cachedGraphics.Clear();
        _originalColors.Clear();

        var targets = GetTargets();
        foreach (var target in targets)
        {
            if (target == null) continue;

            // 자신과 모든 자식에서 Graphic 컴포넌트 수집
            var graphics = target.GetComponentsInChildren<Graphic>(true);
            foreach (var graphic in graphics)
            {
                if (!_cachedGraphics.Contains(graphic))
                {
                    _cachedGraphics.Add(graphic);
                    _originalColors[graphic] = graphic.color;
                }
            }
        }
    }

    // 타겟 Transform 배열 반환 (비어있으면 자기 자신)
    private Transform[] GetTargets()
    {
        if (_targets == null || _targets.Length == 0)
        {
            return new[] { transform };
        }

        return _targets;
    }

    #endregion

    #region Pointer Events

    public void OnPointerDown(PointerEventData eventData)
    {
        Initialize();
        PlayTween();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Initialize();
        ResetToOriginal();
        PlayFeedback();
    }

    #endregion

    #region Tween

    // 트윈 애니메이션 재생
    private void PlayTween()
    {
        PlayScaleTween();

        if (_useColor)
        {
            PlayColorTween();
        }
    }

    // 스케일 트윈 재생
    private void PlayScaleTween()
    {
        var targets = GetTargets();

        foreach (var target in targets)
        {
            if (target == null) continue;

            // 원본 스케일 기준으로 상대적 목표값 계산
            if (!_originalScales.TryGetValue(target, out var originalScale))
            {
                originalScale = target.localScale;
                _originalScales[target] = originalScale;
            }

            Vector3 targetScale = originalScale * _scaleMultiplier;

            target.DOKill();
            target.DOScale(targetScale, _duration)
                .SetEase(_ease);
        }
    }

    // 컬러 트윈 재생
    private void PlayColorTween()
    {
        foreach (var graphic in _cachedGraphics)
        {
            if (graphic == null) continue;

            graphic.DOKill();
            graphic.DOColor(_targetColor, _duration)
                .SetEase(_ease);
        }
    }

    // 사운드 피드백
    private void PlayFeedback()
    {
        if (_useClickSound)
        {
            Main.JSAM.PlaySFX(_audioLibrarySounds);
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 원본 스케일 및 컬러로 즉시 복원합니다.
    /// </summary>
    public void ResetToOriginal()
    {
        // 스케일 복원
        foreach (var kvp in _originalScales)
        {
            if (kvp.Key != null)
            {
                kvp.Key.DOKill();
                kvp.Key.DOScale(kvp.Value, _duration).SetEase(_ease);
            }
        }

        // 컬러 복원
        if (_useColor)
        {
            foreach (var kvp in _originalColors)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.DOKill();
                    kvp.Key.DOColor(kvp.Value, _duration).SetEase(_ease);
                }
            }
        }
    }

    #endregion
}
