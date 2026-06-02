using UnityEngine;
/// <summary>
/// TODO: 나중에 필요한 경우 실행 함수들 void -> bool 
/// </summary>
#region 상호작용 - 범용

/// <summary>
/// 상호작용 컨텍스트 instigator: 상호작용을 시도하는 주체, point: 상호작용이 발생하는 지점, normal: 상호작용이 발생한 표면의 법선 벡터
/// </summary>
public readonly struct InteractionContext
{
    public GameObject Instigator { get; }   //  상호작용을 시도하는 주체 (예: 플레이어)
    public Vector3 Point { get; }           //  상호작용이 발생하는 지점 (예: 클릭한 위치)
    public Vector3 Normal { get; }          //  상호작용이 발생한 표면의 법선 벡터 

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
    public GameObject Instigator { get; }
    public Vector3 Point { get; }
    public int Amount { get; }
    public string ToolId { get; }
    /// <summary>도구 종류 (자원 노드의 requiredTool 체크에 사용)</summary>
    public ActionType ActionType { get; }

    public DamageContext(GameObject instigator, Vector3 point, int amount, string toolId,
                         ActionType actionType = ActionType.None)
    {
        Instigator = instigator;
        Point = point;
        Amount = amount;
        ToolId = toolId;
        ActionType = actionType;
    }
}
//  데미지 적용 인터페이스
public interface IDamageable
{
    bool CanDamage(DamageContext damageCtx);
    void ApplyDamage(DamageContext damageCtx);
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


public interface IDisposeInitializable
{
    void InitializeDispose(DisposeData dispose, ChunkData chunk);
}