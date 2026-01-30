using UnityEngine;

public class Build_ObjectType : MonoBehaviour
{
    public MapObjectType Type;
    public int TileCount;

    public void Init(MapObjectType type, int count)
    {
        Type = type;
        TileCount = count;
    }
}
