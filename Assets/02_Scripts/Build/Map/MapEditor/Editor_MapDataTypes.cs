using UnityEngine;
using UnityEngine.Tilemaps;

public enum FloorType { None, Grass, Dirt, Sand }
public enum MapObjectType { None, Water, Tree, Road }

[System.Serializable]
public class FloorMapping
{
    public string Name;
    public Color Color;
    public TileBase Tile;
}

[System.Serializable]
public class ObjectMapping
{
    public string Name;
    public Color Color;
    public MapObjectType Type;
    public TileBase Tile;
    public bool Mass;
    [Range(0f, 1f)] public float Sensitivity = 0.1f;
}