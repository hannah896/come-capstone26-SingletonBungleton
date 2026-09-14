using System;
using UnityEngine;

/// <summary>시간대별 하늘과 조명 설정. 런타임 생성 코드와 별도로 조정할 수 있다.</summary>
[CreateAssetMenu(fileName = "WorldSkyBoxSettings", menuName = "World/World SkyBox Settings")]
public class WorldSkyBoxSettings : ScriptableObject
{
    [Serializable]
    public sealed class PhaseSettings
    {
        public Material Skybox;
        public Color LightColor = Color.white;
        [Min(0f)] public float LightIntensity = 1f;
        [Min(0f)] public float AmbientIntensity = 1f;
    }

    [SerializeField] private PhaseSettings _dawn = new PhaseSettings();
    [SerializeField] private PhaseSettings _morning = new PhaseSettings();
    [SerializeField] private PhaseSettings _afternoon = new PhaseSettings();
    [SerializeField] private PhaseSettings _dusk = new PhaseSettings();

    public PhaseSettings GetPhaseSettings(TimePhase phase) => phase switch
    {
        TimePhase.Dawn => _dawn,
        TimePhase.Morning => _morning,
        TimePhase.Afternoon => _afternoon,
        TimePhase.Dusk => _dusk,
        _ => null,
    };
}
