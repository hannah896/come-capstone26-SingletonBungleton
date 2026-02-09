using System;
using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using TMPro;
using UnityEngine;

[CreateAssetMenu(fileName = "FontsSo", menuName = "DuckJam/Font/FontsSo")]
public class FontsSo : ScriptableObject
{
    public SerializedDictionary<LocalizedCountryType, TMP_FontAsset> dictFontsData;
}

/// <summary>
/// 외곽선 설정을 담는 구조체.
/// Dictionary 키로 사용되므로 IEquatable 구현.
/// </summary>
[Serializable]
public struct OutlineSettings : IEquatable<OutlineSettings>
{
    [Tooltip("외곽선 색상")]
    public Color color;

    [Tooltip("외곽선 두께 (0 ~ 1)")]
    [Range(0f, 1f)]
    public float thickness;

    [Tooltip("글자 확장/축소 (-1 ~ 1)")]
    [Range(-1f, 1f)]
    public float dilate;

    public OutlineSettings(Color color, float thickness = 0f, float dilate = 0f)
    {
        this.color = color;
        this.thickness = thickness;
        this.dilate = dilate;
    }

    // Dictionary 키로 사용하기 위한 비교 구현
    public bool Equals(OutlineSettings other)
    {
        return color.Equals(other.color) &&
               Mathf.Approximately(thickness, other.thickness) &&
               Mathf.Approximately(dilate, other.dilate);
    }

    public override bool Equals(object obj)
    {
        return obj is OutlineSettings other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(color, thickness.ToString("F3"), dilate.ToString("F3"));
    }

    public static bool operator ==(OutlineSettings left, OutlineSettings right) => left.Equals(right);
    public static bool operator !=(OutlineSettings left, OutlineSettings right) => !left.Equals(right);

    /// <summary>
    /// 기본 텍스트 색상과 동일한지 확인 (외곽선 미적용 판단용).
    /// </summary>
    public bool IsDefault(Color textColor)
    {
        bool isSameColor = Mathf.Approximately(textColor.r, color.r) &&
                          Mathf.Approximately(textColor.g, color.g) &&
                          Mathf.Approximately(textColor.b, color.b);
        bool isDefaultThickness = Mathf.Approximately(thickness, 0f);
        bool isDefaultDilate = Mathf.Approximately(dilate, 0f);

        return isSameColor && isDefaultThickness && isDefaultDilate;
    }
}

/// <summary>
/// 폰트별 외곽선 머티리얼을 캐싱하는 클래스.
/// </summary>
public class FontData
{
    #region Fields

    // 원본 폰트 에셋
    public TMP_FontAsset originalFont;

    // 외곽선 설정별 머티리얼 캐시
    private readonly Dictionary<OutlineSettings, Material> _materialCache = new();

    #endregion

    #region Properties

    /// <summary>
    /// 캐시된 머티리얼 개수.
    /// </summary>
    public int CachedCount => _materialCache.Count;

    #endregion

    #region Constructor

    public FontData(TMP_FontAsset fontAsset)
    {
        originalFont = fontAsset;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 외곽선 설정에 맞는 머티리얼을 반환합니다 (캐싱됨).
    /// </summary>
    public Material GetMaterial(OutlineSettings settings)
    {
        if (originalFont == null)
        {
            Debug.LogError("originalFont (TMP_FontAsset) is not assigned in FontData!");
            return null;
        }

        if (_materialCache.TryGetValue(settings, out Material cached))
        {
            return cached;
        }

        Material material = CreateOutlineMaterial(settings);
        _materialCache.Add(settings, material);
        return material;
    }

    #endregion

    #region Internal Methods

    // 외곽선 머티리얼 생성
    private Material CreateOutlineMaterial(OutlineSettings settings)
    {
        Material material = new Material(originalFont.material);

        // 외곽선 색상
        material.SetColor(ShaderUtilities.ID_OutlineColor, settings.color);

        // 외곽선 두께
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, settings.thickness);

        // 글자 확장/축소 (Face Dilate)
        material.SetFloat(ShaderUtilities.ID_FaceDilate, settings.dilate);

        return material;
    }

    /// <summary>
    /// 특정 설정의 머티리얼이 캐시되어 있는지 확인합니다.
    /// </summary>
    public bool IsCached(OutlineSettings settings)
    {
        return _materialCache.ContainsKey(settings);
    }

    /// <summary>
    /// 캐시된 모든 머티리얼을 제거합니다.
    /// </summary>
    public void ClearCache()
    {
        foreach (var material in _materialCache.Values)
        {
            if (material != null)
            {
                UnityEngine.Object.Destroy(material);
            }
        }
        _materialCache.Clear();
    }

    /// <summary>
    /// 캐시된 설정 목록을 반환합니다 (디버그용).
    /// </summary>
    public IEnumerable<OutlineSettings> GetCachedSettings()
    {
        return _materialCache.Keys;
    }

    #endregion
}
