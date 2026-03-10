using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "EntityStatData", menuName = "Scriptable Objects/EntityStatData")]
public class EntityStatData : ScriptableObject
{
    [Header("체력")]
    public float MaxHP;
    public float CurHP;

    [Header("이동")]
    public float MoveSpeed;

    [Header("전투 스텟")]
    public float AttackDamage; // 공격력
    public float Defense; // 방어력
    public float MinAttackPeriod; // 최소 공격 주기 (공격 사이의 최소 시간)
}
