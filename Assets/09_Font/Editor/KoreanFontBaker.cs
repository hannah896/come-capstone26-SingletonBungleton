using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// NEXON Football Gothic(otf)로 한글 SDF 아틀라스를 굽는 에디터 유틸리티.
/// TMP의 Font Asset Creator를 코드로 자동화한다.
/// 메뉴: Tools/Font/Bake NEXON Korean SDF (Bold) / (Light) / (Both)
/// </summary>
public static class KoreanFontBaker
{
    // 소스 폰트(otf)와 출력 SDF 에셋 경로
    private const string BoldOtfPath = "Assets/09_Font/NEXON Football Gothic B.otf";
    private const string BoldSdfPath = "Assets/09_Font/NEXON Football Gothic B SDF.asset";
    private const string LightOtfPath = "Assets/09_Font/NEXON Football Gothic L.otf";
    private const string LightSdfPath = "Assets/09_Font/NEXON Football Gothic L SDF.asset";

    // ── 굽기 스펙 (메모리/품질 트레이드오프 — 필요 시 여기만 조정) ──
    private const int SamplingPointSize = 128;     // 글자 렌더 해상도 (클수록 선명/무거움)
    private const int AtlasPadding = 16;          // SDF 패딩 (얇은 L 폰트 글자 하단 그림자 방지: 박스 밖 SDF가 0까지 떨어질 여유 확보)
    private const int AtlasSize = 4096;           // 한글이 많으므로 큰 아틀라스
    private const bool BakeFullSyllables = true;  // true: 한글 음절 전체(가~힣 11172), false: 음절 굽기 생략(런타임 Dynamic 생성에 위임)

    [MenuItem("Tools/Font/Bake NEXON Korean SDF (Bold)")]
    public static void BakeBold() => Bake(BoldOtfPath, BoldSdfPath, "NEXON Football Gothic B SDF");

    [MenuItem("Tools/Font/Bake NEXON Korean SDF (Light)")]
    public static void BakeLight() => Bake(LightOtfPath, LightSdfPath, "NEXON Football Gothic L SDF");

    [MenuItem("Tools/Font/Bake NEXON Korean SDF (Both)")]
    public static void BakeBoth()
    {
        BakeBold();
        BakeLight();
    }

    // 단일 폰트를 SDF로 굽는다 (otf → SDF 에셋, 기존 경로 덮어쓰기로 guid 유지)
    private static void Bake(string sourceOtfPath, string outputSdfPath, string assetName)
    {
        Font src = AssetDatabase.LoadAssetAtPath<Font>(sourceOtfPath);
        if (src == null)
        {
            Debug.LogError($"[KoreanFontBaker] 소스 폰트를 찾을 수 없습니다: {sourceOtfPath}");
            return;
        }

        // 새 SDF 폰트 에셋 생성 (Dynamic + 멀티 아틀라스: 한 장에 안 들어가면 자동으로 추가 생성)
        TMP_FontAsset fa = TMP_FontAsset.CreateFontAsset(
            src, SamplingPointSize, AtlasPadding, GlyphRenderMode.SDFAA,
            AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: true);

        if (fa == null)
        {
            Debug.LogError($"[KoreanFontBaker] TMP_FontAsset 생성 실패: {assetName}");
            return;
        }
        fa.name = assetName;

        // 구울 문자 집합을 아틀라스에 미리 채운다 (pre-bake)
        string charset = BuildCharset();
        fa.TryAddCharacters(charset, out string missing);

        // 기존 경로에 덮어써서 .meta(guid)를 유지 → FontsSo 등 기존 참조 유지
        AssetDatabase.CreateAsset(fa, outputSdfPath);

        // 아틀라스 텍스처/머티리얼을 서브 에셋으로 포함
        if (fa.material != null)
        {
            fa.material.name = fa.name + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
        }
        foreach (Texture2D tex in fa.atlasTextures)
        {
            if (tex == null) continue;
            if (string.IsNullOrEmpty(tex.name)) tex.name = fa.name + " Atlas";
            AssetDatabase.AddObjectToAsset(tex, fa);
        }

        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(outputSdfPath);

        int missingCount = string.IsNullOrEmpty(missing) ? 0 : missing.Length;
        Debug.Log($"[KoreanFontBaker] '{assetName}' 굽기 완료 — 문자 {fa.characterTable.Count}자, 글리프 {fa.glyphTable.Count}개, " +
                  $"아틀라스 {fa.atlasTextures.Length}장({AtlasSize}px), 누락 {missingCount}자");
    }

    // ASCII + 한글 호환 자모 + (옵션) 한글 음절 전체
    private static string BuildCharset()
    {
        StringBuilder sb = new StringBuilder();

        // 기본 ASCII (영문/숫자/기호)
        for (int c = 0x0020; c <= 0x007E; c++) sb.Append((char)c);

        // 한글 호환 자모 (ㄱ~ㅣ)
        for (int c = 0x3131; c <= 0x3163; c++) sb.Append((char)c);

        // 한글 음절 (가~힣)
        if (BakeFullSyllables)
            for (int c = 0xAC00; c <= 0xD7A3; c++) sb.Append((char)c);

        return sb.ToString();
    }
}
