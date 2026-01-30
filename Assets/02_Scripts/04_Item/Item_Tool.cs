using UnityEditor.Localization.Plugins.XLIFF.V12;
using UnityEngine;


public interface IEquipable
{
    public int CurrentDurability { get; set; }      // 현재 내구도 (도구/무기)
    public void UseDurability (int durability);
}


public class Item_Tool : Item, IEquipable
{
    private int _currentDurability;
    public int CurrentDurability { get => _currentDurability; set => _currentDurability = value; }

    public override string ToString()
    {
        string info = base.ToString();
        if (itemData.hasDurability) info += $" [내구도: {_currentDurability:F0}%]";
        return info;
    }

    public void UseDurability(int amount=1)
    {
        if (!itemData.hasDurability) return;

        _currentDurability = Mathf.Max(0, _currentDurability - amount);
        if (
            _currentDurability <= 0) Debug.Log($"{itemData.itemName} 파손됨.");
    }

    protected override void Init(ItemDataSO data)
    {
        base.Init(data);
        _currentDurability = data.maxDurability;
    }
}
