using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEditor;
using UnityEngine;

namespace SingletonBungleton.BossTraining.Editor
{
    /// <summary>
    /// 아레나 프리팹의 Boss에 BossAgent / BehaviorParameters / DecisionRequester를 붙이고 값을 맞춘다.
    /// ML-Agents 패키지가 설치돼 있을 때만 나타나는 메뉴다.
    /// 메뉴: Tools/보스 학습/보스 에이전트 연결
    /// </summary>
    public static class BossAgentSetup
    {
        private const string ArenaPrefabPath = "Assets/03_Prefabs/Training/BossTrainingArena.prefab";

        [MenuItem("Tools/보스 학습/보스 에이전트 연결")]
        public static void Attach()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ArenaPrefabPath) == null)
            {
                Debug.LogError($"[보스 학습] 아레나 프리팹이 없습니다. 먼저 Tools/보스 학습/아레나 씬 생성을 실행하세요: {ArenaPrefabPath}");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(ArenaPrefabPath);
            try
            {
                Transform boss = root.transform.Find("Boss");
                if (boss == null)
                {
                    Debug.LogError("[보스 학습] 프리팹에서 Boss 오브젝트를 찾지 못했습니다.");
                    return;
                }

                // BehaviorParameters가 Agent보다 먼저 있어야 Agent 초기화 시 에러가 나지 않는다
                var behavior = GetOrAdd<BehaviorParameters>(boss.gameObject);
                behavior.BehaviorName = BossAgent.BehaviorNameDefault;
                behavior.BehaviorType = BehaviorType.Default;
                behavior.BrainParameters.VectorObservationSize = BossAgent.ObservationSize;
                behavior.BrainParameters.NumStackedVectorObservations = 1;
                behavior.BrainParameters.ActionSpec = new ActionSpec(2, new[] { 3 });

                var agent = GetOrAdd<BossAgent>(boss.gameObject);
                agent.MaxStep = 0;   // 시간 제한은 아레나가 EpisodeInterrupted로 처리한다

                var requester = GetOrAdd<DecisionRequester>(boss.gameObject);
                requester.DecisionPeriod = 5;
                requester.TakeActionsBetweenDecisions = true;

                PrefabUtility.SaveAsPrefabAsset(root, ArenaPrefabPath);
                Debug.Log("[보스 학습] 아레나 프리팹의 Boss에 BossAgent를 연결했습니다. (관측 15 / 연속 2 + 이산 3종)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var existing = go.GetComponent<T>();
            return existing != null ? existing : go.AddComponent<T>();
        }
    }
}

