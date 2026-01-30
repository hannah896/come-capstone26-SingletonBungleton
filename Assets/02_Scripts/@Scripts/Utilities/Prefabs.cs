using UnityEngine;

[CreateAssetMenu(menuName = "Develop/Prefabs", order = 1000)]
public class Prefabs : ScriptableObject {

#if UNITY_EDITOR

    [Header("Base")]
    public GameObject Text;
    public GameObject Image;
    public GameObject Button;
    public GameObject Toggle;

#endif

}