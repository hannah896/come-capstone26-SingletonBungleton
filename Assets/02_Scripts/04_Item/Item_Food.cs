using UnityEditor.Localization.Plugins.XLIFF.V12;
using UnityEngine;

public interface IValidityPeriod
{
    // 현재 신선도 (음식)
    public float CurrentFreshness { get; set; }
    public float CreatedTime { get; set; }
    bool Consume();
}
public class Item_Food : Item, IValidityPeriod
{
    #region Fields
    private float _currentFreshness;
    private float _createdTime;
    #endregion

    #region Properties
    public float CurrentFreshness { get => _currentFreshness; set => _currentFreshness = value; }
    public float CreatedTime { get => _createdTime; set => _createdTime = value; }
    #endregion

    protected override void Init(ItemDataSO data)
    {
        base.Init(data);


        if (data.hasFreshness)
        {
            _currentFreshness = data.maxFreshness;
            var _createdTime = Time.time;
        }
    }


    #region === 음식 및 신선도 로직 ===

    /// 경과 시간에 따른 신선도 갱신 (인벤토리에서 호출)
    public void UpdateFreshness()
    {
        if (!itemData.hasFreshness) return;

        float elapsedTime = Time.time - _createdTime;
        CurrentFreshness = Mathf.Max(0, itemData.maxFreshness - elapsedTime);

        if (CurrentFreshness <= 0)
            Debug.Log($"[Item] {itemData.itemName}이(가) 부패했습니다.");
    }



    /// 아이템 소비 (음식 타입 전용)
    public bool Consume()
    {
        if (itemData.itemType != ItemType.Food || stackCount <= 0) return false;
        if (itemData.hasFreshness && CurrentFreshness <= 0) return false;

        stackCount--;
        ApplySurvivalEffects();
        return true;
    }

    public override bool CanStackWith(Item other)
    {
        var _food = other as Item_Food;
        base.CanStackWith(other);

        if (_food == null) 
            return true;

        // 음식은 신선도 차이가 적을 때만 합침 (예: 5분 이내)
        if (itemData.hasFreshness && Mathf.Abs(_currentFreshness - _food.CurrentFreshness) > 300f)
            return false;

        return true;
    }

    public override string ToString()
    {
        string info = base.ToString();
        if (itemData.hasFreshness) info += $" [신선도: {_currentFreshness:F0}%]";
        return info;
    }
    #endregion
}