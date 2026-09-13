using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SpawnRuleSet", menuName = "Scriptable Objects/TestWorld/SpawnRuleSet")]
public class SpawnRuleSet : ScriptableObject
{
    [Header("플레이어 기준 몬스터 스폰 규칙")]
    [Tooltip("스폰 시점에 플레이어의 현재 좌표, 시간대를 검사합니다. 각 규칙은 하나의 몬스터 종류를 담당합니다.")]
    [SerializeField] public List<MonsterSpawnRule> MonsterRules = new();
}
