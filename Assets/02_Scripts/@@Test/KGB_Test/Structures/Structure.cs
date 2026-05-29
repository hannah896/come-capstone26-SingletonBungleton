using UnityEngine;

//TODO : IDamageable 추가 고려
public abstract class Structure : MonoBehaviour, IInteractable
{
    public abstract StationType StationType { get; }

    public virtual bool CanInteract(InteractionContext ctx) => true;
    public virtual void Interact(InteractionContext ctx) { }
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