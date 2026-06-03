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
    private PlayerInputData inputData;
    private Transform playerBody;
    private PlayerInventory playerInventory;
    private GameObject equippedToolObject;
    private IEquipable equippedToolEquipable;
    private Renderer[] bodyRenderers;
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
    /// 도끼 휘두르기 — 1인칭 도구 스윙 + 카메라 시야 흔들림
    /// </summary>
    public void PlayChopSwing()
    {
        if (toolPivot == null) return;

        toolPivot.DOKill();
        Quaternion restRotation = toolPivot.localRotation;

        DOTween.Sequence()
            // 1. 들어올리기 + 오른쪽으로 당기기 (0.4s)
            .Append(toolPivot.DOLocalRotate(new Vector3(-45f, 20f, 8f), 0.4f, RotateMode.LocalAxisAdd)
                .SetEase(Ease.OutQuad))
            // 2. 내려찍기 + 왼쪽 아크 (0.6s)
            .Append(toolPivot.DOLocalRotate(new Vector3(100f, -35f, -12f), 0.6f, RotateMode.LocalAxisAdd)
                .SetEase(Ease.InQuart))
            // 3. 원위치 복귀 (1.0s)
            .Append(toolPivot.DOLocalRotateQuaternion(restRotation, 1.0f)
                .SetEase(Ease.OutQuad));

        // 카메라 시야: 내려찍는 타이밍에 흔들림
        DOTween.To(() => pitchOffset, x => pitchOffset = x, 6f, 0.6f)
            .SetDelay(0.4f)
            .SetEase(Ease.InQuad)
            .OnComplete(() =>
                DOTween.To(() => pitchOffset, x => pitchOffset = x, 0f, 0.9f)
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
    }

    private void OnDisable()
    {
        Main.Loop.OnLateUpdate -= OnLateUpdateLoop;

        if (playerInventory != null)
            playerInventory.OnEquippedItemChanged -= OnEquippedItemChanged;

        ClearEquippedToolView();
        RemoveToolCameraFromStack();
    }

    private void OnLateUpdateLoop(float deltaTime)
    {
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

    private void ApplyBodyVisibility()
    {
        if (bodyRenderers == null) return;

        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            if (bodyRenderers[i] == null) continue;
            bodyRenderers[i].shadowCastingMode =
                isLocalView && hidePlayerBodyInFirstPerson
                    ? ShadowCastingMode.ShadowsOnly
                    : ShadowCastingMode.On;
        }
    }

    private void OnEquippedItemChanged(EquipSlot slot, ItemDataSO itemData)
    {
        if (slot != EquipSlot.Hand) return;
        PlaceEquippedToolUnderPivot(itemData).Forget();
    }

    // 아이템 프리팹은 Addressables에 ItemDataSO 파일명(itemData.name)을 키로 등록해야 합니다.
    private async UniTaskVoid PlaceEquippedToolUnderPivot(ItemDataSO itemData)
    {
        ClearEquippedToolView();

        if (toolPivot == null || itemData == null)
            return;

        var token = this.GetCancellationTokenOnDestroy();
        var spawned = await Extensions.SpawnAsync(itemData.name, toolPivot)
            .AttachExternalCancellation(token);

        if (spawned == null) return;

        equippedToolObject = spawned;
        ApplyEquippedToolViewTransform(itemData);
        InitializeEquippedTool(itemData);
        ApplyViewModelLayer(equippedToolObject);
        ConfigureEquippedTorchLight(itemData);
        RemoveToolViewPhysics(equippedToolObject);
        playerInventory?.RegisterEquippedItemInstance(EquipSlot.Hand, equippedToolEquipable);
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
            playerInventory?.UnregisterEquippedItemInstance(EquipSlot.Hand, equippedToolEquipable);
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
            item.Init(itemData);
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
