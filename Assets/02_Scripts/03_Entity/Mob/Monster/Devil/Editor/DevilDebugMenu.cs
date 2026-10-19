using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>데빌 플레이 테스트 메뉴 (플레이 모드 전용). 메뉴: Tools/몬스터/테스트/...</summary>
public static class DevilDebugMenu
{
    private static readonly FieldInfo MachineField =
        typeof(Monster).GetField("stateMachine", BindingFlags.NonPublic | BindingFlags.Instance);

    [MenuItem("Tools/몬스터/테스트/데빌 진짜 찾기 시전")]
    private static void CastClones()
    {
        if (!EditorApplication.isPlaying) return;
        foreach (var devil in Object.FindObjectsByType<Devil>(FindObjectsSortMode.None))
        {
            if (devil == null || devil.IsClone || !devil.isActiveAndEnabled) continue;
            if (devil.Target == null) devil.AcquireTarget();
            if (devil.Target == null)
            {
                Debug.LogWarning("[DevilDebug] 데빌이 플레이어를 못 찾았습니다 (가까이 가서 다시 시도)");
                return;
            }
            (MachineField.GetValue(devil) as DevilStateMachine)?.ToCloneCast();
            Debug.Log("[DevilDebug] 진짜 찾기 시전");
            return;
        }
        Debug.LogWarning("[DevilDebug] 데빌이 없습니다 (몬스터 소환기로 먼저 소환)");
    }
}
