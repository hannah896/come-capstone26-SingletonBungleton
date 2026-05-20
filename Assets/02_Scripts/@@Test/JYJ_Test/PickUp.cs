using UnityEngine;

/// F키로 아이템 줍기

public class PlayerPickup : MonoBehaviour
{
    [Header("설정")]
    [SerializeField] private float pickupRadius = 1.5f;
    [SerializeField] private KeyCode pickupKey = KeyCode.F;
    [SerializeField] private LayerMask itemLayer;

    private void Update()
    {
        if (Input.GetKeyDown(pickupKey))
            TryPickup();
    }

    private void TryPickup()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position, pickupRadius, itemLayer);

        foreach (var col in hits)
        {
            // DroppedItem 먼저 체크
            if (col.TryGetComponent<DroppedItem>(out var dropped))
            {
                dropped.Pickup();
                break;
            }
            // 아이템 프리팹 체크
            if (col.TryGetComponent<Item>(out var item))
            {
                //item.Pickup();
                break;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}