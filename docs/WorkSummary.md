# Work Summary

---

## 2026-05-11

### 1. 인벤토리 입력 구조 변경

**파일:** `Assets/02_Scripts/03_Entity/Player/PlayerInputData.cs`, `PlayerInputHandler.cs`, `PlayerInventory.cs`, `Assets/02_Scripts/04_Item/InventoryDisplayUI.cs`, `InventorySlotUI.cs`

- 아이템 줍기 입력을 `G` 키로 분리
- 선택 슬롯 장착 입력을 `E` 키 전용으로 변경
  - 기존 `E = Interact/줍기` 흐름은 인벤토리 로직에서 제거
  - `InputActions_PlayerInputHandler`에서 `Interact.performed` 연결 제거
- 마우스 휠 입력으로 인벤토리 선택 슬롯 포커스 이동 구현
- 숫자키 입력은 해당 슬롯 직접 선택으로 유지
- 선택된 슬롯은 `InventorySlotUI`에서 `Outline`으로 표시
- 장착 슬롯 UI 갱신을 위해 `Head/Chest/Hand` 장착 슬롯 표시 로직 보강

### 2. 우클릭 도구 상호작용 추가

**파일:** `Assets/02_Scripts/03_Entity/Player/PlayerInputData.cs`, `PlayerInventory.cs`

- 마우스 우클릭을 장착 도구 사용 입력으로 추가
- 손 슬롯(`EquipSlot.Hand`)에 장착된 생존 도구 기준으로 화면 중앙 레이캐스트 수행
- 레이캐스트 대상에 `GatherableObject`가 있으면 `OnHit(SurvivalToolType)` 호출
- UI 위에서 우클릭한 경우 도구 사용이 발생하지 않도록 `Main.Input.IsPointerOverUI()` 검사 추가

### 3. 1인칭 로컬 플레이어 뷰 처리

**파일:** `Assets/02_Scripts/03_Entity/Player/Player.cs`, `PlayerFirstPersonCameraController.cs`, `PlayerInventory.cs`

- Photon Fusion `NetworkObject.HasInputAuthority` 기준으로 로컬 플레이어만 FP 카메라/입력/인벤토리 UI를 초기화하도록 변경
- 로컬 플레이어는 자신의 몸 렌더러를 `ShadowCastingMode.ShadowsOnly`로 전환해 FP 카메라에 몸이 비치지 않도록 처리
- 원격 플레이어는 렌더러를 숨기지 않아 멀티플레이에서 다른 플레이어가 정상적으로 보이도록 처리
- 손 슬롯 장착 아이템 변경 이벤트(`OnEquippedItemChanged`)를 추가해 장착 도구 프리팹 표시 갱신을 연결

**검증:** `uloop compile` 실행 결과 컴파일 에러 0개, 경고 0개.

### 4. 1인칭 도구 피벗 프리셋 방식 변경

**파일:** `Assets/02_Scripts/03_Entity/Player/PlayerFirstPersonCameraController.cs`, `Assets/02_Scripts/03_Entity/Player/Player.cs`, `Assets/02_Scripts/04_Item/DroppedItem.cs`

- 런타임에 `FirstPersonView` 루트나 임시 손 모델을 자동 생성하던 흐름 제거
- 장착 아이템 변경 이벤트를 받아 손 슬롯 도구 프리팹을 `ToolPivot` 바로 아래 자식으로만 생성하도록 변경
- 생성된 도구의 위치, 회전, 스케일, 이름, Collider, Rigidbody, MonoBehaviour, Renderer 설정은 건드리지 않도록 유지
- 이전 장착 도구는 새 장착 도구를 붙이기 전에 제거하여 `ToolPivot` 아래에 현재 장착 도구만 남도록 처리
- 장착 도구 프리팹의 Rigidbody는 장착 중 `isKinematic = true`로 고정하고, 월드에 버려져 `DroppedItem.Setup()`을 타면 `isKinematic = false`로 되돌리도록 처리

**검증:** `uloop compile` 실행 결과 컴파일 에러 0개, 경고 0개.

---

## 2026-05-06

### 1. Player 인벤토리 기능 연결

**파일:** `Assets/02_Scripts/03_Entity/Player/Player.cs`, `PlayerInputData.cs`, `PlayerInventory.cs`

- `PlayerInventory` 컴포넌트를 추가하여 Player가 인벤토리 기능을 직접 보유하도록 구성
- Player 초기화 시 `PlayerInventory`가 없으면 자동으로 `AddComponent<PlayerInventory>()` 처리
- `PlayerInputData`에 `InventoryTogglePressed`, `QuickSlotIndex` 입력 데이터 추가
- Player 업데이트 루프에서 `playerInventory.Tick()`을 호출하여 이벤트 입력이 소비되기 전에 처리
- Tab 입력으로 `UI_Popup_Inventory` 팝업 열기/닫기 구현
- Interact 입력 시 주변 `DroppedItem` 중 가장 가까운 아이템을 줍도록 구현

---

### 2. InventoryManager 런타임 안정성 보강

**파일:** `Assets/02_Scripts/04_Item/InventoryManager.cs`, `DroppedItem.cs`, `GatherableObject.cs`

- `InventoryManager.EnsureInstance()` 추가
  - 씬에 인벤토리 매니저가 없으면 `@InventoryManager` 오브젝트를 자동 생성
  - Player, UI, 드랍 아이템, 채집 오브젝트에서 공통으로 사용
- 슬롯 초기화 로직을 `InitializeSlots()`로 분리하여 중복 슬롯 생성 방지
- `AddItem(ItemDataSO, int, out int remainingAmount)` 오버로드 추가
  - 인벤토리가 일부만 수용했을 때 남은 수량을 정확히 반환
- `DroppedItem.Pickup()`에서 일부만 주웠을 경우 바닥 아이템 수량을 남은 개수로 갱신
- `GatherableObject`가 인벤토리 초과분만 바닥에 드랍하도록 수정

---

### 3. 인벤토리 UI 방어 코드 및 입력 충돌 정리

**파일:** `Assets/02_Scripts/04_Item/InventoryUI.cs`, `InventorySlotUI.cs`, `Assets/02_Scripts/@Scripts/Scenes/GameScene.cs`

- `UI_Popup_Inventory` 초기화 시 `InventoryManager.EnsureInstance()` 호출
- 슬롯 프리팹/컨테이너 미연결 시 경고 후 안전하게 중단
- `InventorySlotUI`의 아이콘, 스택 텍스트, 내구도 바 null 체크 추가
- 기존 `GameScene.Update()`의 임시 Tab 인벤토리 토글 제거
  - Player의 `PlayerInventory`가 인벤토리 토글 책임을 갖도록 정리

**검증:** `uloop-compile` 스킬을 시도했으나 현재 셸 PATH에서 `uloop` 실행 파일을 찾지 못해 Unity 컴파일은 실행하지 못함 (`uloop` command not found). 대체로 `dotnet build .\Assembly-CSharp.csproj --no-restore`를 실행했으며, 신규 인벤토리 코드 오류는 확인되지 않았고 기존 Firebase 참조(`Firebase.Analytics`) 누락 오류 2건으로 전체 빌드는 실패.

---

### 4. 인벤토리 팝업 Addressables 누락 대응

**파일:** `Assets/02_Scripts/03_Entity/Player/PlayerInventory.cs`, `Assets/02_Scripts/04_Item/InventoryUI.cs`, `InventorySlotUI.cs`

- `UI_Popup_Inventory` Addressable 프리팹이 없어 `InvalidKeyException`이 발생하는 문제 확인
- `PlayerInventory.ToggleInventory()`에서 Addressables 로드 실패 시 런타임 기본 인벤토리 UI를 생성하도록 fallback 추가
- `UI_Popup_Inventory.CreateRuntimeFallback()` 추가
  - `Canvas_Popup` 아래에 기본 배경, 패널, 5열 슬롯 그리드, 슬롯 프리팹을 코드로 생성
  - Addressables 프리팹이 등록되면 기존처럼 프리팹을 우선 사용
- 런타임 fallback 팝업은 UIManager 스택에 등록되지 않으므로 `Close()` 시 직접 `Destroy(gameObject)` 처리
- `InventorySlotUI`가 런타임 생성 슬롯의 자식 오브젝트를 이름으로 자동 바인딩하도록 보강

**검증:** `dotnet build .\Assembly-CSharp.csproj --no-restore` 재실행. 신규 fallback 코드 오류는 확인되지 않았고, 기존 Firebase 참조(`Firebase.Analytics`) 누락 오류 2건으로 전체 빌드는 계속 실패.

---

### 5. 도구 액션 상태 복귀 문제 수정

**파일:** `Assets/02_Scripts/02_StateMachine/FSM/StateMachine.cs`, `Assets/02_Scripts/03_Entity/Player/PlayerState/Root/PlayerActionState.cs`, `Assets/02_Scripts/03_Entity/Player/PlayerState/Action/*.cs`, `Player.cs`, `PlayerRootStateBase.cs`, `PlayerRootStateMachine.cs`

- `StateMachine.OnUpdate(deltaTime)`, `OnGameUpdate(deltaTime)`가 실제 deltaTime을 상태에 전달하도록 수정
  - 기존에는 `Update()` / `FixedUpdate()`를 인자 없이 호출하여 시간 기반 상태 처리에 기본값 `1`이 들어가던 문제 개선
- `PlayerActionState`에 액션 상태 이탈 공통 처리 추가
  - 액션 진입 후 최소 `0.15초` 이후부터 입력 인터럽트 허용
  - 이동 입력 또는 Trace 입력 시 Locomotion 복귀
  - Attack 입력 시 `PlayerAttackState`로 전환
  - Jump 입력 시 점프 속도 적용 후 Locomotion 복귀
- 도구/상호작용 애니메이션이 Loop/Begin/Stop 구조라 완료 판정이 안 잡히는 경우를 대비해 `2.5초` timeout fallback 추가
- Pick/Mine/Chop/Dig/Ignite/Cook/Inspect/Build 하위 상태의 복귀 경로를 `PlayerActionState.ChangeToLocomotion()`으로 통일
- 액션 상태 파일 8개를 UTF-8 ASCII 로그/주석 형태로 정리하여 깨진 인코딩을 복구
- `PlayerActionState`에서 InputData 입력이 들어오지 않는 상황을 대비해 `Keyboard.current` / `Mouse.current` 직접 fallback 인터럽트 추가
  - WASD/방향키 입력 시 Locomotion 복귀
  - Space 입력 시 점프 속도 적용 후 Locomotion 복귀
  - 마우스 좌클릭 입력 시 `PlayerAttackState` 전환
- 개발빌드/에디터 디버그 오버레이에 현재 Root/Sub 상태와 입력 플래그를 표시하도록 보강
  - 액션 진입 후 `RootState: PlayerActionState`에서 `PlayerLocomotionState` 또는 `PlayerAttackState`로 빠지는지 화면에서 확인 가능

**검증:** `uloop` CLI를 재확인했으나 현재 Codex 셸 PATH에서 실행 파일을 찾지 못해 PlayMode 입력 주입 테스트는 수행하지 못함. `dotnet build .\Assembly-CSharp.csproj --no-restore` 재실행 결과 신규 액션 상태 전환/디버그 표시 코드 오류는 확인되지 않았고, 기존 Firebase 참조(`Firebase.Analytics`) 누락 오류 2건으로 전체 빌드는 계속 실패.

---

## 2026-04-13

### 1. 메인 카메라 수정

**파일:** `Assets/CustomPackage/Main/Screen/Camera/Prefabs/MainCameraObject.prefab`, `MainCamera.cs`, `ScreenManager.cs`

- `MainCameraObject.prefab` 카메라 투영 방식 Orthographic → **Perspective** 변경 (`orthographic: 0`)
- `MainCamera.GenerateObject()` 를 `async void` → `async UniTask GenerateObjectAsync()` 로 변경
  - `ScreenManager.OnInitializeAsync()`에서 `await`로 카메라 생성 완료 보장
  - lazy init 프로퍼티 getter는 `.Forget()` 처리

---

### 2. 허기 수치 공식 적용

**파일:** `Assets/02_Scripts/03_Entity/Player/PlayerStat/PlayerStatData.cs`, `PlayerStatus.cs`

- **하루 = 12분** (현실 기준)
- **최대 허기 공식:** `MaxHunger = HungerDrain * DayDurationMinutes * 2`
- `PlayerStatData`에서 `MaxHunger`, `CurHunger` 필드 제거
  - `DayDurationMinutes = 12f` 필드 추가
  - `MaxHunger`를 계산 프로퍼티로 교체 (`HungerDrain * DayDurationMinutes * 2f`)
- `PlayerStatus` 생성자: `CurrentHunger = data.MaxHunger` (최대치로 시작)

---

### 3. Photon 서버 연결 파라미터 값형식 전달

**파일:** `Assets/CustomPackage/Main/Network/NetworkManager.cs`, `MultiplayerExample.cs`

- `readonly struct RoomCreateArgs(string roomName, int maxPlayers = 4)` 추가
- `readonly struct RoomJoinArgs(string roomName)` 추가
- `CreateRoomAsync(string, int)` → `CreateRoomAsync(RoomCreateArgs)`
- `JoinRoomAsync(string)` → `JoinRoomAsync(RoomJoinArgs)`
- `JoinRoomAsync(SessionInfo)` 오버로드는 내부에서 `RoomJoinArgs`로 변환
- `MultiplayerExample.cs` 호출부 수정

---

### 4. Trace 자동이동 기능 구현

**파일:** `PlayerTracer.cs`(신규), `PlayerTraceState.cs`, `PlayerLocomotionState.cs`, `PlayerInputData.cs`, `Player.cs`

**개요:** 마우스 좌클릭으로 자원 레이어 오브젝트를 감지하면 표면에서 1f 거리까지 자동 이동

**`PlayerTracer.cs` (신규 컴포넌트)**
- `LayerMask resourceLayer`: Inspector에서 레이어 설정
- `float stopDistance = 1f`: 표면 정지 거리
- `Main.Loop.OnUpdate`에 등록, `Mouse.current.leftButton.wasPressedThisFrame` 감지
- Camera.main 기준 레이캐스트 → 히트 노멀 수평 성분으로 목적지 계산
  - 측면 히트: `hitPoint + 수평노멀 * stopDistance`
  - 수직 면(위/아래): 플레이어 → 타겟 방향 사용
  - Y좌표는 항상 플레이어 발 높이 유지
- 계산된 목적지를 `PlayerInputData.TraceDestination`에 기록

**`PlayerTraceState.cs`**
- `OnEnter()`: `Input.TraceDestination` 저장
- `Update()`: Walk 속도로 목적지 방향 이동, 도착 임계값(`0.25f`) 도달 시 Idle 복귀
- WASD 입력 또는 점프 입력 시 즉시 수동 취소

**`PlayerLocomotionState.cs`**
- `traceState` 인스턴스 추가 (`OnEnter()`에서 생성)
- `Update()` 상단에서 `Input.TracePressed && Motor.IsGrounded` → `ChangeToTrace()`

**`PlayerInputData.cs`**
- `TracePressed`, `TraceDestination` 추가
- `ConsumeEventInputs()`에서 `TracePressed` 리셋

**`Player.cs`**
- `playerTracer` 필드 추가, `Start()`에서 `playerTracer?.Bind(inputData)` 바인딩
- `OnValidate()`에서 `GetComponent<PlayerTracer>()` 자동 할당

**사용법:** Player 프리팹에 `PlayerTracer` 컴포넌트 추가 후 Inspector에서 `Resource Layer` 설정

---

### 5. Player 프리팹 컴포넌트 세팅 (uloop)

**프리팹:** `Assets/03_Prefabs/Player/Player.prefab`

- uloop으로 `PlayerTracer` 컴포넌트를 Player 프리팹 루트에 추가 및 저장
- 최종 컴포넌트 구성: `Transform`, `CharacterController`, `Player`, `PlayerMotor`, `PlayerFirstPersonCameraController`, `PlayerTracer`
- Inspector에서 `PlayerTracer.resourceLayer` 및 `stopDistance` 설정 필요
