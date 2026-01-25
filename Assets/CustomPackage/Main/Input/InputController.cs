using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class InputController
{
    #region Properties

    public static bool AllowInput { get; set; } = true;
    
    public event Action<float> OnActionZoomPerformed;       // 터치 패드 두 터치 사이의 변화 값 Performed
    public event Action<float> OnActionScrollPerformed;     // 스크롤 변화 값 Performed
    
    public event Action<Vector2> OnActionPositionLeftStarted;       // 왼쪽 클릭 Started
    public event Action<Vector2> OnActionPositionLeftCanceled;       // 왼쪽 클릭 Canceled
    public event Action<Vector2> OnActionRightClickStarted;      // 오른쪽 클릭 Started
    public event Action<Vector2> OnActionPositionPerformed;      // 마우스 커서 Performed
    public event Action<Vector2> OnActionPositionLeftDrag;          // 왼쪽 클릭 드래그 Performed
    public event Action<Vector2>  OnActionPositionClicked;       // 왼쪽 클릭 추적 Canceled
    public event Action<Vector2> OnActionPositionRightDrag;   // 오른쪽 클릭 Drag
    
    public event Action<Vector2Int> OnActionIntPositionPerformed;   // 마우스 커서 Performed
    public event Action<Vector2Int> OnActionIntPositionLeftDrag;   // 왼쪽 클릭 Drag
    public event Action<Vector2Int> OnActionIntPositionLeftStarted;     // 왼쪽 클릭 Started
    public event Action<Vector2Int> OnActionIntPositionLeftCanceled;    // 왼쪽 클릭 Canceled
    public event Action<Vector2Int> OnActionIntPositionRightStarted;    // 오른쪽 클릭 Started
    public event Action<Vector2Int> OnActionIntPositionRightDrag;   // 오른쪽 클릭 Drag

    public Camera Camera => Main.Screen.Camera;

    #endregion

    #region Fields
    
    private GameController _gameController;
    private InputAction _actionLeftClick;
    private InputAction _actionRightClick;
    private InputAction _actionPosition;
    private InputAction _actionScroll;

    #endregion

    private bool _isPinching;
    private bool _isFirstTouch = false;
    private bool _isLeftClick = false;
    private bool _isRightClick = false;
    private float _lastPinchDistance;
    private Vector2 _startedPosition = Vector2.zero;
    private Vector2 _performedPosition = Vector2.zero;
    private Vector2Int _cursorPosition = Vector2Int.zero;
    private float _dragDistance = 0;

    #region Initialize

    public void Initialize()
    {
        EnhancedTouchSupport.Enable();
        
        _gameController = new GameController();
        _actionLeftClick = _gameController.GamePlay.Select;
        _actionRightClick = _gameController.GamePlay.RightClick;
        _actionPosition = _gameController.GamePlay.Position;
        _actionScroll = _gameController.GamePlay.Scrolled;
        
        _actionScroll.performed += OnScroll;
        _actionLeftClick.started += OnLeftClicked;
        _actionLeftClick.canceled += OnLeftCanceled;
        _actionRightClick.started += OnRightClicked;
        _actionRightClick.canceled += OnRightCanceled;
        _actionPosition.performed += OnPositionChanged;
        
        GameEvents.OnGamePause += OnGamePause;
        GameEvents.OnGameResume += OnGameResume;
    }

    public void OnDestroy()
    {
        _actionScroll.performed -= OnScroll;
        _actionLeftClick.started -= OnLeftClicked;
        _actionLeftClick.canceled -= OnLeftCanceled;
        _actionRightClick.started -= OnRightClicked;
        _actionRightClick.canceled -= OnRightCanceled;
        _actionPosition.performed -= OnPositionChanged;
        
        GameEvents.OnGamePause -= OnGamePause;
        GameEvents.OnGameResume -= OnGameResume;
    }

    #endregion

    #region Events

    // 왼쪽 클릭 및 터치 Started 추적
    private async void OnLeftClicked(InputAction.CallbackContext context)
    {
        if (!AllowInput || IsPointerOverUI()) return;
        if (!_isFirstTouch)
        {
            await UniTask.NextFrame();
            _isFirstTouch = true;
        }

        Vector3 worldPosition = GetWorldPosition();    
        OnActionPositionLeftStarted?.Invoke(worldPosition);

        Vector2Int cursorPosition = GetCursorPosition(worldPosition);
        OnActionIntPositionLeftStarted?.Invoke(cursorPosition);
        
        _isLeftClick = true;
        _dragDistance = 0;
        _performedPosition = worldPosition;
        _startedPosition = worldPosition;
    }

    // 왼쪽 클릭 및 터치 Canceled 추적
    private void OnLeftCanceled(InputAction.CallbackContext context)
    {
        if (!AllowInput) return;
        
        Vector3 worldPosition = GetWorldPosition();
        OnActionPositionLeftCanceled?.Invoke(worldPosition);
        
        Vector2Int cursorPosition = GetCursorPosition(worldPosition);
        OnActionIntPositionLeftCanceled?.Invoke(cursorPosition);

        if (Vector2.Distance(_startedPosition, worldPosition) < 0.1f && _dragDistance < 0.1f)
        {
            OnActionPositionClicked?.Invoke(_startedPosition);
        }

        _dragDistance = 0;
        _performedPosition = worldPosition;
        _isPinching = false;
        _isLeftClick = false;
    }

    // 마우스 오른쪽 클릭 Started 추적
    private void OnRightClicked(InputAction.CallbackContext context)
    {
        if (!AllowInput || IsPointerOverUI()) return;
        
        Vector3 worldPosition = GetWorldPosition();
        OnActionRightClickStarted?.Invoke(worldPosition);
        
        Vector2Int cursorPosition = GetCursorPosition(worldPosition);
        OnActionIntPositionRightStarted?.Invoke(cursorPosition);

        _isRightClick = true;
    }

    private void OnRightCanceled(InputAction.CallbackContext context)
    {
        if (!AllowInput) return;

        _isRightClick = false;
    }

    // 마우스 및 패드 포인터 변화 값 추적
    private void OnPositionChanged(InputAction.CallbackContext context)
    {
        if (!AllowInput) return;
        // 기본 worldPosition 처리
        Vector3 worldPosition = GetWorldPosition();
        OnActionPositionPerformed?.Invoke(worldPosition);
        
        // 그리드 기준의 position 처리
        Vector2Int cursorPosition = GetCursorPosition(worldPosition);
        if (_cursorPosition != cursorPosition)
        {
            _cursorPosition = cursorPosition;
            OnActionIntPositionPerformed?.Invoke(cursorPosition);
        } 
        
        _dragDistance += Vector2.Distance(_performedPosition, worldPosition);
        _performedPosition = worldPosition;

        if (_isLeftClick)
        {        
            // 줌 처리
            if (Touch.activeTouches.Count == 2)
            {
                OnActionZoomPerformed?.Invoke(GetZoomDelta());
                return;
            }
            // 드래그 처리
            if (Touch.activeTouches.Count < 2)
            {
                OnActionPositionLeftDrag?.Invoke(worldPosition);
                OnActionIntPositionLeftDrag?.Invoke(cursorPosition);
            }
        }

        if (_isRightClick)
        {
            // 드래그 처리
            OnActionPositionRightDrag?.Invoke(worldPosition);
            OnActionIntPositionRightDrag?.Invoke(cursorPosition);
        }
    }
    
    // 마우스 스크롤 변화 값 추적
    private void OnScroll(InputAction.CallbackContext context)
    {
        if (!AllowInput || IsPointerOverUI()) return;
        OnActionScrollPerformed?.Invoke(GetScrollPosition());
    }

    private void OnGamePause() => AllowInput = false;
    private void OnGameResume() => AllowInput = true;

    #endregion

    // 현재 마우스 포인터의 WorldPoint를 반환
    public Vector3 GetWorldPosition()
    {
        Vector3 screenPosition = _actionPosition.ReadValue<Vector2>();
        return Camera.ScreenToWorldPoint(screenPosition.SetZ(Camera.transform.position.z));
    }

    // 마우스 스크롤 Position을 반환
    private float GetScrollPosition()
    {
        Vector2 scroll = _actionScroll.ReadValue<Vector2>();
        return scroll.y;
    }

    private float GetZoomDelta()
    {
        Vector2 p1 = Touch.activeTouches[0].screenPosition;
        Vector2 p2 = Touch.activeTouches[1].screenPosition;

        float currentDistance = Vector2.Distance(p1, p2);

        if (!_isPinching)
        {
            _lastPinchDistance = currentDistance;
            _isPinching = true;
            return 0;
        }
        
        float delta = currentDistance - _lastPinchDistance;
        _lastPinchDistance = currentDistance;
        return delta;
    }

    private Vector2Int GetCursorPosition(Vector3 worldPosition)
    {
        int x = Mathf.FloorToInt(worldPosition.x + 0.5f);
        int y = Mathf.FloorToInt(worldPosition.y + 0.5f);
        return new(x, y);        
    }
    
    // 해당 클릭 스크린 부분에 UI가 있는지 감지하고 결과를 반환
    private bool IsPointerOverUI()
    {
        Vector2 screenPosition = _actionPosition.ReadValue<Vector2>();
        
        // 1. 현재 EventSystem이 없으면 UI가 없는 것이므로 false
        if (EventSystem.current == null) return false;

        // 2. 포인터 이벤트 데이터 생성 (현재 마우스/터치 위치 설정)
        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = screenPosition;

        // 3. UI 요소들에 대해 Raycast 수행
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        // 4. 결과 리스트에 하나라도 있다면 UI가 해당 위치에 존재하는 것
        return results.Count > 0;
    }
}