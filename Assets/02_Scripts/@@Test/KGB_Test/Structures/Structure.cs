using UnityEngine;

//TODO : IDamageable 추가 고려
public abstract class Structure : MonoBehaviour, IInteractable
{

    

    public virtual bool CanInteract(InteractionContext ctx) => true;
    public virtual void Interact(InteractionContext ctx) { }
}