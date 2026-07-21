using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 몬스터 종류 카탈로그. 네트워크로 Addressable 키 문자열을 매 틱 보낼 수 없으므로
/// 종류마다 1바이트 ID를 부여해 그 ID만 복제하고, 양쪽 피어가 이 표로 실제 프리팹/스탯을 찾는다.
///
/// 에셋 위치: Resources/ConfigData/MonsterCatalog.asset (ScriptableObjectSingleton 규약)
/// 주의: CatalogId는 리스트 인덱스 + 1이다. 순서를 바꾸거나 중간을 지우면 기존 세션/세이브와 어긋난다.
/// 새 몬스터는 반드시 리스트 끝에 추가할 것.
/// </summary>
[CreateAssetMenu(fileName = "MonsterCatalog", menuName = "Game/Monster/MonsterCatalog")]
public class MonsterCatalog : ScriptableObjectSingleton<MonsterCatalog>
{
    [Serializable]
    public class Entry
    {
        [Tooltip("몬스터 프리팹의 Addressable 키")]
        public string addressableKey;

        [Tooltip("스폰 시 주입할 스탯 SO. 비워두면 프리팹에 박힌 statData를 그대로 쓴다.")]
        public MonsterStatData statData;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    public IReadOnlyList<Entry> Entries => entries;

    /// <summary>CatalogId(1-based)로 항목을 찾는다. 0이나 범위를 벗어나면 null.</summary>
    public Entry GetEntry(byte catalogId)
    {
        int index = catalogId - 1;
        if (index < 0 || index >= entries.Count) return null;
        return entries[index];
    }

    /// <summary>Addressable 키로 CatalogId(1-based)를 찾는다. 없으면 0.</summary>
    public byte GetCatalogId(string addressableKey)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null && entries[i].addressableKey == addressableKey)
                return (byte)(i + 1);
        }

        return 0;
    }
}
