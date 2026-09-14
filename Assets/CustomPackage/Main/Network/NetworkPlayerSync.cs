#if PHOTON_FUSION
using Fusion;
using UnityEngine;

/// <summary>
/// 플레이어 캐릭터 위치/회전/애니메이션 동기화 (Host 모드).
/// 플레이어 프리팹(NetworkObject 포함)에 부착한다.
///
/// 동작 방식:
/// - 입력 권한 클라이언트: 로컬 시뮬레이션(HFSM/PlayerMotor)이 transform과 애니메이터를 직접 구동하고,
///   그 결과를 NetworkManager.OnInput이 Fusion Input(위치/Yaw/애니 파라미터)으로 호스트에 보고한다.
/// - 호스트: 보고받은 값을 transform·애니메이터와 Networked 프로퍼티에 확정 기록한다.
///   호스트 자신의 캐릭터는 로컬 시뮬레이션 값을 그대로 기록한다.
/// - 프록시(그 외 모든 피어): Networked 값을 Render에서 보간해 표시한다.
///
/// [애니메이션]
/// 원격 플레이어는 Player.OnLoopUpdate에서 상태 머신이 돌지 않으므로 애니메이터를 아무도 구동하지 않는다.
/// 그래서 소유자의 애니메이터 파라미터를 그대로 복제해 원격 피어에서 재생한다.
/// 애니메이션 상태를 열거형으로 따로 정의하지 않고 <see cref="Animator.parameters"/>를 런타임에 읽어
/// 일반적으로 처리하므로, 애니메이터 컨트롤러를 수정할 필요가 없고
/// 남/여 컨트롤러의 파라미터 구성이 서로 달라도(같은 이름이 한쪽은 Bool, 다른 쪽은 Trigger) 그대로 동작한다.
/// 같은 플레이어의 캐릭터는 모든 피어에서 동일한 프리팹으로 스폰되므로 파라미터 순서도 동일하다.
///
/// [장착 아이템]
/// 소유자 화면의 장착 뷰는 1인칭 뷰모델(ViewModel 레이어 + 전용 ToolCamera)이라 남의 화면에는 렌더되지 않는다.
/// 그래서 손에 든 아이템의 Addressable 키(= ItemDataSO 파일명)를 복제하고,
/// 원격 피어는 <see cref="PlayerEquipmentView"/>로 손 본에 실제 프리팹을 붙여 보여준다.
/// </summary>
public class NetworkPlayerSync : NetworkBehaviour
{
    #region Networked Properties

    // 호스트가 확정한 위치/Yaw (프록시가 보간 대상)
    [Networked] private Vector3 NetPosition { get; set; }
    [Networked] private float NetYaw { get; set; }

    // 호스트가 확정한 애니메이터 Bool 파라미터 비트마스크
    [Networked] private ushort NetAnimBools { get; set; }

    // 호스트가 확정한 마지막 Trigger 파라미터 인덱스 + 1 (0 = 없음)
    [Networked] private byte NetAnimTriggerIndex { get; set; }

    // Trigger 발동 시퀀스 — 값이 바뀔 때만 원격에서 트리거를 재발동한다
    [Networked] private byte NetAnimTriggerSeq { get; set; }

<<<<<<< Updated upstream
=======
    // 호스트가 확정한 마지막 CrossFade 대상 인덱스 + 1 (0 = 없음)
    [Networked] private byte NetAnimCrossFadeIndex { get; set; }

    // CrossFade 시퀀스 — 값이 바뀔 때만 원격에서 CrossFade를 재현한다
    [Networked] private byte NetAnimCrossFadeSeq { get; set; }

    // 손에 장착한 아이템의 Addressable 키 (= ItemDataSO 파일명). 빈 문자열 = 맨손.
    // 아이템 이름은 최대 22자라 _32면 충분하다.
    [Networked] private NetworkString<_32> NetEquippedHandKey { get; set; }

>>>>>>> Stashed changes
    #endregion

    #region Fields

    // 프록시 보간 속도 (클수록 빠르게 따라붙음)
    [SerializeField] private float _lerpSpeed = 12f;

    // 이 거리 이상 벌어지면 보간 없이 스냅 (텔레포트/스폰 직후 대비)
    [SerializeField] private float _snapDistance = 8f;

    // 비트마스크 한 칸에 담을 수 있는 Bool 파라미터 수
    private const int MaxBoolParams = 16;

    private Animator _animator;

    // 애니메이터 파라미터 해시 (컨트롤러에 정의된 순서 = 비트/인덱스 순서)
    private int[] _boolHashes;
    private int[] _triggerHashes;

    // 소유자 전용 — 마지막으로 발동한 트리거와 그 시퀀스
    private byte _localTriggerIndex;
    private byte _localTriggerSeq;

    // 원격 전용 — 마지막으로 재현한 트리거 시퀀스
    private byte _appliedTriggerSeq;

    // 트리거 이벤트 구독 해제를 위해 보관
    private PlayerAnimData _animData;

    // 소유자 전용 — 장착 변경 구독 해제를 위해 보관
    private PlayerInventory _inventory;

    // 원격 전용 — 손 본에 실제 아이템을 붙여 보여주는 뷰
    private PlayerEquipmentView _equipmentView;

    // 원격 전용 — 마지막으로 뷰에 반영한 장착 키
    private string _appliedEquipKey;

    #endregion

    #region Lifecycle

    public override void Spawned()
    {
        CacheAnimator();

        if (Object.HasInputAuthority)
        {
            // 자기 캐릭터: OnInput 수집 대상으로 등록 (transform·애니메이터를 읽어 호스트로 보고)
            Main.Network?.RegisterLocalCharacter(Object);

            // Trigger는 애니메이터에서 되읽을 수 없으므로 발동 시점을 이벤트로 잡는다
            _animData = GetComponent<Player>()?.AnimData;
            if (_animData != null)
                _animData.OnTriggerPlayed += OnLocalTriggerPlayed;
<<<<<<< Updated upstream
=======

                // CrossFade(사망 연출·부활 복귀 등 상태 직접 전환)도 파라미터로는 표현되지 않아 따로 잡는다
                _animData.OnCrossFadePlayed += OnLocalCrossFadePlayed;
            }

            // 손에 든 아이템이 바뀔 때마다 호스트로 보고한다 (남의 화면에 그려주기 위함)
            _inventory = GetComponent<PlayerInventory>();
            if (_inventory != null)
            {
                _inventory.OnEquippedItemChanged += OnLocalEquippedItemChanged;

                // 재접속·리스폰 등으로 이미 장착한 상태로 스폰될 수 있으므로 현재 값을 한 번 보고한다
                ReportEquippedHand(GetItemKey(_inventory.EquippedHand));
            }
        }
        else
        {
            // 남의 캐릭터: 손 본에 실제 아이템을 붙여 보여주는 뷰를 준비한다.
            // (소유자 자신은 1인칭 뷰모델이 따로 있어 붙이지 않는다)
            _equipmentView = Extensions.GetOrAddComponent<PlayerEquipmentView>(gameObject);
>>>>>>> Stashed changes
        }

        if (Object.HasStateAuthority)
        {
            // 호스트: 스폰 위치로 초기화
            NetPosition = transform.position;
            NetYaw = transform.eulerAngles.y;
        }
        else if (!Object.HasInputAuthority)
        {
            // 프록시: 첫 프레임은 확정 값으로 즉시 스냅
            transform.SetPositionAndRotation(NetPosition, Quaternion.Euler(0f, NetYaw, 0f));
        }

        // 뒤늦게 합류한 피어가 이미 지나간 트리거를 다시 재생하지 않도록 현재 시퀀스를 기준점으로 삼는다
        _appliedTriggerSeq = NetAnimTriggerSeq;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (_animData != null)
        {
            _animData.OnTriggerPlayed -= OnLocalTriggerPlayed;
            _animData = null;
        }

        if (_inventory != null)
        {
            _inventory.OnEquippedItemChanged -= OnLocalEquippedItemChanged;
            _inventory = null;
        }

        if (Object.HasInputAuthority)
        {
            Main.Network?.UnregisterLocalCharacter(Object);
        }
    }

    public override void FixedUpdateNetwork()
    {
        // 상태 확정은 호스트(StateAuthority) 전담
        if (!Object.HasStateAuthority) return;

        if (Object.HasInputAuthority)
        {
            // 호스트 자신의 캐릭터: 로컬 시뮬레이션이 이미 transform·애니메이터를 구동했다 → 그대로 확정
            NetPosition = transform.position;
            NetYaw = transform.eulerAngles.y;

            NetAnimBools = PackBools();
            NetAnimTriggerIndex = _localTriggerIndex;
            NetAnimTriggerSeq = _localTriggerSeq;
        }
        else if (GetInput(out NetworkInputData input) && input.HasCharacterState)
        {
            // 원격 클라이언트 캐릭터: 클라가 보고한 로컬 시뮬레이션 결과를 확정
            transform.SetPositionAndRotation(
                input.CharacterPosition, Quaternion.Euler(0f, input.CharacterYaw, 0f));
            NetPosition = input.CharacterPosition;
            NetYaw = input.CharacterYaw;

            NetAnimBools = input.AnimBools;
            NetAnimTriggerIndex = input.AnimTriggerIndex;
            NetAnimTriggerSeq = input.AnimTriggerSeq;
        }
    }

    public override void Render()
    {
        // 자기 캐릭터(입력 권한)는 로컬 시뮬레이션이 그린다 — 복제 값으로 덮지 않는다
        if (Object.HasInputAuthority) return;

        // 남의 캐릭터는 상태 머신이 돌지 않으므로 애니메이터를 복제 값으로 직접 구동한다.
        // (호스트가 보는 원격 캐릭터도 마찬가지 — 호스트에서도 그 캐릭터의 상태 머신은 돌지 않는다)
        ApplyAnimState();
<<<<<<< Updated upstream
=======
        ApplyCrossFade();
        ApplyEquipmentView();
>>>>>>> Stashed changes

        // 호스트가 보는 원격 캐릭터의 위치는 FixedUpdateNetwork에서 이미 적용됨
        if (Object.HasStateAuthority) return;

        // 프록시: 확정 값으로 부드럽게 보간
        Vector3 targetPos = NetPosition;
        if ((transform.position - targetPos).sqrMagnitude > _snapDistance * _snapDistance)
        {
            transform.SetPositionAndRotation(targetPos, Quaternion.Euler(0f, NetYaw, 0f));
            return;
        }

        float t = 1f - Mathf.Exp(-_lerpSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, targetPos, t);
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, NetYaw, 0f), t);
    }

    #endregion

    #region 애니메이션 동기화

    /// <summary>
    /// 소유자의 현재 애니메이터 상태를 Fusion Input에 실어 호스트로 보고한다.
    /// (NetworkManager.OnInput에서 호출)
    /// </summary>
    public void WriteAnimState(ref NetworkInputData input)
    {
        if (_animator == null) return;

        input.AnimBools = PackBools();
        input.AnimTriggerIndex = _localTriggerIndex;
        input.AnimTriggerSeq = _localTriggerSeq;
    }

    // 애니메이터와 파라미터 해시 목록을 캐싱한다.
    private void CacheAnimator()
    {
        _animator = GetComponentInChildren<Animator>(true);
        if (_animator == null || _animator.runtimeAnimatorController == null)
        {
            _boolHashes = new int[0];
            _triggerHashes = new int[0];
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[NetworkPlayerSync] Animator를 찾지 못해 애니메이션 동기화가 비활성화됩니다.", this);
#endif
            return;
        }

        var boolHashes = new System.Collections.Generic.List<int>();
        var triggerHashes = new System.Collections.Generic.List<int>();

        foreach (AnimatorControllerParameter p in _animator.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Bool) boolHashes.Add(p.nameHash);
            else if (p.type == AnimatorControllerParameterType.Trigger) triggerHashes.Add(p.nameHash);
        }

        _boolHashes = boolHashes.ToArray();
        _triggerHashes = triggerHashes.ToArray();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_boolHashes.Length > MaxBoolParams)
        {
            Debug.LogError(
                $"[NetworkPlayerSync] Bool 파라미터가 {_boolHashes.Length}개로 상한({MaxBoolParams})을 넘었습니다. " +
                "초과분은 동기화되지 않습니다. NetworkInputData.AnimBools를 더 넓은 타입으로 바꾸세요.", this);
        }
#endif
    }

    // 소유자: 현재 Bool 파라미터 값을 비트마스크로 압축
    private ushort PackBools()
    {
        if (_animator == null || _boolHashes == null) return 0;

        ushort mask = 0;
        int count = Mathf.Min(_boolHashes.Length, MaxBoolParams);
        for (int i = 0; i < count; i++)
        {
            if (_animator.GetBool(_boolHashes[i]))
                mask |= (ushort)(1 << i);
        }
        return mask;
    }

    // 원격: 복제된 값으로 애니메이터를 구동
    private void ApplyAnimState()
    {
        if (_animator == null || _boolHashes == null) return;

        ushort mask = NetAnimBools;
        int count = Mathf.Min(_boolHashes.Length, MaxBoolParams);
        for (int i = 0; i < count; i++)
        {
            _animator.SetBool(_boolHashes[i], (mask & (1 << i)) != 0);
        }

        // 시퀀스가 바뀐 경우에만 1회 발동 (같은 트리거가 연속 발동해도 시퀀스가 달라 구분된다)
        byte seq = NetAnimTriggerSeq;
        if (seq == _appliedTriggerSeq) return;

        _appliedTriggerSeq = seq;

        int index = NetAnimTriggerIndex - 1;
        if (_triggerHashes != null && index >= 0 && index < _triggerHashes.Length)
        {
            _animator.SetTrigger(_triggerHashes[index]);
        }
    }

    // 소유자: 로컬에서 Trigger가 발동됐다 — 인덱스와 시퀀스를 갱신해 다음 Input에 실어 보낸다
    private void OnLocalTriggerPlayed(int animHash)
    {
        if (_triggerHashes == null) return;

        for (int i = 0; i < _triggerHashes.Length; i++)
        {
            if (_triggerHashes[i] != animHash) continue;

            _localTriggerIndex = (byte)(i + 1);
            _localTriggerSeq++;
            return;
        }
    }

    #endregion

    #region 장착 아이템 동기화

    // 소유자: 장착이 바뀌었다 — 손 슬롯만 호스트로 보고한다
    private void OnLocalEquippedItemChanged(EquipSlot slot, ItemDataSO itemData)
    {
        if (slot != EquipSlot.Hand) return;

        ReportEquippedHand(GetItemKey(itemData));
    }

    // 소유자 → 호스트. 호스트 자신의 캐릭터라면 RPC 없이 바로 확정한다.
    private void ReportEquippedHand(string key)
    {
        key ??= string.Empty;

        if (Object.HasStateAuthority)
            NetEquippedHandKey = key;
        else
            Rpc_ReportEquippedHand(key);
    }

    // 장착은 프레임마다 바뀌지 않으므로 매 틱 Input에 싣지 않고 변경 시점에만 RPC로 보낸다.
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void Rpc_ReportEquippedHand(NetworkString<_32> key)
    {
        NetEquippedHandKey = key;
    }

    // 원격: 복제된 키가 바뀌면 손 본의 아이템 뷰를 교체한다.
    // (뒤늦게 합류한 피어도 _appliedEquipKey가 null이라 첫 프레임에 현재 장착이 반영된다)
    private void ApplyEquipmentView()
    {
        if (_equipmentView == null) return;

        string key = NetEquippedHandKey.Value ?? string.Empty;
        if (key == _appliedEquipKey) return;

        _appliedEquipKey = key;
        _equipmentView.SetHandItem(key);
    }

    // 아이템 프리팹의 Addressable 키는 ItemDataSO 파일명이다 (PlayerFirstPersonCameraController와 동일 규칙)
    private static string GetItemKey(ItemDataSO itemData)
    {
        return itemData != null ? itemData.name : string.Empty;
    }

    #endregion
}
#endif
