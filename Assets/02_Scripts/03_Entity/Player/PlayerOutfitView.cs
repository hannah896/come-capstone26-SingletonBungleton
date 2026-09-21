using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 방어구를 캐릭터 메시 "위에 덮어" 표시하는 뷰.
///
/// 기존 바디(SM_Body/SM_Legs 등)는 그대로 두고 옷 메시만 겹쳐 올린다.
/// 옷 프리팹의 SkinnedMeshRenderer는 자기 스켈레톤에 바인딩돼 있으므로,
/// bones/rootBone을 플레이어의 본으로 이름 기준 교체해 플레이어 애니메이션을 따라가게 한다.
///
/// 옷 프리팹의 구조 자체는 건드리지 않고 bones 배열만 바꾸며, 해제할 때 원래 본으로 되돌린다.
/// 그래서 풀에 반환해도 다음 스폰이 깨지지 않는다.
///
/// 투구처럼 스키닝이 없는 고정 메시는 attachToBone을 켜서 해당 휴머노이드 본의 자식으로 붙인다.
/// 이때 프리팹 루트가 본 원점에 오므로, 위치/크기 보정은 프리팹 안의 자식에서 한다.
///
/// 주의: 옷과 플레이어의 바인드 포즈가 1~3cm 다르므로 살이 옷을 뚫는 클리핑이 생길 수 있다.
/// 그건 오프셋으로 못 고치고 메시 쪽에서 맞춰야 한다.
/// </summary>
public class PlayerOutfitView : MonoBehaviour
{
    /// <summary>방어구 아이템과 실제로 입힐 옷 프리팹 키의 연결.</summary>
    [Serializable]
    public class OutfitBinding
    {
        [Tooltip("ItemDataSO 파일명 (예: Armor_Chestplate)")]
        public string itemKey;

        [Tooltip("입힐 옷 프리팹의 Addressable 키 (예: SM_Ranger_Female)")]
        public string outfitKey;
    }

    [Header("본 계층")]
    [Tooltip("플레이어 스켈레톤의 루트. 비워두면 Animator의 Hips에서 거슬러 올라가 찾는다.")]
    [SerializeField] private Transform skeletonRoot;

    [Header("부착 방식")]
    [Tooltip("켜면 스키닝 대신 아래 본의 자식으로 붙인다 (투구 등 고정 메시용)")]
    [SerializeField] private bool attachToBone;

    [Tooltip("attachToBone일 때 붙일 휴머노이드 본")]
    [SerializeField] private HumanBodyBones attachBone = HumanBodyBones.Head;

    [Header("장착 연결")]
    [Tooltip("이 뷰가 반응할 장착 슬롯")]
    [SerializeField] private EquipSlot watchedSlot = EquipSlot.Chest;

    [SerializeField] private List<OutfitBinding> outfitBindings = new();

    [Tooltip("바인딩에 없는 아이템이 장착됐을 때 대신 입힐 키. 비워두면 아무것도 입히지 않는다.")]
    [SerializeField] private string fallbackOutfitKey;

    [Header("표시 조건")]
    [Tooltip("소유자의 1인칭 화면에서는 옷을 숨긴다 (바디가 숨겨져 있어 옷만 떠 보이지 않도록).")]
    [SerializeField] private bool hideInFirstPerson = true;

    private PlayerInventory inventory;
    private GameObject spawnedOutfit;
    private Renderer[] spawnedRenderers;
    private bool isLocalView = true;

    // 옷의 원래 바인딩. 풀에 되돌리기 전에 그대로 복원한다.
    private readonly List<SkinnedMeshRenderer> reboundRenderers = new();
    private readonly List<Transform[]> originalBones = new();
    private readonly List<Transform> originalRootBones = new();

    // 플레이어 본 이름 → Transform
    private Dictionary<string, Transform> boneMap;

    // 스폰이 비동기라 요청이 연달아 들어오면 순서가 뒤집힐 수 있다 — 최신 요청만 반영한다.
    private int requestSeq;

    /// <summary>현재 입고 있는 옷 오브젝트 (없으면 null).</summary>
    public GameObject SpawnedOutfit => spawnedOutfit;

    /// <summary>소유자 본인인지 지정한다. Player가 1인칭/원격 판정 후 호출.</summary>
    public void SetLocalView(bool value)
    {
        isLocalView = value;
        ApplyVisibility();
    }

    /// <summary>인벤토리의 장착 변경을 구독한다. Player.Start에서 호출.</summary>
    public void BindInventory(PlayerInventory playerInventory)
    {
        if (inventory == playerInventory) return;

        if (inventory != null)
            inventory.OnEquippedItemChanged -= OnEquippedItemChanged;

        inventory = playerInventory;
        if (inventory == null) return;

        inventory.OnEquippedItemChanged += OnEquippedItemChanged;
        Equip(inventory.GetEquippedItem(watchedSlot));
    }

    // 구독 해제는 OnDisable이 아니라 파괴 시점에만 수행한다.
    private void OnDestroy()
    {
        if (inventory != null)
            inventory.OnEquippedItemChanged -= OnEquippedItemChanged;

        Unequip();
    }

    private void OnEquippedItemChanged(EquipSlot slot, ItemDataSO itemData)
    {
        if (slot != watchedSlot) return;
        Equip(itemData);
    }

    /// <summary>방어구를 입힌다. itemData가 null이면 벗긴다.</summary>
    public void Equip(ItemDataSO itemData)
    {
        Unequip();

        if (itemData == null) return;

        string outfitKey = ResolveOutfitKey(itemData.name);
        if (string.IsNullOrWhiteSpace(outfitKey)) return;

        EquipAsync(outfitKey, ++requestSeq).Forget();
    }

    /// <summary>입고 있던 옷을 벗겨 풀로 되돌린다.</summary>
    public void Unequip()
    {
        // 스폰 대기 중인 요청까지 무효화한다.
        requestSeq++;

        RestoreOriginalBinding();

        if (spawnedOutfit == null) return;

        if (Main.Pool != null)
            Extensions.Despawn(spawnedOutfit);

        spawnedOutfit = null;
        spawnedRenderers = null;
    }

    private string ResolveOutfitKey(string itemKey)
    {
        for (int i = 0; i < outfitBindings.Count; i++)
        {
            OutfitBinding binding = outfitBindings[i];
            if (binding != null && binding.itemKey == itemKey)
                return binding.outfitKey;
        }

        return fallbackOutfitKey;
    }

    private async UniTaskVoid EquipAsync(string outfitKey, int seq)
    {
        Transform parent = transform;
        if (attachToBone)
        {
            parent = ResolveAttachBone();
            if (parent == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[PlayerOutfitView] 붙일 본({attachBone})을 찾지 못해 '{outfitKey}'를 입히지 않았습니다.", this);
#endif
                return;
            }
        }

        var token = this.GetCancellationTokenOnDestroy();
        GameObject spawned = await Extensions.SpawnAsync(outfitKey, parent)
            .AttachExternalCancellation(token);

        if (spawned == null) return;

        // 기다리는 동안 장착이 또 바뀌었다면 방금 스폰한 건 버린다.
        if (seq != requestSeq)
        {
            Extensions.Despawn(spawned);
            return;
        }

        spawnedOutfit = spawned;
        spawned.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        spawned.transform.localScale = Vector3.one;

        // 옷이 자기 애니메이터로 본을 움직이면 플레이어 애니메이션과 싸운다.
        Animator outfitAnimator = spawned.GetComponentInChildren<Animator>(true);
        if (outfitAnimator != null)
            outfitAnimator.enabled = false;

        // 본에 직접 붙인 고정 메시는 본을 따라 움직이므로 스키닝 교체가 필요 없다.
        if (!attachToBone)
            RebindToPlayerSkeleton(spawned, outfitKey);

        spawnedRenderers = spawned.GetComponentsInChildren<Renderer>(true);
        SetLayerRecursively(spawned.transform, gameObject.layer);
        ApplyVisibility();
    }

    // 옷의 SkinnedMeshRenderer가 플레이어 본을 따라가도록 bones/rootBone을 이름으로 갈아끼운다.
    private void RebindToPlayerSkeleton(GameObject outfit, string outfitKey)
    {
        Dictionary<string, Transform> bones = GetBoneMap();
        if (bones == null || bones.Count == 0)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[PlayerOutfitView] 플레이어 스켈레톤을 찾지 못해 옷을 본에 붙이지 못했습니다.", this);
#endif
            return;
        }

        HashSet<string> missing = null;

        foreach (SkinnedMeshRenderer renderer in outfit.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Transform[] source = renderer.bones;
            var mapped = new Transform[source.Length];

            for (int i = 0; i < source.Length; i++)
            {
                Transform sourceBone = source[i];
                if (sourceBone != null && bones.TryGetValue(sourceBone.name, out Transform target))
                {
                    mapped[i] = target;
                    continue;
                }

                // 못 찾은 본은 원래 것을 그대로 둔다 (해당 부위만 안 움직일 뿐 깨지지는 않는다)
                mapped[i] = sourceBone;
                if (sourceBone != null)
                    (missing ??= new HashSet<string>()).Add(sourceBone.name);
            }

            // 복원용으로 원래 바인딩을 기록해 둔다
            reboundRenderers.Add(renderer);
            originalBones.Add(source);
            originalRootBones.Add(renderer.rootBone);

            renderer.bones = mapped;

            if (renderer.rootBone != null && bones.TryGetValue(renderer.rootBone.name, out Transform newRoot))
                renderer.rootBone = newRoot;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (missing != null)
        {
            Debug.LogWarning(
                $"[PlayerOutfitView] '{outfitKey}'의 본 중 플레이어 스켈레톤에 없는 것이 있습니다: " +
                string.Join(", ", missing), this);
        }
#endif
    }

    // 옷을 벗길 때 원래 바인딩으로 되돌린다. 안 되돌리면 풀에서 다시 꺼낸 옷이 남의 본을 물고 나온다.
    private void RestoreOriginalBinding()
    {
        for (int i = 0; i < reboundRenderers.Count; i++)
        {
            SkinnedMeshRenderer renderer = reboundRenderers[i];
            if (renderer == null) continue;

            renderer.bones = originalBones[i];
            renderer.rootBone = originalRootBones[i];
        }

        reboundRenderers.Clear();
        originalBones.Clear();
        originalRootBones.Clear();
    }

    private Dictionary<string, Transform> GetBoneMap()
    {
        if (boneMap != null && boneMap.Count > 0) return boneMap;

        Transform root = ResolveSkeletonRoot();
        if (root == null) return null;

        boneMap = new Dictionary<string, Transform>();
        foreach (Transform bone in root.GetComponentsInChildren<Transform>(true))
            boneMap[bone.name] = bone;

        return boneMap;
    }

    private Transform ResolveAttachBone()
    {
        Animator animator = GetComponentInChildren<Animator>(true);
        if (animator == null || !animator.isHuman) return null;

        return animator.GetBoneTransform(attachBone);
    }

    // 인스펙터 지정 > 휴머노이드 Hips의 부모 체인 최상단 순.
    private Transform ResolveSkeletonRoot()
    {
        if (skeletonRoot != null) return skeletonRoot;

        Animator animator = GetComponentInChildren<Animator>(true);
        if (animator == null || !animator.isHuman) return null;

        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        if (hips == null) return null;

        // Hips의 부모(Armature 등)까지 올라가 스켈레톤 전체를 훑을 수 있게 한다.
        skeletonRoot = hips.parent != null ? hips.parent : hips;
        return skeletonRoot;
    }

    // 1인칭이면 내 화면에서만 감춘다 (그림자는 유지, 다른 피어 화면에는 그대로 보인다).
    private void ApplyVisibility()
    {
        if (spawnedRenderers == null) return;

        bool hide = isLocalView && hideInFirstPerson;
        for (int i = 0; i < spawnedRenderers.Length; i++)
        {
            if (spawnedRenderers[i] == null) continue;
            spawnedRenderers[i].shadowCastingMode = hide
                ? ShadowCastingMode.ShadowsOnly
                : ShadowCastingMode.On;
        }
    }

    private static void SetLayerRecursively(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursively(t.GetChild(i), layer);
    }
}
