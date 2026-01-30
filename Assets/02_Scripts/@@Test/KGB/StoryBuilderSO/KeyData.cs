using UnityEngine;

[CreateAssetMenu(fileName = "New Key", menuName = "ScriptableObjects/TestWorld/Key Data")]
public class KeyData : ScriptableObject
{
    [Tooltip("키의 고유 ID (예: Axe, Pickaxe, FireStaff)")]
    [SerializeField] private string keyID;
    public string KeyID => keyID;

    [Tooltip("디버깅용 설명")]
    [SerializeField] private string description;
    public string Description => description;

    // 필요하다면 아이콘 같은 것도 여기에 추가 가능
    // public Sprite icon; 
}