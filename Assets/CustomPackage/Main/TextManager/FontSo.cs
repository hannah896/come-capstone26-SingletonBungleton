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

public class FontData
{
    public TMP_FontAsset originalFont;

    public FontData(TMP_FontAsset fontAsset)
    {
        originalFont = fontAsset;
    }
    
    private readonly Dictionary<Color, Material> dictColorToMaterial = new();
    public Material GetMaterial(Color color)
    {
        if (originalFont == null)
        {
            Debug.LogError("originalFont (TMP_FontAsset) is not assigned in FontData!");
            return null; // NullReferenceException 방지
        }
    
        Material material;

        if (dictColorToMaterial.TryGetValue(color, out material)) return material;
    
        material = new Material(originalFont.material); 
        material.SetColor(ShaderUtilities.ID_OutlineColor, color);
    
        dictColorToMaterial.Add(color, material);
        return material;
    }
}