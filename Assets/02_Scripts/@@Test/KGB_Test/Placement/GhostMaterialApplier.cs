using System.Collections.Generic;
using UnityEngine;

public class GhostMaterialApplier
{
    private readonly List<MaterialOverride> materialOverrides = new();
    struct MaterialOverride
    {
        public Renderer renderer;
        public Material[] originalMaterials; // 원본 (복원용, 여기선 안 씀)
        public Material[] ghostMaterials;    // 복제 머티리얼
    }

    public void ApplyGhostMaterial(GameObject target, Color tint)
    {
        materialOverrides.Clear();

        foreach(Renderer renderer in target.GetComponentsInChildren<Renderer>())
        {
            Material[] originalMats = renderer.sharedMaterials;
            Material[] ghostMats = new Material[originalMats.Length];
            for (int i = 0; i < originalMats.Length; i++)
            {
                if (originalMats[i] == null) { ghostMats[i] = null; continue; }
                // 원본 머티리얼을 복제 (텍스처, 노멀맵 등 다 유지됨)
                var clone = new Material(originalMats[i]);
                MakeTransparent(clone);
                ApplyTint(clone, tint);
                ghostMats[i] = clone;
            }

            renderer.sharedMaterials = ghostMats; // 인스턴스 머티리얼로 교체 (씬 전체에 영향 안 줌)
            materialOverrides.Add(new MaterialOverride
            {
                renderer = renderer,
                originalMaterials = originalMats,
                ghostMaterials = ghostMats
            });
        }
    }


    /// <summary>
    /// 색조만 바꾸고 싶을 때 (valid ↔ invalid 전환). 머티리얼 재생성 안 함.
    /// </summary>
    public void SetTint(Color tint)
    {
        for (int i = 0; i < materialOverrides.Count; i++)
        {
            var mats = materialOverrides[i].ghostMaterials;
            for (int j = 0; j < mats.Length; j++)
                if (mats[j] != null) ApplyTint(mats[j], tint);
        }
    }

    /// <summary>고스트 인스턴스가 Destroy될 때 호출. 복제한 머티리얼 정리.</summary>
    public void Cleanup()
    {
        foreach (var ov in materialOverrides)
            foreach (var m in ov.ghostMaterials)
                if (m != null) UnityEngine.Object.Destroy(m);
        materialOverrides.Clear();
    }

    static void ApplyTint(Material m, Color tint)
    {
        // URP Lit / Unlit
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
        // Built-in Standard
        else if (m.HasProperty("_Color")) m.SetColor("_Color", tint);

        // 이미시브 살짝 (선택사항 - 어두운 환경에서도 색조 보이게)
        if (m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", tint * 0.3f);
        }
    }

    static void MakeTransparent(Material m)
    {
        // URP Lit/Unlit: Surface Type = Transparent
        if (m.HasProperty("_Surface"))
        {
            m.SetFloat("_Surface", 1f); // 0=Opaque, 1=Transparent
            m.SetFloat("_Blend", 0f);   // 0=Alpha
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        // Built-in Standard
        else if (m.HasProperty("_Mode"))
        {
            m.SetFloat("_Mode", 3f); // Transparent
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
        }
    }
}
