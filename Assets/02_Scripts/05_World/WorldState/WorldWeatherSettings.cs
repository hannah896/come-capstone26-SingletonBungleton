using UnityEngine;

/// <summary>지역 날씨를 로컬 화면에 표시할 프리팹과 전환 설정입니다.</summary>
[CreateAssetMenu(fileName = "WorldWeatherSettings", menuName = "World/World Weather Settings")]
public class WorldWeatherSettings : ScriptableObject
{
    public GameObject RainPrefab;
    public GameObject SnowPrefab;
    public AudioClip RainLoop;
    [Range(0f, 1f)] public float RainVolume = 0.5f;
    [Min(0f)] public float TransitionSeconds = 3f;
    [Range(0f, 1f)] public float RainIntensity = 0.5f;
    [Range(0f, 1f)] public float SnowIntensity = 1f;
    [Min(0f)] public float SnowEmissionRate = 100f;
}
