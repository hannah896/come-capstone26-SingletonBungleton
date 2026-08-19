using UnityEngine;

/// <summary>
/// 모든 건축물의 공통 부모 클래스. CanInteract는 기본적으로 true
/// </summary>
public abstract class Structure : MonoBehaviour, IInteractable
{
    public virtual bool CanInteract(InteractionContext interactionCtx) => true;
    public virtual void Interact(InteractionContext interactionCtx) { }

    /// <summary>망치로 부술 수 있는 상태인지. 보관함류는 비어있어야 부술 수 있도록 오버라이드한다.</summary>
    public virtual bool CanDemolish() => true;

    /// <summary>망치로 부순다. 아무것도 드롭하지 않고 그냥 사라진다.</summary>
    public virtual void Demolish() => Destroy(gameObject);
}

// 건축물 타입 정의    
public enum StationType
{
    None,        // 맨손 (Survival 카테고리 — 횃불, 모닥불, 망치 같은 기초)
    Workbench,   // 제작대
    Bonfire,    // 모닥불
    Chest,       // 상자
    CookingPot,  // 요리솥
    Furnace      // 화덕
}