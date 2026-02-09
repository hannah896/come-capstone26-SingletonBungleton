using UnityEditor.Localization.Plugins.XLIFF.V12;
using UnityEngine;
using UnityEngine.Splines;

/// 게임 내에 실제로 존재하는 아이템 인스턴스 (데이터 실체)

[System.Serializable]
public abstract class Item : MonoBehaviour
{
    // === 데이터 및 상태 ===
    public ItemDataSO itemData;        // 원본 데이터(설계도)
    public int stackCount;             // 현재 수량

    private void Awake()
    {
        Init();
    }

    /// 아이템 생성 및 초기화
    protected virtual void Init(ItemDataSO data)
    {
        itemData = data;
        stackCount = Mathf.Min(0, data.maxStack);
    } 

    protected virtual void Init()
    {
        if (itemData == null)
        {
            Debug.Log($"[Item] {gameObject.name} 프리펩 내에 SO 데이터가 존재하지 않습니다. ");
            return;
        }
        stackCount = Mathf.Min(0, itemData.maxStack);
    }

    protected virtual void ApplySurvivalEffects()
    {
        // TODO: Survival Manager 연동
        if (itemData.hungerRestore > 0) Debug.Log($"배고픔 +{itemData.hungerRestore}");
        if (itemData.healthRestore > 0) Debug.Log($"체력 +{itemData.healthRestore}");
        if (itemData.sanityRestore > 0) Debug.Log($"정신력 +{itemData.sanityRestore}");
    }

    // === 스택 및 수량 관리 ===

    /// 대상 아이템과 겹치기가 가능한지 확인
    public virtual bool CanStackWith(Item other)
    {
        if (other == null || other.itemData != this.itemData) return false;
        if (!itemData.isStackable || stackCount >= itemData.maxStack) return false;

        

        return true;
    }

    /// 수량 추가 후 남은 수량 반환
    public int AddStack(int amount)
    {
        int space = itemData.maxStack - stackCount;
        int toAdd = Mathf.Min(amount, space);
        stackCount += toAdd;
        return amount - toAdd;
    }

    /// 수량 제거 후 실제 제거된 수량 반환
    public int RemoveStack(int amount)
    {
        int toRemove = Mathf.Min(amount, stackCount);
        stackCount -= toRemove;
        return toRemove;
    }



    public override string ToString()
    {
        string info = $"{itemData.itemName} x{stackCount}";
        return info;
    }
}