using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 피격 시 모델을 잠깐 빨갛게 물들였다가 원래 색으로 되돌린다 (마인크래프트식 피격 표시). 회복 시에는 초록빛(FlashHeal).
///
/// 머티리얼을 복제하지 않고 MaterialPropertyBlock으로 _BaseColor만 덮어쓴다(URP Lit 기준).
/// 프리팹을 건드릴 필요가 없도록 Monster.Awake에서 자동으로 붙는다.
/// 파티클 등 이펙트 렌더러는 건드리지 않고 모델 메시(Skinned/Mesh)만 대상으로 한다.
/// </summary>
public class MobHitFlash : MonoBehaviour
{
    #region Fields
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    // 빨간색 유지 시간 + 원래 색으로 돌아오는 시간(초)
    private const float HoldTime = 0.12f;
    private const float FadeTime = 0.18f;

    private static readonly Color FlashColor = new Color(1f, 0.25f, 0.25f, 1f);

    /// <summary>회복 연출용 초록빛. 1보다 큰 값으로 살짝 밝게 빛나 보이게 한다.</summary>
    public static readonly Color HealColor = new Color(0.45f, 1.6f, 0.55f, 1f);
    private const float HealHoldTime = 0.25f;
    private const float HealFadeTime = 0.5f;

    private struct Target
    {
        public Renderer Renderer;
        public int MaterialIndex;
        public Color BaseColor;
    }

    private readonly List<Target> _targets = new List<Target>();
    private MaterialPropertyBlock _block;
    private float _timer;
    private float _fadeTime = FadeTime;
    private Color _color = FlashColor;
    private bool _flashing;
    private bool _subscribed;
    #endregion

    /// <summary>대상 렌더러를 모은다. 체력바처럼 런타임에 붙는 자식은 ignoreRoot로 제외한다.</summary>
    public void Init(Transform ignoreRoot = null)
    {
        _block ??= new MaterialPropertyBlock();
        _targets.Clear();

        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (!(r is SkinnedMeshRenderer || r is MeshRenderer)) continue;
            if (ignoreRoot != null && r.transform.IsChildOf(ignoreRoot)) continue;

            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null || !mats[i].HasProperty(BaseColorId)) continue;
                _targets.Add(new Target { Renderer = r, MaterialIndex = i, BaseColor = mats[i].GetColor(BaseColorId) });
            }
        }

        if (!_subscribed)
        {
            Main.Loop.OnLateUpdate += OnLateUpdate;
            _subscribed = true;
        }
    }

    /// <summary>빨간색 깜빡임을 시작한다. 깜빡이는 중에 다시 맞으면 처음부터 다시 시작한다.</summary>
    public void Flash() => Flash(FlashColor, HoldTime, FadeTime);

    /// <summary>회복 연출: 초록빛으로 조금 더 길게 물들였다가 돌아온다.</summary>
    public void FlashHeal() => Flash(HealColor, HealHoldTime, HealFadeTime);

    private void Flash(Color color, float hold, float fade)
    {
        if (_targets.Count == 0) return;
        _color = color;
        _fadeTime = Mathf.Max(fade, 0.01f);
        _timer = hold + _fadeTime;
        _flashing = true;
        Apply(1f);
    }

    /// <summary>즉시 원래 색으로 되돌린다 (풀 반환/재사용 시).</summary>
    public void ResetColor()
    {
        if (!_flashing) return;
        _flashing = false;
        foreach (Target t in _targets)
        {
            if (t.Renderer != null) t.Renderer.SetPropertyBlock(null, t.MaterialIndex);
        }
    }

    private void OnLateUpdate(float deltaTime)
    {
        if (!_flashing) return;

        _timer -= deltaTime;
        if (_timer <= 0f)
        {
            ResetColor();
            return;
        }

        // 유지 구간은 완전히 물들이고, 이후 페이드 시간 동안 원래 색으로
        float t = _timer >= _fadeTime ? 1f : _timer / _fadeTime;
        Apply(t);
    }

    private void Apply(float strength)
    {
        foreach (Target t in _targets)
        {
            if (t.Renderer == null) continue;
            t.Renderer.GetPropertyBlock(_block, t.MaterialIndex);
            _block.SetColor(BaseColorId, Color.Lerp(t.BaseColor, t.BaseColor * _color, strength));
            t.Renderer.SetPropertyBlock(_block, t.MaterialIndex);
        }
    }

    private void OnDestroy()
    {
        if (_subscribed && Main.Instance != null && Main.Loop != null)
            Main.Loop.OnLateUpdate -= OnLateUpdate;
    }
}
