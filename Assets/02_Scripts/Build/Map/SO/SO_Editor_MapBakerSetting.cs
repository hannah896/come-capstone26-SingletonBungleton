using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "SO_MapBakerSettings", menuName = "ScriptableObjects/Build/Baker Settings")]
public class MapBakerSettings : ScriptableObject
{
    public Texture2D MapImage;
    public Vector2Int MapSize = new Vector2Int(100, 100);
    [Range(0f, 1f)] public float Sensitivity = 0.1f;
    public List<FloorMapping> FloorMappings = new List<FloorMapping>();
    public List<ObjectMapping> ObjectMappings = new List<ObjectMapping>();
}