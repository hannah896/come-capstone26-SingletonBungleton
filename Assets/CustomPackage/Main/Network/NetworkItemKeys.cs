#if PHOTON_FUSION
using System;
using Fusion;
using UnityEngine;

/// <summary>
/// 아이템 Addressable 키를 네트워크 상태에 싣기 위한 압축 도구.
///
/// Fusion 네트워크 오브젝트 하나의 상태는 32KB(8192워드)를 넘을 수 없다.
/// NetworkString&lt;_32&gt;는 한 칸에 수십 워드를 차지해서, 드롭/보관함처럼 수백 칸짜리 테이블에 그대로 넣으면 상한을 넘는다.
/// 그래서 테이블에는 키의 32비트 해시(1워드)만 넣고, 해시 → 실제 키 문자열은 작은 이름표 테이블에 한 번만 기록한다.
/// </summary>
public static class NetworkItemKeys
{
    /// <summary>NetworkString&lt;_32&gt;에 담을 수 있는 최대 키 길이.</summary>
    public const int MaxKeyLength = 31;

    /// <summary>모든 피어에서 같은 값을 내는 FNV-1a 32비트 해시. 0은 "없음" 예약값이라 쓰지 않는다.</summary>
    public static int Hash(string key)
    {
        if (string.IsNullOrEmpty(key)) return 0;

        unchecked
        {
            uint hash = 2166136261;
            for (int i = 0; i < key.Length; i++)
            {
                hash ^= key[i];
                hash *= 16777619;
            }
            return hash == 0 ? 1 : (int)hash;
        }
    }

    /// <summary>
    /// 호스트: 키를 이름표 테이블에 등록하고 해시를 돌려준다.
    /// 테이블이 가득 차면 isReferenced가 false인(더 이상 아무 항목도 쓰지 않는) 이름을 지우고 다시 시도한다.
    /// </summary>
    public static bool TryRegister(NetworkDictionary<int, NetworkString<_32>> names, string key,
                                   Func<int, bool> isReferenced, out int hash)
    {
        hash = Hash(key);
        if (hash == 0 || key.Length > MaxKeyLength)
        {
            Debug.LogError($"[NetworkItemKeys] 동기화할 수 없는 아이템 키입니다(비었거나 {MaxKeyLength}자 초과): {key}");
            return false;
        }

        if (names.TryGet(hash, out NetworkString<_32> existing))
        {
            if (existing.ToString() == key) return true;

            Debug.LogError($"[NetworkItemKeys] 아이템 키 해시 충돌: '{key}' / '{existing}'");
            return false;
        }

        if (names.Count >= names.Capacity)
            PruneUnreferenced(names, isReferenced);

        if (names.Count >= names.Capacity)
        {
            Debug.LogWarning($"[NetworkItemKeys] 아이템 이름표가 가득 찼습니다(Capacity={names.Capacity}): {key}");
            return false;
        }

        names.Set(hash, key);
        return true;
    }

    /// <summary>해시로 실제 키를 찾는다. 없으면 null.</summary>
    public static string Resolve(NetworkDictionary<int, NetworkString<_32>> names, int hash)
        => hash != 0 && names.TryGet(hash, out NetworkString<_32> key) ? key.ToString() : null;

    private static void PruneUnreferenced(NetworkDictionary<int, NetworkString<_32>> names, Func<int, bool> isReferenced)
    {
        var unused = new System.Collections.Generic.List<int>();
        foreach (var pair in names)
        {
            if (!isReferenced(pair.Key)) unused.Add(pair.Key);
        }
        for (int i = 0; i < unused.Count; i++)
            names.Remove(unused[i]);
    }
}
#endif
