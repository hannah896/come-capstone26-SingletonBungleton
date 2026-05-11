using Cysharp.Threading.Tasks;
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

    private float yaw;
    private float pitch;
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


    private void OnValidate()
    {
        toolPivot = transform.Find("EyePivot/ToolPivot");
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

        CacheBodyRenderers();
        ApplyBodyVisibility();
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
        PlaceEquippedToolUnderPivot(playerInventory.EquippedHand);
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

        Vector2 look = inputData.LookInput;

        // Yaw: 플레이어 몸체를 좌우 회전 (1인칭에서 몸이 카메라 방향과 일치)
        yaw += look.x * mouseSensitivity;
        playerBody.rotation = Quaternion.Euler(0f, yaw, 0f);

        // Pitch: 눈 피벗만 상하 회전 (몸은 기울지 않음)
        pitch -= look.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        eyePivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
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
        PlaceEquippedToolUnderPivot(itemData);
    }

    private void PlaceEquippedToolUnderPivot(ItemDataSO itemData)
    {
        ClearEquippedToolView();

        if (toolPivot == null || itemData == null || itemData.prefab == null)
            return;

        equippedToolObject = Instantiate(itemData.prefab, toolPivot, false);
        ApplyEquippedToolViewTransform(itemData);
        InitializeEquippedTool(itemData);
        ApplyViewModelLayer(equippedToolObject);
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

        if (itemData != null && itemData.survivalToolType == SurvivalToolType.Pickaxe)
        {
            toolTransform.localPosition = pickaxeLocalPosition;
            toolTransform.localRotation = Quaternion.Euler(pickaxeLocalEuler);
            toolTransform.localScale = pickaxeLocalScale;
            return;
        }

        toolTransform.localPosition = equippedToolLocalPosition;
        toolTransform.localRotation = Quaternion.Euler(equippedToolLocalEuler);
        toolTransform.localScale = equippedToolLocalScale;

        if (itemData != null && itemData.survivalToolType == SurvivalToolType.Axe)
            ApplyAxeMeshViewTransform();
    }

    private void ApplyAxeMeshViewTransform()
    {
        Transform meshTransform = equippedToolObject.transform.Find("axe");
        if (meshTransform == null)
        {
            Renderer renderer = equippedToolObject.GetComponentInChildren<Renderer>(true);
            meshTransform = renderer != null ? renderer.transform : null;
        }

        if (meshTransform == null) return;

        meshTransform.localPosition = axeMeshLocalPosition;
        meshTransform.localRotation = Quaternion.Euler(axeMeshLocalEuler);
        meshTransform.localScale = axeMeshLocalScale;
    }

    private void ClearEquippedToolView()
    {
        equippedToolEquipable?.Unequip();
        equippedToolEquipable = null;

        if (equippedToolObject == null) return;

        Destroy(equippedToolObject);
        equippedToolObject = null;
    }

    private void InitializeEquippedTool(ItemDataSO itemData)
    {
        Item item = equippedToolObject.GetComponentInChildren<Item>(true);
        if (item != null)
            item.Init(itemData);

        equippedToolEquipable = item as IEquipable;
        if (equippedToolEquipable != null) return;

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
            children[i].gameObject.layer = viewModelLayer;
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
