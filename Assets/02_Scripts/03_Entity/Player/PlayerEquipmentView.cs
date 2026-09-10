using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 다른 피어의 화면에 보이는 "월드 장착 뷰".
///
/// <see cref="PlayerFirstPersonCameraController"/>가 만드는 장착 뷰는 소유자 전용 1인칭 뷰모델이라
/// ViewModel 레이어에 올라가고 소유자의 ToolCamera에만 렌더된다 — 남의 화면에는 아무것도 보이지 않는다.
/// 이 컴포넌트는 그 대신 캐릭터의 손 본에 아이템 프리팹을 실제로 붙여, 모든 피어의 메인 카메라에 렌더되게 한다.
///
/// 표시할 아이템은 <see cref="NetworkPlayerSync"/>가 복제해 온 Addressable 키(= ItemDataSO 파일명)로 지정된다.
/// 소유자 자신(입력 권한 보유)에게는 붙이지 않는다 — 1인칭 뷰모델과 겹쳐 보이기 때문.
/// </summary>
public class PlayerEquipmentView : MonoBehaviour
{
    /// <summary>아이템별 손 부착 오프셋. itemKey가 일치하면 기본 오프셋 대신 적용된다.</summary>
    [Serializable]
    public class AttachOverride
    {
        [Tooltip("ItemDataSO 파일명 (= Addressable 키)")]
        public string itemKey;
        public Vector3 localPosition;
        public Vector3 localEuler;
        public Vector3 localScale = Vector3.one;
    }

    [Header("부착 위치")]
    [Tooltip("비워두면 Animator에서 오른손 본을 자동으로 찾는다.")]
    [SerializeField] private Transform handAttachPoint;

    [Header("기본 오프셋 (손 본 로컬 기준)")]
    [SerializeField] private Vector3 defaultLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 defaultLocalEuler = Vector3.zero;
    [SerializeField] private Vector3 defaultLocalScale = Vector3.one;

    [Header("아이템별 오프셋")]
    [SerializeField] private List<AttachOverride> attachOverrides = new();

    [Header("횃불 조명")]
    [SerializeField] private float torchLightIntensity = 3.5f;
    [SerializeField] private float torchLightRange = 7f;
    [SerializeField] private Color torchLightColor = new(1f, 0.45f, 0.18f, 1f);

    // 손 본 자동 탐색 실패 시 이름으로 찾을 후보 (Mixamo / UE / 일반 명명 규칙)
    private static readonly string[] HandBoneNameHints =
    {
        "mixamorig:RightHand", "RightHand", "hand_r", "Hand_R", "R_Hand", "Bip001 R Hand"
    };

    private GameObject _spawnedObject;

    // 현재 화면에 붙어 있는(또는 붙는 중인) 아이템 키
    private string _currentKey = string.Empty;

    // 스폰이 비동기라 요청이 연달아 들어오면 순서가 뒤집힐 수 있다 — 최신 요청만 반영한다.
    private int _requestSeq;

    /// <summary>현재 표시 중인 아이템 키 (없으면 빈 문자열).</summary>
    public string CurrentKey => _currentKey;

    /// <summary>
    /// 표시할 장착 아이템을 지정한다. 빈 문자열이면 해제.
    /// 같은 키가 다시 들어오면 아무것도 하지 않는다.
    /// </summary>
    public void SetHandItem(string itemKey)
    {
        itemKey ??= string.Empty;
        if (_currentKey == itemKey) return;

        _currentKey = itemKey;
        _requestSeq++;

        ClearView();

        if (string.IsNullOrEmpty(itemKey)) return;

        SpawnViewAsync(itemKey, _requestSeq).Forget();
    }

    private void OnDestroy()
    {
        ClearView();
    }

    private async UniTaskVoid SpawnViewAsync(string itemKey, int seq)
    {
        Transform attachPoint = ResolveAttachPoint();
        if (attachPoint == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                "[PlayerEquipmentView] 손 본을 찾지 못해 장착 아이템을 표시할 수 없습니다. " +
                "handAttachPoint를 직접 지정하세요.", this);
#endif
            return;
        }

        var token = this.GetCancellationTokenOnDestroy();
        GameObject spawned = await Extensions.SpawnAsync(itemKey, attachPoint)
            .AttachExternalCancellation(token);

        if (spawned == null) return;

        // 기다리는 동안 장착이 또 바뀌었다면 방금 스폰한 건 버린다.
        if (seq != _requestSeq)
        {
            Extensions.Despawn(spawned);
            return;
        }

        _spawnedObject = spawned;
        ApplyAttachTransform(itemKey);
        ApplyViewState(itemKey);
    }

    // 손 본을 찾는다. 인스펙터 지정 > 휴머노이드 본 > 이름 탐색 순.
    private Transform ResolveAttachPoint()
    {
        if (handAttachPoint != null) return handAttachPoint;

        Animator animator = GetComponentInChildren<Animator>(true);
        if (animator != null && animator.isHuman)
        {
            Transform bone = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (bone != null)
            {
                handAttachPoint = bone;
                return handAttachPoint;
            }
        }

        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < HandBoneNameHints.Length; i++)
        {
            for (int c = 0; c < children.Length; c++)
            {
                if (children[c].name != HandBoneNameHints[i]) continue;
                handAttachPoint = children[c];
                return handAttachPoint;
            }
        }

        return null;
    }

    private void ApplyAttachTransform(string itemKey)
    {
        if (_spawnedObject == null) return;

        Vector3 position = defaultLocalPosition;
        Vector3 euler = defaultLocalEuler;
        Vector3 scale = defaultLocalScale;

        for (int i = 0; i < attachOverrides.Count; i++)
        {
            AttachOverride entry = attachOverrides[i];
            if (entry == null || entry.itemKey != itemKey) continue;

            position = entry.localPosition;
            euler = entry.localEuler;
            scale = entry.localScale;
            break;
        }

        Transform t = _spawnedObject.transform;
        t.localPosition = position;
        t.localRotation = Quaternion.Euler(euler);
        t.localScale = scale;
    }

    // 손에 들린 상태로 만든다 — 물리/픽업 비활성화, 플레이어 레이어로 이동, 횃불 점등.
    private void ApplyViewState(string itemKey)
    {
        if (_spawnedObject == null) return;

        RemovePhysics(_spawnedObject);
        SetLayerRecursively(_spawnedObject.transform, gameObject.layer);
        ConfigureTorchLight(itemKey);
    }

    // 손에 붙은 뷰가 다른 플레이어에게 주워지거나 캐릭터를 밀지 않도록 물리 요소를 제거한다.
    private static void RemovePhysics(GameObject root)
    {
        Rigidbody[] rigidbodies = root.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            rigidbodies[i].isKinematic = true;
            rigidbodies[i].detectCollisions = false;
            Destroy(rigidbodies[i]);
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;
    }

    private static void SetLayerRecursively(Transform t, int layer)
    {
        // Light는 그대로 둔다 (레이어를 바꾸면 컬링 마스크에서 빠질 수 있음)
        if (t.GetComponent<Light>() == null)
            t.gameObject.layer = layer;

        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursively(t.GetChild(i), layer);
    }

    // 원격 캐릭터가 든 횃불도 불이 보이도록 켠다. (Item.BindTorchLight는 IsLit 기본값(꺼짐)으로 시작한다)
    private void ConfigureTorchLight(string itemKey)
    {
        Item item = _spawnedObject.GetComponentInChildren<Item>(true);
        ItemDataSO itemSO = item != null ? item.ItemDataSO : null;
        if (itemSO == null || itemSO.survivalToolType != SurvivalToolType.Torch) return;

        Light[] lights = _spawnedObject.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light == null) continue;

            light.enabled = true;
            light.color = torchLightColor;
            light.intensity = Mathf.Max(light.intensity, torchLightIntensity);
            light.range = Mathf.Max(light.range, torchLightRange);
            light.cullingMask = Physics.AllLayers;
        }
    }

    private void ClearView()
    {
        if (_spawnedObject == null) return;

        if (Main.Pool != null)
            Extensions.Despawn(_spawnedObject);

        _spawnedObject = null;
    }
}
