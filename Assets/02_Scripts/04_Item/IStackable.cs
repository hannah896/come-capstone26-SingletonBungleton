using UnityEngine;

/// <summary>
/// 스택이 가능한 아이템에 구현하는 인터페이스.
/// AddStack / RemoveStack 은 기본 구현 제공.
/// CanStackWith 는 각 구현 클래스에서 SO 동일 여부까지 추가 체크해야 한다.
/// </summary>
public interface IStackable
{
    int stackCount { get; set; }
    int stackMax { get; }

    /// <summary>
    /// 이 슬롯에 otherSO 아이템을 합칠 수 있는지 확인.
    /// 기본 구현은 공간만 확인 — 서브클래스에서 SO 동일 여부도 함께 체크할 것.
    /// </summary>
    bool CanStackWith(ItemDataSO otherSO)
    {
        // SO 동일 여부는 서브클래스에서 반드시 체크해야 하므로 기본값은 false
        return false;
    }

    /// <summary>스택 추가. 반환값: 추가하지 못한 초과 수량.</summary>
    int AddStack(int amount)
    {
        int space = stackMax - stackCount;
        int toAdd = Mathf.Min(amount, space);
        stackCount += toAdd;
        return amount - toAdd;
    }

    /// <summary>스택 제거. 반환값: 실제로 제거된 수량.</summary>
    int RemoveStack(int amount)
    {
        int toRemove = Mathf.Min(amount, stackCount);
        stackCount -= toRemove;
        return toRemove;
    }
}
