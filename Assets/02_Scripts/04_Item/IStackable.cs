using UnityEngine;

/// <summary>
/// 스텍을 쌓을 수 있는 아이템 타입이 상속받는 인터페이스
/// </summary>
public interface IStackable
{
    public int stackCount { get; set; }
    public int stackMax { get; }

    /// <summary>
    /// 스텍합칠수 있는지 여부
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public bool CanStackWith(ItemData other)
    {
        return stackCount < stackMax;
    }

    /// <summary>
    /// 스텍 합치기  - 반환값: 실제로 추가하지 못한 수량 (초과분)
    /// </summary>
    /// <param name="amount"></param>
    /// <returns></returns>
    public int AddStack(int amount)
    {
        int space = stackMax - stackCount;
        int toAdd = Mathf.Min(amount, space);
        stackCount += toAdd;
        return amount - toAdd;
    }

    /// <summary>
    /// 스텍 없애기 - 반환값: 실제로 제거된 수량
    /// </summary>
    /// <param name="amount"></param>
    /// <returns></returns>
    public int RemoveStack(int amount)
    {
        int toRemove = Mathf.Min(amount, stackCount);
        stackCount -= toRemove;
        return toRemove;
    }
}
