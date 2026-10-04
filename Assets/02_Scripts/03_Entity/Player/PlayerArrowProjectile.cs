using System;
using UnityEngine;

/// <summary>
/// 플레이어가 활로 쏘는 화살 투사체. 화살 모델이 아직 없어서 가는 원기둥 + 뾰족한 끝으로 만든 임시 모양이다.
/// 진짜 화살 프리팹이 생기면 Spawn의 모델 생성 부분만 프리팹 Instantiate로 바꾸면 된다.
///
/// 물리 없이 매 프레임 앞으로 이동하며, 이동 구간을 레이캐스트해서 처음 맞은 대상에 onHit을 호출한다.
/// 데미지 처리는 쏜 쪽(PlayerInventory)이 onHit 안에서 한다 — 멀티에서도 같은 경로(호스트 보고)를 탄다.
/// (화살 모양은 쏜 피어에서만 보인다. 다른 피어는 데미지 결과만 반영된다.)
/// </summary>
public class PlayerArrowProjectile : MonoBehaviour
{
    private const float ShaftLength = 0.6f;
    private const float ShaftRadius = 0.015f;

    private Vector3 direction;
    private float speed;
    private float remainingDistance;
    private LayerMask hitMask;
    private Transform ownerRoot;
    private Action<RaycastHit> onHit;
    private bool finished;

    /// <param name="ownerRoot">쏜 플레이어. 이 계층의 콜라이더는 무시한다.</param>
    /// <param name="onHit">대상(몬스터/동물)을 맞췄을 때 호출. 다른 곳에 맞으면 호출 없이 사라진다.</param>
    public static PlayerArrowProjectile Spawn(Vector3 position, Vector3 direction, float speed, float maxDistance,
        LayerMask hitMask, Transform ownerRoot, Action<RaycastHit> onHit)
    {
        var root = new GameObject("PlayerArrow");
        root.transform.position = position;
        root.transform.rotation = Quaternion.LookRotation(direction);

        // 몸통: 원기둥은 기본이 Y축 방향이라 Z축(앞)으로 눕힌다
        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.name = "Shaft";
        shaft.transform.SetParent(root.transform, false);
        shaft.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        shaft.transform.localScale = new Vector3(ShaftRadius * 2f, ShaftLength * 0.5f, ShaftRadius * 2f);
        Destroy(shaft.GetComponent<Collider>());

        // 화살촉: 앞쪽 끝에 길쭉한 구
        GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        tip.name = "Tip";
        tip.transform.SetParent(root.transform, false);
        tip.transform.localPosition = new Vector3(0f, 0f, ShaftLength * 0.5f);
        tip.transform.localScale = new Vector3(ShaftRadius * 3f, ShaftRadius * 3f, ShaftRadius * 8f);
        Destroy(tip.GetComponent<Collider>());

        PlayerArrowProjectile arrow = root.AddComponent<PlayerArrowProjectile>();
        arrow.direction = direction.normalized;
        arrow.speed = speed;
        arrow.remainingDistance = maxDistance;
        arrow.hitMask = hitMask;
        arrow.ownerRoot = ownerRoot;
        arrow.onHit = onHit;
        return arrow;
    }

    private void Update()
    {
        if (finished) return;

        float step = speed * Time.deltaTime;
        if (TryFindHit(step, out RaycastHit hit))
        {
            transform.position = hit.point;
            finished = true;

            if (IsCombatTarget(hit.collider))
                onHit?.Invoke(hit);

            Destroy(gameObject);
            return;
        }

        transform.position += direction * step;
        remainingDistance -= step;
        if (remainingDistance <= 0f)
        {
            finished = true;
            Destroy(gameObject);
        }
    }

    // 이동 구간에서 가장 가까운 유효 충돌을 찾는다. 자기 자신과 상관없는 트리거(구조물 근접 범위 등)는 건너뛴다.
    private bool TryFindHit(float distance, out RaycastHit best)
    {
        best = default;
        RaycastHit[] hits = Physics.RaycastAll(transform.position, direction, distance, hitMask, QueryTriggerInteraction.Collide);
        float bestDistance = float.MaxValue;
        bool found = false;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider collider = hits[i].collider;
            if (ownerRoot != null && collider.transform.IsChildOf(ownerRoot)) continue;
            if (collider.isTrigger && !IsCombatTarget(collider)) continue;

            if (hits[i].distance < bestDistance)
            {
                bestDistance = hits[i].distance;
                best = hits[i];
                found = true;
            }
        }

        return found;
    }

    private static bool IsCombatTarget(Collider collider)
        => collider.GetComponentInParent<Monster>() != null || collider.GetComponentInParent<Animal>() != null;
}
