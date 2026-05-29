using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Mob 사망 시 떨어뜨릴 드랍 항목들을 정의하는 ScriptableObject.
/// 여러 Mob이 공유·재사용할 수 있다. DropData 타입은 ResourceNodeDataSO.cs 정의를 그대로 사용.
/// </summary>
[CreateAssetMenu(fileName = "DropTableSO", menuName = "Scriptable Objects/DropTableSO")]
public class DropTableSO : ScriptableObject
{
    [Header("Drops")]
    public DropData[] Drops;

    public float DropRadius = 0.5f;
    public string DropFxPrefabKey;

    /// <summary>
    /// origin 위치를 중심으로 드랍을 스폰한다.
    /// </summary>
    public UniTask Spawn(Vector3 origin)
        => DropSpawner.SpawnAsync(Drops, origin, DropRadius);
}
