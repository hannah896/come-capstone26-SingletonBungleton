using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Lock", menuName = "ScriptableObjects/TestWorld/Lock Data")]
public class LockData : ScriptableObject
{
    [Tooltip("잠금의 종류 (예: Rock_Blockage, River)")]
    [SerializeField] private string lockID;
    public string LockID => lockID;

    [Tooltip("이 잠금을 해제할 수 있는 열쇠 목록")]
    [SerializeField] private List<KeyData> validKeys;
    public List<KeyData> ValidKeys => validKeys;

    public bool CanUnlock(List<KeyData> preTaskKeys)
    {
        foreach (var key in validKeys)
        {
            if (preTaskKeys.Contains(key))
                return true; // 하나라도 있으면 해제 성공!
        }
        return false; // 하나도 없으면 실패
    }
}