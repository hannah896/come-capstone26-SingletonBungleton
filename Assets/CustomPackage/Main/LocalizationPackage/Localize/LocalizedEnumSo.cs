using AYellowpaper.SerializedCollections;
using UnityEngine;

[CreateAssetMenu(fileName = "LocalizedEnumSo", menuName = "DuckJam/LocalizedEnumSo")]
public class LocalizedEnumSo : ScriptableObject
{
    [SerializeField]
    public SerializedDictionary<int, string> DictValueToString = new SerializedDictionary<int, string>();
}