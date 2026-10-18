using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 드래곤 보스전의 엄폐물 표식 (월광 기둥 등). 이 컴포넌트가 붙은 오브젝트의 콜라이더는
/// - 브레스를 막는다 (드래곤 입 ↔ 플레이어 사이에 있으면 브레스 무효)
/// - 얼음 돌진을 멈춘다 (부딪힌 드래곤이 스스로 그로기)
/// - 서리 폭발 · 월식 폭우의 안전지대가 된다 (SafeRadius 안이면 피해 감소/무효)
/// - 2페이즈 부식 브레스에 맞으면 부식되고, MaxCorrosion번 맞으면 부서진다 (위 효과가 모두 사라짐)
///
/// 빛·각인 정화는 5단계(월광 기둥)에서 이 클래스를 확장한다.
/// </summary>
public class DragonCover : MonoBehaviour
{
    private static readonly List<DragonCover> s_all = new List<DragonCover>();
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [Tooltip("서리 폭발 안전지대 반경(m). 기둥 중심에서 이 거리 안에 서 있으면 피해가 줄어든다")]
    [SerializeField] private float safeRadius = 3f;
    [Tooltip("부식 브레스를 이만큼 맞으면 부서진다")]
    [SerializeField] private int maxCorrosion = 3;
    [Tooltip("부식될수록 물드는 색 (마지막엔 이 색이 된다)")]
    [SerializeField] private Color corrodedColor = new Color(0.25f, 0.45f, 0.15f, 1f);

    private int corrosion;
    private Renderer[] renderers;
    private Color[] baseColors;
    private Vector3[] baseScales;
    private MaterialPropertyBlock block;

    /// <summary>부식 횟수가 바뀌었을 때 (기둥 연출용). 인자는 부식 횟수.</summary>
    public event System.Action<DragonCover, int> Corroded;

    /// <summary>활성화된 엄폐물 전체.</summary>
    public static IReadOnlyList<DragonCover> All => s_all;

    public float SafeRadius => safeRadius;
    public int Corrosion => corrosion;
    public int MaxCorrosion => maxCorrosion;
    public bool IsBroken => corrosion >= maxCorrosion;

    /// <summary>엄폐물로 쓸 수 있는 상태인지 (부서진 기둥은 false).</summary>
    public virtual bool IsIntact => isActiveAndEnabled && !IsBroken;

    protected virtual void Awake()
    {
        s_all.Add(this);
        renderers = GetComponentsInChildren<Renderer>();
        baseColors = new Color[renderers.Length];
        baseScales = new Vector3[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            baseScales[i] = renderers[i].transform.localScale;
            var mat = renderers[i].sharedMaterial;
            baseColors[i] = mat != null && mat.HasProperty(BaseColorId) ? mat.GetColor(BaseColorId) : Color.white;
        }
    }

    protected virtual void OnDestroy() => s_all.Remove(this);

    /// <summary>부식 1회. 다 차면 부서져 콜라이더를 끈다. (호스트 판정 — 멀티 복제는 7단계)</summary>
    public void Corrode()
    {
        if (IsBroken) return;
        corrosion++;
        ApplyCorrosionLook();
        Corroded?.Invoke(this, corrosion);

        if (!IsBroken) return;
        foreach (var col in GetComponentsInChildren<Collider>())
            col.enabled = false;
    }

    /// <summary>부식을 되돌린다 (재도전·페이즈 초기화).</summary>
    public void ResetCorrosion()
    {
        corrosion = 0;
        ApplyCorrosionLook();
        foreach (var col in GetComponentsInChildren<Collider>(true))
            col.enabled = true;
    }

    private void ApplyCorrosionLook()
    {
        block ??= new MaterialPropertyBlock();
        float t = maxCorrosion > 0 ? Mathf.Clamp01((float)corrosion / maxCorrosion) : 0f;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].GetPropertyBlock(block);
            block.SetColor(BaseColorId, Color.Lerp(baseColors[i], corrodedColor, t));
            renderers[i].SetPropertyBlock(block);
            // 부서진 기둥은 반쯤 무너진 느낌으로 낮춘다
            renderers[i].transform.localScale = IsBroken ? Vector3.Scale(baseScales[i], new Vector3(1f, 0.35f, 1f)) : baseScales[i];
        }
    }

    /// <summary>이 위치가 살아 있는 엄폐물의 안전지대 안인지.</summary>
    public static bool IsInSafeZone(Vector3 position)
    {
        foreach (var cover in s_all)
        {
            if (cover == null || !cover.IsIntact) continue;
            Vector3 d = position - cover.transform.position;
            d.y = 0f;
            if (d.sqrMagnitude <= cover.safeRadius * cover.safeRadius) return true;
        }
        return false;
    }

    /// <summary>from → to 선분 사이를 살아 있는 엄폐물이 가로막는지.</summary>
    public static bool IsBlocked(Vector3 from, Vector3 to)
    {
        if (s_all.Count == 0) return false;

        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist < 0.01f) return false;

        var hits = Physics.RaycastAll(from, dir / dist, dist, ~0, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            var cover = hit.collider.GetComponentInParent<DragonCover>();
            if (cover != null && cover.IsIntact) return true;
        }
        return false;
    }

    /// <summary>center 주변 radius 안에 살아 있는 엄폐물 콜라이더가 있으면 그 엄폐물을 돌려준다.</summary>
    public static DragonCover FindOverlap(Vector3 center, float radius)
    {
        if (s_all.Count == 0) return null;

        foreach (var col in Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Ignore))
        {
            var cover = col.GetComponentInParent<DragonCover>();
            if (cover != null && cover.IsIntact) return cover;
        }
        return null;
    }
}
