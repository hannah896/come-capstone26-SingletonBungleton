using UnityEngine;

//TODO : IDamageable 추가 고려
/// <summary>
/// 모든 건축물의 공통 부모 클래스. CanInteract는 기본적으로 true
/// </summary>
public abstract class Structure : MonoBehaviour, IInteractable
{
    public virtual bool CanInteract(InteractionContext interactionCtx) => true;
    public virtual void Interact(InteractionContext interactionCtx) { }
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