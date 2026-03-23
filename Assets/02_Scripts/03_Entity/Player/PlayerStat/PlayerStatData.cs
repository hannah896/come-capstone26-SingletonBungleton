using UnityEngine;

[CreateAssetMenu(fileName = "PlayerStatData", menuName = "Scriptable Objects/PlayerStatData")]
public class PlayerStatData : EntityStatData
{
    [Header("이동 확장")]
    public float SprintMultiplier = 1.5f;
    public float JumpForce = 8f;

    [Header("허기")]
    public float MaxHunger;
    public float CurHunger;
    public float HungerDrain;
    public float HungerHPDecreaseRate; // 허기가 0이 되었을 때 체력 감소 비율


    [Header("정신력")]
    public float MaxMental;
    public float CurMental;
    public float MentalDecreaseRate; // 정신력이 감소하는 비율

    [Header("체온")]
    public float Temperature;
    public float ColdHPDecreaseRate; // 체온이 낮아졌을 때 체력 감소 비율
    public float HotHPDecreaseRate; // 체온이 높아졌을 때 체력 감소 비율
    public float MaxColdTemperature; // 견딜 수 있는 마지노선 추위 체온
    public float MaxHotTemperature; // 견딜 수 있는 마지노선 추위 체온


    [Header("습도")]
    public float Wetness;
    public float MaxWetness; // 견딜 수 있는 마지노선 습도
    public float WetnessTemperatureDecreaseRate; // 습도가 높아졌을 때 체온 감소 비율
}
