using UnityEngine;
/// <summary>
/// TODO: 나중에 필요한 경우 실행 함수들 void -> bool 
/// </summary>
#region 상호작용(점화,  등)
// 상호작용 컨텍스트
public readonly struct InteractionContext
{
    public GameObject Instigator { get; }   //  상호작용을 시도하는 주체 (예: 플레이어)
    public Vector3 Point { get; }
    public Vector3 Normal { get; }

    public InteractionContext(GameObject instigator, Vector3 point, Vector3 normal)
    {
        Instigator = instigator;
        Point = point;
        Normal = normal;
    }
}

//  상호작용 인터페이스
public interface IInteractable
{
    bool CanInteract(InteractionContext context);
    void Interact(InteractionContext context);
}
#endregion 

#region 데미지(채굴, 벌목, 공격 당함 등)
// 데미지 컨텍스트
public readonly struct DamageContext
{
    //  데미지를 가하는 주체
    public GameObject Instigator { get; }
    public Vector3 Point { get; }
    public int Amount { get; }
    public string ToolId { get; }

    public DamageContext(GameObject instigator, Vector3 point, int amount, string toolId)
    {
        Instigator = instigator;
        Point = point;
        Amount = amount;
        ToolId = toolId;
    }
}
//  데미지 적용 인터페이스
public interface IDamageable
{
    bool CanDamage(DamageContext context);
    void ApplyDamage(DamageContext context);
}
#endregion

#region 수집(채집, 뽑기 등)
// 자원 수집 컨텍스트
public readonly struct GatherContext
{
    public GameObject Instigator { get; }
    public Vector3 Point { get; }

    public GatherContext(GameObject instigator, Vector3 point)
    {
        Instigator = instigator;
        Point = point;
    }
}

//  자원 수집 인터페이스
public interface IGatherable
{
    bool CanGather(GatherContext context);
    void Gather(GatherContext context);
}
#endregion


//  배치 초기화 인터페이스
public interface IDisposeInitializable
{
    void InitializeDispose(DisposeData dispose, ChunkData chunk);
}