using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "SpawnRuleSet", menuName = "Scriptable Objects/TestWorld/SpawnRuleSet")]
public class SpawnRuleSet : ScriptableObject
{
    [Header("플레이어 기준 몬스터 스폰 규칙")]
    [Tooltip("스폰 시점에 플레이어의 현재 좌표, 시간대를 검사합니다. 각 규칙은 하나의 몬스터 종류를 담당합니다.")]
    [FormerlySerializedAs("MonsterRules")]
    [SerializeField] public List<NatureSpawnRule> NatureSpawnRules = new();

    [Header("월드 생성 시 몬스터 배치 규칙")]
    [Tooltip("월드 시드로 지역마다 한 번 배치합니다. 사망 여부는 저장됩니다.")]
    [FormerlySerializedAs("InitialMonsterRules")]
    [SerializeField] public List<InitialSpawnRule> InitialSpawnRules = new();
}
