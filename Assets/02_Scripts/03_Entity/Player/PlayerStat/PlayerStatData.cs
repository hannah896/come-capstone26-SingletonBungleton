using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "PlayerStatData", menuName = "Scriptable Objects/PlayerStatData")]
public class PlayerStatData : EntityStatData
{
    [Header("이동")]
    public float SprintMultiplier = 1.5f;
    public float JumpForce = 8f;

    [Header("월드 시간")]
    public float DayDurationMinutes = 12f;

    [Header("허기")]
    public float MaxHunger = 150f;
    public float CurHunger = 150f;
    public float HungerDrain;
    public float HungerHPDecreaseRate;

    [Header("Ego")]
    [FormerlySerializedAs("MaxMental")]
    public float MaxEgo;
    [FormerlySerializedAs("CurMental")]
    public float CurEgo;
    [FormerlySerializedAs("MentalDecreaseRate")]
    public float EgoDecreaseRate;

    [Header("체온")]
    public float Temperature;
    public float ColdHPDecreaseRate;
    public float HotHPDecreaseRate;
    public float MaxColdTemperature;
    public float MaxHotTemperature;

    [Header("습도")]
    public float Wetness;
    public float MaxWetness;
    public float WetnessTemperatureDecreaseRate;
    public float WaterProofWetness;
}
