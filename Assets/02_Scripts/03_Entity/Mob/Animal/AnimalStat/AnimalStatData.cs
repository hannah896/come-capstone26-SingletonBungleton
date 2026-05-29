using UnityEngine;

[CreateAssetMenu(fileName = "AnimalStatData", menuName = "Scriptable Objects/AnimalStatData")]
public class AnimalStatData : EntityStatData
{
    [Header("동물")]
    public float FleeRange;   // 위협 감지 시 도망 시작 거리
}
