using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class TweenObject : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private TweenData onPointerDown = new();
    [SerializeField] private TweenData onPointerUp = new();
    
    public void OnPointerDown(PointerEventData eventData)
    {
        
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        
    }
}
