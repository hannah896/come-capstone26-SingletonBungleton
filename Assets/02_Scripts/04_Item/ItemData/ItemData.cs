/// <summary>
/// 실제 게임 내에서 런타임의 아이템의 데이터 클래스 
/// </summary>
[System.Serializable]
public abstract class ItemData
{
    public ItemDataSO data;

    public ItemData(ItemDataSO data, int count = 1)
    {
        this.data = data;
    }
}