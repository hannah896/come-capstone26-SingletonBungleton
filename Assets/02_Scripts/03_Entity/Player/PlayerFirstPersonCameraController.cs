using Cysharp.Threading.Tasks;
using DG.Tweening;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 1인칭 시점 카메라 컨트롤러 (Cinemachine 3.x 연동)
/// - 마우스 X → 플레이어 몸체 Yaw 회전
/// - 마우스 Y → 눈 피벗(EyePivot) Pitch 회전
/// - CinemachineCamera는 EyePivot을 Follow + 회전 상속하여 1인칭 시점 구현
/// </summary>
public class PlayerFirstPersonCameraController : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Transform eyePivot;         // 눈 위치 피벗 (플레이어 자식)
    [SerializeField] private CinemachineCamera fpCamera; // 1인칭 시네머신 카메라

    [Header("회전 설정")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    // 마우스 감도 PlayerPrefs 키 (설정 UI와 공유)
    public const string MouseSensitivityKey = "Setting_MouseSensitivity";

    [Header("1인칭 표시")]
    [SerializeField] private bool hidePlayerBodyInFirstPerson = true;
    [SerializeField] private Transform toolPivot;

#if UNITY_EDITOR
    [Tooltip("Play 중에도 씬 뷰에서는 몸을 그린다. 게임 화면은 그대로 숨겨진다. (에디터 전용)")]
    [SerializeField] private bool showBodyInSceneView = true;

    [Tooltip("씬 뷰에 CharacterController 캡슐과 발 높이를 와이어로 그린다. (에디터 전용)")]
    [SerializeField] private bool drawCapsuleGizmo = true;
#endif

    [Header("도구 카메라")]
    [SerializeField] private string viewModelLayerName = "ViewModel";
    [SerializeField] private float toolCameraFov = 55f;
    [SerializeField] private float toolCameraNearClip = 0.01f;
    [SerializeField] private float toolCameraFarClip = 10f;
    [SerializeField] private Vector3 equippedToolLocalPosition = new(-1f, 0f, 3.2f);
    [SerializeField] private Vector3 equippedToolLocalEuler = Vector3.zero;
    [SerializeField] private Vector3 equippedToolLocalScale = Vector3.one;
    [SerializeField] private Vector3 axeMeshLocalPosition = new(1.38f, -1.5f, 2.56f);
    [SerializeField] private Vector3 axeMeshLocalEuler = new(26.14f, -168.4f, -12.8f);
    [SerializeField] private Vector3 axeMeshLocalScale = new(7f, 7f, 1f);
    [SerializeField] private Vector3 pickaxeLocalPosition = new(-0.07f, -0.86f, 2.33f);
    [SerializeField] private Vector3 pickaxeLocalEuler = new(6.952f, -148.7f, -5.915f);
    [SerializeField] private Vector3 pickaxeLocalScale = new(7f, 7f, 7f);
    [SerializeField] private Vector3 torchLocalPosition = new(-1f, 0.25f, 3.2f);
    [SerializeField] private Vector3 torchLocalEuler = new(0f, 0f, -8f);
    [SerializeField] private Vector3 torchLocalScale = new(0.18f, 0.85f, 0.18f);
    [SerializeField] private Vector3 hammerMeshLocalPosition = new(1.38f, -1.5f, 2.56f);
    [SerializeField] private Vector3 hammerMeshLocalEuler = new(26.14f, -168.4f, -12.8f);
    [SerializeField] private Vector3 hammerMeshLocalScale = new(7f, 7f, 7f);
    [SerializeField] private Vector3 shovelLocalPosition = new(-0.07f, -0.86f, 2.33f);
    [SerializeField] private Vector3 shovelLocalEuler = new(6.952f, -148.7f, -5.915f);
    [SerializeField] private Vector3 shovelLocalScale = new(7f, 7f, 7f);
    [SerializeField] private Vector3 weaponLocalPosition = new(-1f, 0f, 3.2f);
    [SerializeField] private Vector3 weaponLocalEuler = Vector3.zero;
    [SerializeField] private Vector3 weaponLocalScale = Vector3.one;
    [SerializeField] private Vector3 weaponMeshLocalPosition = new(1.38f, -1.5f, 2.56f);
    [SerializeField] private Vector3 weaponMeshLocalEuler = new(26.14f, -168.4f, -12.8f);
    [SerializeField] private Vector3 weaponMeshLocalScale = new(7f, 7f, 7f);
    [SerializeField] private Vector3 shieldLocalPosition = new(-1f, 0f, 3.2f);
    [SerializeField] private Vector3 shieldLocalEuler = Vector3.zero;
    [SerializeField] private Vector3 shieldLocalScale = Vector3.one;
    [SerializeField] private Vector3 shieldMeshLocalPosition = new(1.38f, -1.5f, 2.56f);
    [SerializeField] private Vector3 shieldMeshLocalEuler = new(26.14f, -168.4f, -12.8f);
    [SerializeField] private Vector3 shieldMeshLocalScale = new(7f, 7f, 7f);

    [Header("토치 조명")]
    [SerializeField] private string equippedLightLayerName = "Default";
    [SerializeField] private float equippedTorchLightIntensity = 3.5f;
    [SerializeField] private float equippedTorchLightRange = 7f;
    [SerializeField] private Color equippedTorchLightColor = new(1f, 0.45f, 0.18f, 1f);

    private float yaw;
    private float pitch;
    private float pitchOffset;

    // 진행 중인 스윙 흔들림 트윈 (연타 시 중복 누적 방지용)
    private Tween swingShakeTween;
    private PlayerInputData inputData;
    private Transform playerBody;
    private PlayerInventory playerInventory;
    private GameObject equippedToolObject;
    private IEquipable equippedToolEquipable;
    private Renderer[] bodyRenderers;

    // 몸에 나중에 붙는 렌더러 (액션 중 손에 든 도구 등). 몸과 같은 규칙으로 숨긴다.
    private Renderer[] attachedBodyRenderers;
    private bool isLocalView = true;
    private Camera toolCamera;
    private Camera stackedBaseCamera;
    private int viewModelLayer = -1;
    private int equippedLightLayer;


    private void OnValidate()
    {
        toolPivot = transform.Find("EyePivot/ToolPivot");
    }

    /// <summary>
    /// 원격 플레이어용 — 몸체 렌더러만 초기화한다 (입력/카메라 바인딩 없음)
    /// </summary>
    public void InitBodyRenderers(Transform body)
    {
        if (body == null) return;
        playerBody = body;
        CacheBodyRenderers();
        ApplyBodyVisibility();
    }

    /// <summary>
    /// Player.Start()에서 호출 — 입력 데이터와 몸체 Transform 바인딩
    /// </summary>
    public void Bind(PlayerInputData data, Transform body)
    {
        inputData = data;
        playerBody = body;
        yaw = body.eulerAngles.y;
        pitch = 0f;

        // 저장된 마우스 감도 로드 (없으면 인스펙터 기본값 유지)
        mouseSensitivity = PlayerPrefs.GetFloat(MouseSensitivityKey, mouseSensitivity);

        CacheBodyRenderers();
        ApplyBodyVisibility();
    }

    /// <summary>
    /// 마우스 감도를 런타임에 변경한다. (설정 UI에서 호출)
    /// </summary>
    public void SetMouseSensitivity(float value)
    {
        mouseSensitivity = Mathf.Max(0.01f, value);
    }

    public void SetLocalView(bool value)
    {
        isLocalView = value;
        ApplyBodyVisibility();
    }

    public void BindInventory(PlayerInventory inventory)
    {
        if (!isLocalView) return;
        if (playerInventory == inventory) return;

        if (playerInventory != null)
            playerInventory.OnEquippedItemChanged -= OnEquippedItemChanged;

        playerInventory = inventory;

        if (playerInventory == null) return;

        playerInventory.OnEquippedItemChanged += OnEquippedItemChanged;
        PlaceEquippedToolUnderPivot(playerInventory.EquippedHand).Forget();
    }

    /// <summary>
    /// 도구 휘두르기 — 1인칭 도구 스윙 + 카메라 시야 흔들림.
    ///
    /// 구간 길이는 액션 하위 상태(PlayerActionSubStateBase)가 넘겨준다.
    /// 바디 애니메이션(Begin→Loop→Stop)에서 실측한 값이므로, 도구가 최고점에 오르는 순간과
    /// 내려찍는 순간이 캐릭터 모델의 동작과 일치한다. 임의로 상수를 박지 말 것.
    /// </summary>
    /// <param name="riseDuration">도구를 최고점까지 들어올리는 시간</param>
    /// <param name="strikeDuration">최고점에서 타격 지점까지 내려찍는 시간</param>
    /// <param name="recoverDuration">타격 후 원위치로 돌아오는 시간</param>
    public void PlayToolSwing(float riseDuration, float strikeDuration, float recoverDuration)
    {
        if (toolPivot == null) return;

        riseDuration = Mathf.Max(0.01f, riseDuration);
        strikeDuration = Mathf.Max(0.01f, strikeDuration);
        recoverDuration = Mathf.Max(0.01f, recoverDuration);

        toolPivot.DOKill();
        Quaternion restRotation = toolPivot.localRotation;

        DOTween.Sequence()
            // 1. 들어올리기 + 오른쪽으로 당기기 (Begin 클립 ~ Loop 클립의 최고점)
            .Append(toolPivot.DOLocalRotate(new Vector3(-45f, 20f, 8f), riseDuration, RotateMode.LocalAxisAdd)
                .SetEase(Ease.OutQuad))
            // 2. 내려찍기 + 왼쪽 아크 (바디가 실제로 타격하는 순간에 끝난다)
            .Append(toolPivot.DOLocalRotate(new Vector3(100f, -35f, -12f), strikeDuration, RotateMode.LocalAxisAdd)
                .SetEase(Ease.InQuart))
            // 3. 원위치 복귀 (Stop 클립이 끝나는 시점까지)
            .Append(toolPivot.DOLocalRotateQuaternion(restRotation, recoverDuration)
                .SetEase(Ease.OutQuad));

        // 카메라 시야: 내려찍는 타이밍에 흔들림
        // 연타(벌목 루프)로 트윈이 겹쳐 쌓이지 않도록 이전 흔들림은 먼저 걷어낸다.
        swingShakeTween?.Kill();
        swingShakeTween = DOTween.To(() => pitchOffset, x => pitchOffset = x, 6f, strikeDuration)
            .SetDelay(riseDuration)
            .SetEase(Ease.InQuad)
            .OnComplete(() =>
                swingShakeTween = DOTween.To(() => pitchOffset, x => pitchOffset = x, 0f, recoverDuration)
                    .SetEase(Ease.OutQuad));
    }

    /// <summary>
    /// FP_CinemachineCamera 어드레서블을 로드하여 카메라를 동적 생성한다.
    /// fpCamera가 이미 주입된 경우(SetCamera) 스킵.
    /// </summary>
    public async UniTask InitCameraAsync()
    {
        if (!isLocalView) return;

        if (fpCamera == null)
        {
            var cam = await Extensions.Instantiate<CinemachineCamera>("FP_CinemachineCamera");
            fpCamera = cam;
            fpCamera.Follow = eyePivot;
        }

        ConfigureToolCamera();
    }

    /// <summary>
    /// 외부에서 이미 생성된 카메라를 직접 주입할 때 사용
    /// </summary>
    public void SetCamera(CinemachineCamera cam)
    {
        if (!isLocalView) return;
        fpCamera = cam;
        if (fpCamera != null) fpCamera.Follow = eyePivot;
        ConfigureToolCamera();
    }

    public Transform EyePivot => eyePivot;
    public CinemachineCamera FPCamera => fpCamera;

    private void OnEnable()
    {
        Main.Loop.OnLateUpdate += OnLateUpdateLoop;

#if UNITY_EDITOR
        // 카메라 단위로 몸 표시를 갈라주기 위한 훅. 중복 구독을 막고 OnDestroy에서만 해제한다.
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
#endif
    }

    private void OnDisable()
    {
        Main.Loop.OnLateUpdate -= OnLateUpdateLoop;

        if (playerInventory != null)
            playerInventory.OnEquippedItemChanged -= OnEquippedItemChanged;

        ClearEquippedToolView();
        RemoveToolCameraFromStack();
    }

#if UNITY_EDITOR
    private void OnDestroy()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }

    /// <summary>
    /// 씬 뷰에서만 몸을 그린다. (에디터 전용)
    ///
    /// 1인칭에서 몸을 숨기는 수단인 shadowCastingMode는 <b>렌더러 속성</b>이라 카메라를 가리지 않는다.
    /// 그래서 Play 중에는 씬 창에서도 캐릭터가 사라져 발이 지형에 묻히는지 같은 걸 눈으로 볼 수 없다.
    /// URP는 카메라마다 이 콜백을 부르므로, 렌더 직전에 그 카메라가 씬 뷰인지 보고 표시를 갈라준다.
    /// 게임 화면(Game/ToolCamera)에는 기존과 똑같이 ShadowsOnly가 적용된다.
    /// </summary>
    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera renderingCamera)
    {
        if (bodyRenderers == null) return;

        // 애초에 몸을 숨기지 않는 경우(원격 캐릭터 등)는 ApplyBodyVisibility가 정한 값을 그대로 둔다.
        if (!isLocalView || !hidePlayerBodyInFirstPerson) return;

        ShadowCastingMode mode =
            showBodyInSceneView && renderingCamera.cameraType == CameraType.SceneView
                ? ShadowCastingMode.On
                : ShadowCastingMode.ShadowsOnly;

        SetShadowMode(bodyRenderers, mode);
        SetShadowMode(attachedBodyRenderers, mode);
    }

    /// <summary>
    /// CharacterController 캡슐과 발 높이를 씬 뷰에 그린다. (에디터 전용)
    /// 캡슐 하단과 메시 발끝이 어긋나면 캐릭터가 지형에 묻히거나 떠 보이므로, 그 둘을 같이 표시한다.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!drawCapsuleGizmo) return;
        if (!TryGetComponent(out CharacterController cc)) return;

        Vector3 center = transform.position + cc.center;
        float half = Mathf.Max(cc.height * 0.5f - cc.radius, 0f);
        Vector3 sphereTop = center + Vector3.up * half;
        Vector3 sphereBottom = center - Vector3.up * half;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(sphereTop, cc.radius);
        Gizmos.DrawWireSphere(sphereBottom, cc.radius);
        Gizmos.DrawLine(sphereTop + Vector3.right * cc.radius, sphereBottom + Vector3.right * cc.radius);
        Gizmos.DrawLine(sphereTop - Vector3.right * cc.radius, sphereBottom - Vector3.right * cc.radius);
        Gizmos.DrawLine(sphereTop + Vector3.forward * cc.radius, sphereBottom + Vector3.forward * cc.radius);
        Gizmos.DrawLine(sphereTop - Vector3.forward * cc.radius, sphereBottom - Vector3.forward * cc.radius);

        // 캡슐 하단 (지면에 닿는 면)
        float capsuleBottomY = center.y - cc.height * 0.5f;
        Gizmos.color = Color.cyan;
        DrawFlatCross(new Vector3(center.x, capsuleBottomY, center.z), cc.radius * 1.4f);

        // 실제 메시 최저점 — 캡슐 하단보다 아래면 그만큼 지형에 묻힌다
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        float meshMinY = float.MaxValue;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || !renderers[i].gameObject.activeInHierarchy) continue;
            meshMinY = Mathf.Min(meshMinY, renderers[i].bounds.min.y);
        }

        if (meshMinY >= float.MaxValue) return;

        Gizmos.color = meshMinY < capsuleBottomY - 0.001f ? Color.red : Color.yellow;
        DrawFlatCross(new Vector3(center.x, meshMinY, center.z), cc.radius * 1.1f);
    }

    private static void DrawFlatCross(Vector3 point, float size)
    {
        Gizmos.DrawLine(point + Vector3.right * size, point - Vector3.right * size);
        Gizmos.DrawLine(point + Vector3.forward * size, point - Vector3.forward * size);
    }
#endif

    private void OnLateUpdateLoop(float deltaTime)
    {
        if (Main.Save != null && (Main.Save.IsRestoring || Main.Save.IsCapturing)) return;
        if (!isLocalView) return;
        if (inputData == null || playerBody == null || eyePivot == null) return;

        if (toolCamera == null)
            ConfigureToolCamera();

        // Ctrl 키(RotateView)를 누르고 있는 동안에만 마우스 이동값으로 시야를 회전시킨다.
        Vector2 look = inputData.RotateViewHeld ? inputData.LookInput : Vector2.zero;

        // Yaw: 플레이어 몸체를 좌우 회전 (1인칭에서 몸이 카메라 방향과 일치)
        yaw += look.x * mouseSensitivity;
        playerBody.rotation = Quaternion.Euler(0f, yaw, 0f);

        // Pitch: 눈 피벗만 상하 회전 (몸은 기울지 않음)
        pitch -= look.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        eyePivot.localRotation = Quaternion.Euler(pitch + pitchOffset, 0f, 0f);
    }

    private void CacheBodyRenderers()
    {
        if (playerBody == null) return;
        bodyRenderers = playerBody.GetComponentsInChildren<Renderer>(true);
    }

    /// <summary>
    /// 몸에 붙인 오브젝트(액션 중 손에 든 도구 등)를 몸체와 같은 규칙으로 보이게/숨기게 한다.
    /// null을 넘기면 등록을 해제한다. 한 번에 하나만 추적한다.
    /// </summary>
    public void SetAttachedBodyObject(GameObject attached)
    {
        // 풀로 돌아간 오브젝트가 다른 곳에서 재사용될 때 투명하게 나오지 않도록 원래대로 돌려둔다.
        SetShadowMode(attachedBodyRenderers, ShadowCastingMode.On);

        attachedBodyRenderers = attached != null ? attached.GetComponentsInChildren<Renderer>(true) : null;
        ApplyBodyVisibility();
    }

    private void ApplyBodyVisibility()
    {
        ShadowCastingMode mode = isLocalView && hidePlayerBodyInFirstPerson
            ? ShadowCastingMode.ShadowsOnly
            : ShadowCastingMode.On;

        SetShadowMode(bodyRenderers, mode);
        SetShadowMode(attachedBodyRenderers, mode);
    }

    private static void SetShadowMode(Renderer[] renderers, ShadowCastingMode mode)
    {
        if (renderers == null) return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].shadowCastingMode = mode;
        }
    }

    private void OnEquippedItemChanged(EquipSlot slot, ItemDataSO itemData)
    {
        if (slot != EquipSlot.Hand) return;
        PlaceEquippedToolUnderPivot(itemData).Forget();
    }

    private int equippedViewVersion;

    public void RestoreYaw(float savedYaw)
    {
        yaw = savedYaw;
        if (playerBody != null) playerBody.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    // 아이템 프리팹은 Addressables에 ItemDataSO 파일명(itemData.name)을 키로 등록해야 합니다.
    private async UniTaskVoid PlaceEquippedToolUnderPivot(ItemDataSO itemData)
    {
        int version = ++equippedViewVersion;
        ClearEquippedToolView();

        if (toolPivot == null || itemData == null)
            return;

        var token = this.GetCancellationTokenOnDestroy();
        var spawned = await Extensions.SpawnAsync(itemData.name, toolPivot)
            .AttachExternalCancellation(token);

        if (spawned == null) return;
        if (version != equippedViewVersion)
        {
            Extensions.Despawn(spawned);
            return;
        }

        equippedToolObject = spawned;
        ApplyEquippedToolViewTransform(itemData);
        InitializeEquippedTool(itemData);
        ApplyViewModelLayer(equippedToolObject);
        ConfigureEquippedTorchLight(itemData);
        RemoveToolViewPhysics(equippedToolObject);
        equippedToolEquipable?.Equip();
    }

    private void ApplyEquippedToolViewTransform(ItemDataSO itemData)
    {
        if (equippedToolObject == null) return;

        Transform toolTransform = equippedToolObject.transform;
        if (itemData != null && itemData.survivalToolType == SurvivalToolType.Torch)
        {
            toolTransform.localPosition = torchLocalPosition;
            toolTransform.localRotation = Quaternion.Euler(torchLocalEuler);
            toolTransform.localScale = torchLocalScale;
            return;
        }

        if (itemData != null && 
            (itemData.survivalToolType == SurvivalToolType.Pickaxe_Gold 
            || itemData.survivalToolType == SurvivalToolType.Pickaxe_Stone 
            || itemData.survivalToolType == SurvivalToolType.Pickaxe_Iron) )
        {
            toolTransform.localPosition = pickaxeLocalPosition;
            toolTransform.localRotation = Quaternion.Euler(pickaxeLocalEuler);
            toolTransform.localScale = pickaxeLocalScale;
            return;
        }

        // 망치
        if (itemData != null &&
            (itemData.survivalToolType == SurvivalToolType.Hammer_Stone
            || itemData.survivalToolType == SurvivalToolType.Hammer_Iron
            || itemData.survivalToolType == SurvivalToolType.Hammer_Gold))
        {
            toolTransform.localPosition = equippedToolLocalPosition;
            toolTransform.localRotation = Quaternion.Euler(equippedToolLocalEuler);
            toolTransform.localScale = equippedToolLocalScale;
            ApplyChildMeshTransform(hammerMeshLocalPosition, hammerMeshLocalEuler, hammerMeshLocalScale);
            return;
        }

        // 삽
        if (itemData != null &&
            (itemData.survivalToolType == SurvivalToolType.Shovel_Stone
            || itemData.survivalToolType == SurvivalToolType.Shovel_Iron
            || itemData.survivalToolType == SurvivalToolType.Shovel_Gold))
        {
            toolTransform.localPosition = shovelLocalPosition;
            toolTransform.localRotation = Quaternion.Euler(shovelLocalEuler);
            toolTransform.localScale = shovelLocalScale;
            return;
        }

        // 무기 (창, 칼, 활)
        if (itemData != null &&
            (itemData.combatGearType == CombatGearType.Spear
            || itemData.combatGearType == CombatGearType.Sword
            || itemData.combatGearType == CombatGearType.Bow))
        {
            toolTransform.localPosition = weaponLocalPosition;
            toolTransform.localRotation = Quaternion.Euler(weaponLocalEuler);
            toolTransform.localScale = weaponLocalScale;
            ApplyChildMeshTransform(weaponMeshLocalPosition, weaponMeshLocalEuler, weaponMeshLocalScale);
            return;
        }

        // 방패 (뒷면이 플레이어를 향하도록 Y축 180도 추가)
        if (itemData != null && itemData.combatGearType == CombatGearType.Shield)
        {
            toolTransform.localPosition = shieldLocalPosition;
            toolTransform.localRotation = Quaternion.Euler(shieldLocalEuler);
            toolTransform.localScale = shieldLocalScale;
            ApplyChildMeshTransform(shieldMeshLocalPosition, shieldMeshLocalEuler + new Vector3(0f, 180f, 0f), shieldMeshLocalScale);
            return;
        }

        // 기본값 (도끼 포함)
        toolTransform.localPosition = equippedToolLocalPosition;
        toolTransform.localRotation = Quaternion.Euler(equippedToolLocalEuler);
        toolTransform.localScale = equippedToolLocalScale;
        if (itemData != null &&
            (itemData.survivalToolType == SurvivalToolType.Axe_Gold
            || itemData.survivalToolType == SurvivalToolType.Axe_Stone
            || itemData.survivalToolType == SurvivalToolType.Axe_Iron))
            ApplyAxeMeshViewTransform();
    }

    private void ApplyAxeMeshViewTransform()
    {
        ApplyChildMeshTransform(axeMeshLocalPosition, axeMeshLocalEuler, axeMeshLocalScale);
    }

    private void ApplyChildMeshTransform(Vector3 pos, Vector3 euler, Vector3 scale)
    {
        if (equippedToolObject == null) return;

        Renderer renderer = equippedToolObject.GetComponentInChildren<Renderer>(true);
        Transform meshTransform = renderer != null ? renderer.transform : null;
        if (meshTransform == null) return;

        meshTransform.localPosition = pos;
        meshTransform.localRotation = Quaternion.Euler(euler);
        meshTransform.localScale = scale;
    }

    private void ClearEquippedToolView()
    {
        if (equippedToolEquipable != null)
        {
            equippedToolEquipable.Unequip();
            equippedToolEquipable = null;
        }

        if (equippedToolObject == null) return;

        Extensions.Despawn(equippedToolObject);
        equippedToolObject = null;
    }

    private void InitializeEquippedTool(ItemDataSO itemData)
    {
        Item item = equippedToolObject.GetComponentInChildren<Item>(true);
        if (item != null)
        {
            ItemData runtime = playerInventory?.GetEquippedItemInstance(EquipSlot.Hand) as ItemData;
            if (runtime != null) item.BindRuntimeData(runtime);
            else item.Init(itemData);
            equippedToolEquipable = item.itemData as IEquipable;
            if (equippedToolEquipable != null) return;
        }

        MonoBehaviour[] behaviours = equippedToolObject.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is not IEquipable equipable) continue;
            equippedToolEquipable = equipable;
            return;
        }
    }

    private void ConfigureToolCamera()
    {
        if (!isLocalView) return;

        viewModelLayer = LayerMask.NameToLayer(viewModelLayerName);
        if (viewModelLayer < 0)
        {
            Debug.LogWarning($"[PlayerFirstPersonCameraController] '{viewModelLayerName}' 레이어가 없습니다. ProjectSettings/Tags and Layers에 레이어를 추가해야 도구 전용 렌더링이 동작합니다.");
            return;
        }

        equippedLightLayer = LayerMask.NameToLayer(equippedLightLayerName);
        if (equippedLightLayer < 0)
        {
            Debug.LogWarning($"[PlayerFirstPersonCameraController] '{equippedLightLayerName}' 레이어가 없습니다. 장착 도구 조명은 Default 레이어를 사용합니다.");
            equippedLightLayer = 0;
        }

        ApplyViewModelLayer(equippedToolObject);

        Camera baseCamera = FindCinemachineOutputCamera();
        if (baseCamera == null)
            return;

        EnsureToolCamera(baseCamera);
        ConfigureCameraMasks(baseCamera);
        ConfigureCameraStack(baseCamera);
    }

    private Camera FindCinemachineOutputCamera()
    {
        Camera fallbackCamera = null;

        for (int i = 0; i < CinemachineBrain.ActiveBrainCount; i++)
        {
            CinemachineBrain brain = CinemachineBrain.GetActiveBrain(i);
            if (brain == null || brain.OutputCamera == null)
                continue;

            fallbackCamera ??= brain.OutputCamera;

            if (fpCamera != null && brain.IsLiveChild(fpCamera))
                return brain.OutputCamera;
        }

        return fallbackCamera;
    }

    private void EnsureToolCamera(Camera baseCamera)
    {
        if (toolCamera == null)
        {
            GameObject cameraObject = new GameObject("ToolCamera");
            toolCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<UniversalAdditionalCameraData>();
        }

        Transform toolCameraTransform = toolCamera.transform;
        if (toolCameraTransform.parent != baseCamera.transform)
            toolCameraTransform.SetParent(baseCamera.transform, false);

        toolCameraTransform.localPosition = Vector3.zero;
        toolCameraTransform.localRotation = Quaternion.identity;
        toolCameraTransform.localScale = Vector3.one;

        toolCamera.fieldOfView = toolCameraFov;
        toolCamera.nearClipPlane = toolCameraNearClip;
        toolCamera.farClipPlane = toolCameraFarClip;
        toolCamera.clearFlags = CameraClearFlags.Depth;
        toolCamera.depth = baseCamera.depth + 1f;
        toolCamera.allowHDR = baseCamera.allowHDR;
        toolCamera.allowMSAA = baseCamera.allowMSAA;
        toolCamera.useOcclusionCulling = false;
        toolCamera.cullingMask = 1 << viewModelLayer;
    }

    private void ConfigureCameraMasks(Camera baseCamera)
    {
        int viewModelMask = 1 << viewModelLayer;
        baseCamera.cullingMask &= ~viewModelMask;
        toolCamera.cullingMask = viewModelMask;
    }

    private void ConfigureCameraStack(Camera baseCamera)
    {
        UniversalAdditionalCameraData baseData = baseCamera.GetUniversalAdditionalCameraData();
        UniversalAdditionalCameraData toolData = toolCamera.GetUniversalAdditionalCameraData();

        baseData.renderType = CameraRenderType.Base;
        toolData.renderType = CameraRenderType.Overlay;
        toolData.renderShadows = false;

        if (stackedBaseCamera != null && stackedBaseCamera != baseCamera)
        {
            UniversalAdditionalCameraData oldBaseData = stackedBaseCamera.GetComponent<UniversalAdditionalCameraData>();
            oldBaseData?.cameraStack?.Remove(toolCamera);
        }

        stackedBaseCamera = baseCamera;
        if (!baseData.cameraStack.Contains(toolCamera))
            baseData.cameraStack.Add(toolCamera);
    }

    private void RemoveToolCameraFromStack()
    {
        if (stackedBaseCamera != null)
        {
            UniversalAdditionalCameraData baseData = stackedBaseCamera.GetComponent<UniversalAdditionalCameraData>();
            baseData?.cameraStack?.Remove(toolCamera);
            stackedBaseCamera = null;
        }

        if (toolCamera != null)
        {
            Destroy(toolCamera.gameObject);
            toolCamera = null;
        }
    }

    private void ApplyViewModelLayer(GameObject root)
    {
        if (root == null || viewModelLayer < 0) return;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            GameObject childObject = children[i].gameObject;
            childObject.layer = childObject.GetComponent<Light>() != null
                ? equippedLightLayer
                : viewModelLayer;
        }
    }

    private void ConfigureEquippedTorchLight(ItemDataSO itemData)
    {
        if (equippedToolObject == null || itemData == null) return;
        if (itemData.survivalToolType != SurvivalToolType.Torch) return;

        Light[] lights = equippedToolObject.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            Light torchLight = lights[i];
            if (torchLight == null) continue;

            torchLight.enabled = true;
            torchLight.color = equippedTorchLightColor;
            torchLight.intensity = Mathf.Max(torchLight.intensity, equippedTorchLightIntensity);
            torchLight.range = Mathf.Max(torchLight.range, equippedTorchLightRange);
            torchLight.cullingMask = Physics.AllLayers;
            torchLight.gameObject.layer = equippedLightLayer;
        }
    }

    private static void RemoveToolViewPhysics(GameObject toolObject)
    {
        if (toolObject == null) return;

        Rigidbody[] rigidbodies = toolObject.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            rigidbodies[i].isKinematic = true;
            rigidbodies[i].detectCollisions = false;
            Destroy(rigidbodies[i]);
        }

        Collider[] colliders = toolObject.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;
    }

}
