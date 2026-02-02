using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;

/// <summary>
/// 플레이어 입력을 관리하는 매니저.
/// InputActions의 등록, 활성화/비활성화, 캐싱을 담당합니다.
/// </summary>
public class InputManager : CoreManager
{
    #region Fields

    // Unity Input System 액션 에셋
    private InputSystem_Actions _input;

    // 현재 활성화된 액션 타입 집합
    private HashSet<Type> _curActionTypes = new();

    // 타입별 액션 인스턴스 캐시
    private readonly Dictionary<Type, InputActions> _typeToAction = new();

    #endregion

    #region Properties

    public InputSystem_Actions Actions => _input;
    public int ActiveActionCount => _curActionTypes.Count;
    public int CachedActionCount => _typeToAction.Count;

    #endregion

    #region Initialization

    protected override async UniTask OnInitializeAsync()
    {
        await base.OnInitializeAsync();

        EnhancedTouchSupport.Enable();
        _input = new InputSystem_Actions();
        _input.Enable();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[InputManager] Initialized");
#endif
    }

    #endregion

    #region Input Mask - Type API

    // 지정한 InputAction 타입들만 활성화하고 나머지는 비활성화
    private void SetInput(params Type[] targetTypes)
    {
        HashSet<Type> nextActionTypes = targetTypes
            .Where(IsValidActionType)
            .ToHashSet();

        // 기존에 활성화되었지만 새 마스크에는 없는 액션들 비활성화
        foreach (Type curType in _curActionTypes)
        {
            if (!nextActionTypes.Contains(curType))
            {
                DisconnectAction(curType);
            }
        }

        // 새 마스크에 있지만 기존에 비활성화된 액션들 활성화
        foreach (Type nextType in nextActionTypes)
        {
            if (!_curActionTypes.Contains(nextType))
            {
                ConnectAction(nextType);
            }
        }

        _curActionTypes = nextActionTypes;
    }

    // 인풋 액션 추가
    private void AddInput(params Type[] addTypes)
    {
        SetInput(_curActionTypes.Concat(addTypes).ToArray());
    }

    // 인풋 액션 제거
    private void RemoveInput(params Type[] removeTypes)
    {
        SetInput(_curActionTypes.Where(t => !removeTypes.Contains(t)).ToArray());
    }

    #endregion

    #region Input Mask - Generic API

    /// <summary>
    /// 단일 InputAction 타입을 활성화합니다.
    /// </summary>
    public void SetInput<T>() where T : InputActions
    {
        SetInput(typeof(T));
    }

    /// <summary>
    /// 두 개의 InputAction 타입을 활성화합니다.
    /// </summary>
    public void SetInput<T1, T2>()
        where T1 : InputActions
        where T2 : InputActions
    {
        SetInput(typeof(T1), typeof(T2));
    }

    /// <summary>
    /// 세 개의 InputAction 타입을 활성화합니다.
    /// </summary>
    public void SetInput<T1, T2, T3>()
        where T1 : InputActions
        where T2 : InputActions
        where T3 : InputActions
    {
        SetInput(typeof(T1), typeof(T2), typeof(T3));
    }

    /// <summary>
    /// 네 개의 InputAction 타입을 활성화합니다.
    /// </summary>
    public void SetInput<T1, T2, T3, T4>()
        where T1 : InputActions
        where T2 : InputActions
        where T3 : InputActions
        where T4 : InputActions
    {
        SetInput(typeof(T1), typeof(T2), typeof(T3), typeof(T4));
    }

    /// <summary>
    /// 단일 인풋 액션을 추가합니다.
    /// </summary>
    public void AddInput<T>() where T : InputActions
    {
        AddInput(typeof(T));
    }

    /// <summary>
    /// 단일 인풋 액션을 제거합니다.
    /// </summary>
    public void RemoveInput<T>() where T : InputActions
    {
        RemoveInput(typeof(T));
    }

    /// <summary>
    /// 현재 적용된 모든 인풋 액션을 제거합니다.
    /// </summary>
    public void RemoveAllInputs()
    {
        SetInput();
    }

    #endregion

    #region State Query

    /// <summary>
    /// 인풋 액션이 활성화 상태인지 확인합니다. 모두 활성화되어야 true 반환.
    /// </summary>
    public bool IsActive(params Type[] searchTypes)
    {
        return searchTypes.All(t => _curActionTypes.Contains(t));
    }

    /// <summary>
    /// 단일 인풋 액션이 활성화 상태인지 확인합니다.
    /// </summary>
    public bool IsActive<T>() where T : InputActions
    {
        return _curActionTypes.Contains(typeof(T));
    }

    /// <summary>
    /// 캐싱된 액션 인스턴스를 가져옵니다. 없으면 null 반환.
    /// </summary>
    public T GetAction<T>() where T : InputActions
    {
        return _typeToAction.TryGetValue(typeof(T), out var action) ? action as T : null;
    }

    /// <summary>
    /// 캐싱된 액션 인스턴스를 가져오거나, 없으면 생성합니다.
    /// </summary>
    public T GetOrCreateAction<T>() where T : InputActions
    {
        var type = typeof(T);

        if (_typeToAction.TryGetValue(type, out var action))
        {
            return action as T;
        }

        return CreateAndCacheAction(type) as T;
    }

    /// <summary>
    /// 특정 타입의 액션이 캐싱되어 있는지 확인합니다.
    /// </summary>
    public bool IsCached<T>() where T : InputActions
    {
        return _typeToAction.ContainsKey(typeof(T));
    }

    #endregion

    #region Internal Methods

    // 유효한 InputActions 타입인지 검증
    private bool IsValidActionType(Type type)
    {
        if (type == null) return false;
        if (type.IsAbstract) return false;
        if (!typeof(InputActions).IsAssignableFrom(type)) return false;
        return true;
    }

    // 액션 연결 (필요시 생성)
    private void ConnectAction(Type type)
    {
        var action = GetOrCreateActionInternal(type);
        if (action == null) return;

        action.Connect();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[InputManager] Connected: {type.Name}");
#endif
    }

    // 액션 연결 해제
    private void DisconnectAction(Type type)
    {
        if (!_typeToAction.TryGetValue(type, out var action)) return;

        action.Disconnect();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[InputManager] Disconnected: {type.Name}");
#endif
    }

    // 액션 인스턴스 가져오기 또는 생성
    private InputActions GetOrCreateActionInternal(Type type)
    {
        if (_typeToAction.TryGetValue(type, out var action))
        {
            return action;
        }

        return CreateAndCacheAction(type);
    }

    // 액션 인스턴스 생성 및 캐싱
    private InputActions CreateAndCacheAction(Type type)
    {
        try
        {
            var action = (InputActions)Activator.CreateInstance(type, this);
            _typeToAction[type] = action;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[InputManager] Created and cached: {type.Name}");
#endif
            return action;
        }
        catch (Exception e)
        {
            Debug.LogError($"[InputManager] Failed to create InputActions instance: {type.Name}\n{e}");
            return null;
        }
    }

    #endregion

    #region Utility

    // UI 레이캐스트용 이벤트 데이터
    private PointerEventData _pointerData;

    // UI 레이캐스트 결과 버퍼
    private readonly List<RaycastResult> _raycastResults = new(16);

    /// <summary>
    /// 현재 포인터가 UI 위에 존재하는지 확인합니다.
    /// </summary>
    public bool IsPointerOverUI(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;

        _pointerData ??= new PointerEventData(EventSystem.current);
        _pointerData.position = screenPos;
        _raycastResults.Clear();
        EventSystem.current.RaycastAll(_pointerData, _raycastResults);

        return _raycastResults.Count > 0;
    }

    /// <summary>
    /// 스크린 좌표를 월드 좌표로 변환합니다.
    /// </summary>
    public Vector3 ScreenToWorld(Vector2 screenPos)
    {
        var cam = Camera.main;
        if (cam == null) return Vector3.zero;

        float depth = cam.orthographic ? -cam.transform.position.z : 0f;
        return cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, depth));
    }

    #endregion

    #region Cleanup

    public override void Clear()
    {
        base.Clear();

        // 모든 활성화된 액션 비활성화
        foreach (var type in _curActionTypes)
        {
            if (_typeToAction.TryGetValue(type, out var action))
            {
                action.Disconnect();
            }
        }

        _curActionTypes.Clear();
        _typeToAction.Clear();

        _input?.Disable();
        _input = null;
    }

    #endregion
}
