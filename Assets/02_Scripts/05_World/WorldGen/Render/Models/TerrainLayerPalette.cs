using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 지형 페인팅에 사용할 텍스처 데이터(물감 통)를 보관하는 순수 데이터 클래스
/// </summary>
public class TerrainLayerPalette
{
    public TerrainLayer[] Layers;
    public Dictionary<string, int> IndexMap = new();

    // 나중에 메모리 해제를 위해 키값들도 보관해 둡니다.
    public List<string> LoadedKeys = new();
}