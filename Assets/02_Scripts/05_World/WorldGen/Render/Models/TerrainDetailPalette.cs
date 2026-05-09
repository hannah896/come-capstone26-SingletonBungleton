using System.Collections.Generic;
using UnityEngine;

public class TerrainDetailPalette
{
    public DetailPrototype[] Prototypes;
    public Dictionary<string, int> IndexMap = new();
    public List<string> LoadedKeys = new();
}