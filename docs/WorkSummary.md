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

### 5. 1인칭 도구 전용 카메라 및 장착 표시 보정

**파일:** `Assets/02_Scripts/03_Entity/Player/PlayerFirstPersonCameraController.cs`, `ProjectSettings/TagManager.asset`, `Assets/05_Datas/ItemData_JYJ/Item_SurvivalTools/Tool_Axe.asset`, `Tool_Pickaxe.asset`, `Tool_Torch.asset`, `Assets/03_Prefabs/Item_Prefabs/Item/non-assets/Tool_Torch.prefab`

- Cinemachine으로 제어되는 메인 카메라는 그대로 유지하고, 도구 표시 전용 `ToolCamera`를 별도로 생성하도록 구성
  - Cinemachine Brain의 출력 카메라를 찾아 그 자식으로 `ToolCamera` 생성
  - URP `Overlay` 카메라로 설정하고 Base Camera의 camera stack에 추가
  - 기본 카메라는 `ViewModel` 레이어를 제외하고, `ToolCamera`는 `ViewModel` 레이어만 렌더링
- `ProjectSettings/TagManager.asset`에 `ViewModel` 레이어 추가
- 멀티플레이 환경을 고려하여 로컬 플레이어(`NetworkObject.HasInputAuthority`)만 도구 카메라와 장착 도구 view clone을 생성하도록 처리
- 장착 도구 prefab clone을 `ToolPivot` 아래에 생성하고, 모든 하위 오브젝트를 `ViewModel` 레이어로 변경
- 도구 view clone에서 Rigidbody/Collider가 1인칭 시야나 raycast/물리에 간섭하지 않도록 Rigidbody 제거, Collider 비활성화/제거 처리
- 장착/해제 시 `IEquipable.Equip()` / `IEquipable.Unequip()`을 호출하도록 수정
  - torch 장착 시 Light가 켜지고, 해제 시 Light가 꺼지며 clone이 제거되도록 보정
- 도끼/곡괭이/torch의 `ItemDataSO.prefab` 참조가 비어 있던 문제 수정
  - `Tool_Axe.asset` -> `Tool_Axe.prefab`
  - `Tool_Pickaxe.asset` -> `Tool_Pickaxe.prefab`
  - `Tool_Torch.asset` -> `Tool_Torch.prefab`
- `Tool_Torch.prefab`의 `Item_SurvivalTool.itemData` 참조가 비어 있던 문제 수정
- torch prefab이 큐브 형태라 1인칭 view에서 너무 크게 보이는 문제를 줄이기 위해 torch 전용 위치/회전/스케일 보정값 추가
  - 기본 도구 위치: `(-1, 0, 3.2)`
  - torch 위치/회전/스케일: `(-1, 0.25, 3.2)`, `(0, 0, -8)`, `(0.18, 0.85, 0.18)`

**검증**

- `uloop compile` 실행 결과 컴파일 에러 0개
- Play Mode에서 `W` 이동 및 마우스 회전을 주입하여 도끼/곡괭이/torch가 시야 하단 오른쪽에 유지되는지 확인
- 도끼/곡괭이 view clone의 Rigidbody가 제거되고 Collider가 비활성화되어 이동 중 위치가 흔들리지 않는 것을 확인
- torch 장착 시 Point Light가 켜지고, `UnequipItem(EquipSlot.Hand)` 호출 후 한 프레임 뒤 `Tool_Torch(Clone)`과 켜진 Point Light가 모두 사라지는 것을 확인
- ToolCamera가 `MainCameraObject(Clone)` 아래에 생성되고 URP Overlay로 Base Camera stack에 포함되는 것을 확인
- Unity Console Error 0개 확인

**참고**

- `Assets/03_Prefabs/Player/Player.prefab`에는 `ToolPivot` scale의 미세한 float diff(`0.10000001` -> `0.100000024`)가 남아 있음. 기능 변경 목적의 수정은 아니며 별도 되돌림은 수행하지 않음.

---

### 6. 1인칭 도끼/곡괭이 화면 구도 보정

**파일:** `Assets/02_Scripts/03_Entity/Player/PlayerFirstPersonCameraController.cs`

- 사용자가 제공한 Unity Play Mode 스크린샷 기준으로 도끼 view transform 보정
  - `Tool_Axe(Clone)` 루트는 기존 `ToolPivot` 기준 위치를 유지
  - 실제 렌더러 child인 `axe` 오브젝트에 별도 위치/회전/스케일 적용
  - 적용값: Position `(1.38, -1.5, 2.56)`, Rotation `(26.14, -168.4, -12.8)`, Scale `(7, 7, 1)`
- 사용자가 제공한 Unity Play Mode 스크린샷 기준으로 곡괭이 view transform 보정
  - `Tool_Pickaxe(Clone)` 루트 transform에 직접 위치/회전/스케일 적용
  - 적용값: Position `(-0.07, -0.86, 2.33)`, Rotation `(6.952, -148.7, -5.915)`, Scale `(7, 7, 7)`
- 도구 view clone의 Collider는 `Item_SurvivalTool`의 `RequireComponent(typeof(Collider))` 제약 때문에 삭제하지 않고 비활성화만 하도록 변경
  - `Can't remove BoxCollider because Item_SurvivalTool depends on it` 경고 방지
  - Collider는 남지만 `enabled = false`라 raycast/physics 간섭은 하지 않음

**검증**

- `uloop compile` 실행 결과 컴파일 에러 0개
- Play Mode에서 도끼 장착 후 `axe` child transform 값이 요청 스크린샷 기준 값으로 적용되는지 확인
- Play Mode에서 곡괭이 장착 후 `Tool_Pickaxe(Clone)` root transform 값이 요청 스크린샷 기준 값으로 적용되는지 확인
- Game View 캡처로 도끼/곡괭이 머리가 오른쪽 하단에 크게 보이고 손잡이가 아래로 내려오는 구도 확인
- Play Mode 종료 완료

**참고**

- 테스트 중 콘솔에 남은 `PlayerMotor.get_VerticalVelocity()` NullReference와 `InputSystem_Actions` finalize 경고는 이번 도구 view 보정 코드가 아니라 기존 디버그 GUI/Input System 정리 문제로 확인됨.
- 작업 후 수정한 C# 스크립트는 UTF-8로 재저장함.

---

### 7. 마우스 휠 인벤토리 포커스 슬롯 순환 문제 수정

**파일:** `Assets/02_Scripts/03_Entity/Player/PlayerInventory.cs`, `Assets/02_Scripts/04_Item/InventoryDisplayUI.cs`

- 마우스 휠로 퀵슬롯 선택을 이동할 때 일부 인덱스에서만 포커스 블럭이 켜지는 문제 수정
- 원인
  - `PlayerInventory`는 전체 인벤토리 슬롯 수(`slotCount = 16`) 기준으로 선택 인덱스를 순환
  - HUD 인벤토리 UI는 실제 표시 슬롯이 10개라 `SelectedSlotIndex`가 10~15로 넘어가면 화면에 대응되는 슬롯이 없어 포커스가 사라짐
  - UI 슬롯 수집 순서가 Hierarchy 순서에 의존해 `Slot0`, `Slot1` 순서와 어긋날 가능성이 있었음
- `PlayerInventory`에 퀵슬롯 선택 범위(`quickSlotCount`) 추가
  - `SetQuickSlotCount(int count)` 추가
  - `SelectSlot`, `MoveSelectedSlot`이 전체 인벤토리 슬롯 수가 아니라 퀵슬롯 수 기준으로 clamp/wrap 되도록 변경
- `InventoryDisplayUI`에서 실제 HUD 슬롯 개수를 `PlayerInventory.SetQuickSlotCount(slotUIs.Count)`로 전달
- `InventoryDisplayUI.CollectSlots()`에서 슬롯 이름 끝 숫자를 기준으로 `Slot0`, `Slot1`, ..., `Slot9` 순서로 정렬
  - 이름 숫자를 찾지 못하면 기존 sibling index를 fallback으로 사용

**검증**

- `uloop compile` 실행 결과 컴파일 에러 0개
- Play Mode에서 `slotCount=16`, `quickSlotCount=10`으로 설정되는 것 확인
- `MoveSelectedSlot(1)` 반복 호출 시 `Slot0`부터 `Slot9`까지 순서대로 포커스가 켜지고, `Slot9` 다음 `Slot0`으로 순환하는 것 확인
- 실제 `simulate-mouse-input --action Scroll` 입력 후 `SelectedSlotIndex`와 포커스 Outline이 켜진 슬롯 인덱스가 일치하는 것 확인
- Unity Console Error 0개 확인
- Play Mode 종료 완료

**참고**

- 작업 후 수정한 C# 스크립트는 UTF-8로 재저장함.

---

### 8. 플레이어 인벤토리 크기 20칸 확장 및 UI 연동

**파일:** `Assets/02_Scripts/03_Entity/Player/PlayerInventory.cs`, `Assets/02_Scripts/04_Item/InventoryDisplayUI.cs`, `Assets/03_Prefabs/Player/Player.prefab`

- 플레이어 인벤토리 기본 슬롯 수를 16칸에서 20칸으로 변경
  - `PlayerInventory.slotCount` 기본값 `20`으로 변경
  - `Player.prefab`에 직렬화된 `slotCount` 값도 `20`으로 변경
- HUD 인벤토리 UI가 20개 슬롯을 올바르게 수집하도록 수정
  - 기존 `ResolveReferences()`가 `InvenSlots`보다 다른 `Slots` 컨테이너를 먼저 잡을 수 있던 문제 보정
  - `InvenSlots`를 우선 선택하고, 없을 때만 `Slots`를 fallback으로 사용
- `InvenSlots` 바로 아래에 `top`, `bottom` 행이 있고 실제 슬롯은 그 하위에 있는 구조를 반영
  - 직접 자식만 슬롯으로 수집하던 방식에서 하위 전체를 탐색해 `Slot0`~`Slot19` 이름의 오브젝트를 수집하도록 변경
  - 수집한 슬롯은 숫자 suffix 기준으로 정렬해 `Slot0`, `Slot1`, ..., `Slot19` 순서 보장
- 실제 표시 슬롯 개수 20개를 `PlayerInventory.SetQuickSlotCount(20)`으로 전달하여 마우스 휠 포커스 순환 범위도 20칸으로 확장

**검증**

- `uloop compile` 실행 결과 컴파일 에러 0개, 경고 0개
- Play Mode에서 `slotCount=20`, `slots=20`, `stackCounts=20`, `quickSlotCount=20`, `uiSlotList=20` 확인
- UI 슬롯 수집 순서가 `Slot0`, `Slot1`, `Slot2`, ..., `Slot17`, `Slot18`, `Slot19`로 정렬되는지 확인
- `Slot18 -> Slot19 -> Slot0 -> Slot1` 순환 확인
- 실제 마우스 휠 입력 주입 후 선택 인덱스와 포커스 Outline 슬롯이 일치하는 것 확인
- Game View 스크린샷으로 20칸 HUD 인벤토리 표시 확인
- Unity Console Error 0개 확인
- Play Mode 종료 완료

**참고**

- 작업 후 수정한 C# 스크립트는 UTF-8로 재저장함.

---

### 9. 마우스 휠 퀵슬롯 선택 2칸 이동 문제 수정

**파일:** `Assets/02_Scripts/03_Entity/Player/PlayerInventory.cs`

- 마우스 휠을 한 번 돌렸을 때 선택 인덱스가 `0 -> 1`이 아니라 `0 -> 2`처럼 두 칸 이동하는 문제 수정
- 원인
  - `PlayerInventory.ReadFallbackKeyboardInput()`에서 `Mouse.current.scroll.ReadValue().y`를 매 프레임 직접 읽음
  - 한 번의 휠 입력 값이 2프레임 이상 유지되면 `QuickSlotScrollDelta`가 연속으로 설정되어 `MoveSelectedSlot()`이 두 번 호출됨
- `wasReadingScrollInput` 플래그 추가
  - 스크롤 입력이 새로 들어온 첫 프레임에만 `QuickSlotScrollDelta`를 설정
  - 같은 스크롤 값이 다음 프레임까지 남아 있어도 중복 이동하지 않도록 처리

**검증**

- `uloop compile` 실행 결과 컴파일 에러 0개
- Play Mode에서 선택 인덱스를 0으로 둔 뒤 마우스 휠 아래 입력 1회 주입 시 `selected=1`, `focused=1(Slot1)` 확인
- Play Mode에서 선택 인덱스를 1로 둔 뒤 마우스 휠 위 입력 1회 주입 시 `selected=0`, `focused=0(Slot0)` 확인
- Unity Console Error 0개 확인
- Play Mode 종료 완료

**참고**

- 작업 후 수정한 C# 스크립트는 UTF-8로 재저장함.

---

### 10. 장착 도구 즉시 교체 시 인벤토리/뷰 상태 꼬임 수정

**파일:** `Assets/02_Scripts/03_Entity/Player/PlayerInventory.cs`, `Assets/02_Scripts/03_Entity/Player/PlayerMotor.cs`

- 도구를 장착한 상태에서 바로 다른 도구를 장착할 때 호출 순서와 인벤토리 상태가 꼬일 수 있는 문제 확인 및 수정
- 원인
  - 인벤토리 슬롯에 아이템 참조는 남아 있지만 수량이 0인 `Tool_Axe x0`, `Tool_Pickaxe x0` 같은 유령 슬롯이 남을 수 있었음
  - 유령 슬롯은 UI에 아이콘이 보이지만 실제 수량이 없어 장착/교체 호출이 비정상 흐름으로 이어질 수 있음
  - 기존 `EquipFromSlot()`은 이전 장착 아이템을 되돌릴 때 public `AddItem()`을 호출하여 장착 교체 중간에 `OnInventoryChanged`가 먼저 발행될 수 있었음
- `PlayerInventory` 보정
  - `InitializeSlots()`, `RemoveItem()`, `FillExistingStacks()`에서 수량 0 이하 슬롯을 `ClearSlot()`으로 정리
  - `GetItemCount()`는 수량이 0보다 큰 슬롯만 카운트하도록 변경
  - `EquipFromSlot()`은 수량 0 이하 슬롯에서 장착을 시도하지 않고 슬롯을 정리하도록 변경
  - 내부 전용 `TryAddItemToSlots()`를 추가하여 장착 교체 중 이전 장비 반환을 `OnInventoryChanged` 중간 발행 없이 처리
  - 장착 교체 완료 후 `OnInventoryChanged`와 `OnEquippedItemChanged`가 안정적인 최종 상태 기준으로 호출되도록 정리
- 테스트 중 콘솔을 흐리던 개발용 디버그 오류 수정
  - `PlayerMotor.VerticalVelocity`가 `gravity` 초기화 전 호출되면 `0f`를 반환하도록 null guard 추가

**검증**

- `uloop compile` 실행 결과 컴파일 에러 0개
- Play Mode에서 도끼 장착 직후 곡괭이 장착 재현
  - 교체 후 `EquippedHand = Tool_Pickaxe`
  - 도끼는 인벤토리 슬롯에 `Tool_Axe x1`로 정상 반환
  - `Tool_Axe x0`, `Tool_Pickaxe x0` 유령 슬롯이 더 이상 생성되지 않음
- 한 프레임 뒤 view clone이 `Tool_Pickaxe(Clone)` 하나만 남는 것 확인
- Unity Console Error 0개 확인
- Play Mode 종료 완료

**참고**

- 작업 후 수정한 C# 스크립트는 UTF-8로 재저장함.

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

---

## 2026-05-11

### 1. 장착 입력 중복 호출 및 인벤토리 교체 흐름 보정

**파일:** `Assets/02_Scripts/03_Entity/Player/PlayerInputHandler.cs`, `Assets/02_Scripts/03_Entity/Player/PlayerInventory.cs`, `Assets/02_Scripts/04_Item/InventoryDisplayUI.cs`

- `InputActions_PlayerInputHandler`의 `Interact` 처리를 `performed`가 아닌 `started` 기준으로 변경해 `Hold` 인터랙션에서 E 입력이 즉시 처리된 뒤 지연 `performed`로 한 번 더 처리되는 문제를 차단
- `InputActions_PlayerInputHandler.Connect()`가 재호출될 때 기존 이벤트 구독을 해제한 뒤 다시 연결하도록 보정하고, 같은 프레임 중복 장착 입력을 입력 핸들러 단계에서 무시하도록 처리
- `PlayerInventory`에는 입력 디바운스 로직을 두지 않고, 인벤토리/장착 상태 처리만 담당하도록 정리
- 이미 장착 중인 동일 아이템을 같은 슬롯에서 다시 장착하려는 경우 상태를 뒤집지 않고 무시하도록 보정
- 장착 아이템 교체 시 선택 슬롯이 비면서 생기는 공간을 고려해, 인벤토리가 가득 찬 상태에서도 기존 장착 아이템을 되돌릴 수 있으면 정상 교체되도록 수정
- `InventoryDisplayUI`가 임의의 `PlayerInventory`를 잡지 않도록 로컬 플레이어 인벤토리를 우선 탐색하고, 이미 인벤토리에 바인딩된 UI는 Tab 직접 토글을 하지 않도록 보정

**검증:** uLoop PlayMode에서 로컬 플레이어 인벤토리를 20칸으로 세팅한 뒤 E 입력을 길게 주입해도 곡괭이 장착 로그가 1회만 발생함을 확인. 이어서 `곡괭이 -> 횃불 -> 곡괭이` 교체 시 로그가 `곡괭이 해제 / 횃불 장착 및 켜짐 / 횃불 해제 및 꺼짐 / 곡괭이 장착` 순서로만 발생하고 중복 장착/해제 로그가 재발하지 않음을 확인. 최종 Unity 컴파일 결과 Error 0, 기존 Warning 8건.

---

## 2026-05-18

### 1. WorldGenManager 플레이 모드 생성/실행 에디터 툴 추가

**파일:** `Assets/02_Scripts/05_World/WorldGen/Editor/WorldGenEditorTool.cs`, `docs/WorkSummary.md`

- `Tools/World Gen/Test World Generator` 에디터 윈도우 추가
- `Tools/World Gen/Generate Test World` 메뉴 실행 추가
- Play Mode에서 `WorldGenManager`가 없으면 새 GameObject에 `WorldGenManager` 컴포넌트를 생성
- 씬에 있는 `WorldGenManager`가 비활성 상태이면 활성화 후 실행 대기
- `WorldGenManager`가 있거나 생성된 뒤 `_worldSettings` 로딩 완료 상태를 기다렸다가 기존 `GenerateWorldFromUI` 호출
- 기본 호출값은 기존 월드 생성 UI와 같은 `WorldBranchSetting.Default`, `WorldLoopSetting.Default`

**왜 변경했는지:** `UI_KGB_TestScene`을 대신해 플레이 모드에서 빠르게 월드 생성을 테스트할 수 있도록 하기 위함. 기존 `WorldGenManager`와 경보씨 월드 생성 파이프라인 코드는 수정하지 않음.

**남은 TODO/이슈:** Unity Editor에서 메뉴 실행 후 실제 Play Mode 월드 생성 동작 확인 필요. 생성 직후 `Main` 초기화 또는 `TestWorldSettings` 로딩이 늦어지면 10초 타임아웃 경고가 표시될 수 있음.
 
### 2. PHN_TestScene 플레이어 상태 UI 크기 조정

**파일:** `Assets/01_Scenes/Test/PHN_TestScene.unity`, `docs/WorkSummary.md`

- 현재 배치 상태에서 UI 묶음 전체 크기만 줄이기 위해 `StatusBars` RectTransform scale을 0.45로 조정
- 비율 잠금을 위해 `m_ConstrainProportionsScale`을 켜서 X/Y/Z scale이 함께 유지되도록 설정

**왜 변경했는가:** 요청한 Hunger/HP/Ego 배치 상태는 유지하면서 화면에서 차지하는 UI 크기만 줄이기 위함.

**남은 TODO/이슈:** Unity Editor Game View에서 16:9, 16:10, 4:3 등 여러 해상도로 실제 위치와 크기 확인 필요. 현재 Codex 셸 PATH에서 `uloop` 실행 파일을 찾지 못해 uloop 검증은 수행하지 못함.

---

## 2026-05-19

### 1. 인벤토리 UI 프레임워크 패턴 정리

**파일:** `Assets/02_Scripts/04_Item/InventoryDisplayUI.cs`, `docs/WorkSummary.md`

- `InventoryDisplayUI`가 `MonoBehaviour` 대신 `UI_Hud`를 상속하도록 변경
- `UI_PlayerStatus`와 같은 방식으로 `Initialize()`에서 UI 참조 수집 및 초기 표시 상태 설정, `Set(PlayerInventory)`에서 인벤토리 바인딩 처리
- `ShowFor()`가 `Main.UI.ShowHudOverlay`를 직접 호출하지 않고 `Extensions.ShowHud<InventoryDisplayUI>()`를 통해 UI 프레임워크 진입점을 사용하도록 변경
- UI 스크립트의 직접 Tab 입력 감지를 제거하고, 인벤토리 열림/닫힘 상태는 기존처럼 `PlayerInventory` 이벤트를 통해 반영

**변경 이유:** 인벤토리 UI가 프로젝트 UI 베이스(`UI_Hud`/`UI_Panel`)와 `Extensions` 기반 생성 흐름을 따르지 않아 다른 HUD 스크립트와 구조가 달랐기 때문.

**검증:** `dotnet build .\Assembly-CSharp.csproj --no-restore`를 실행했으나 기존 Firebase Analytics 참조 오류 2건(`Firebase.Analytics` 네임스페이스 누락)으로 전체 빌드는 실패. 이번 인벤토리 UI 변경에서 발생한 신규 컴파일 오류는 확인되지 않음.

**남은 TODO/이슈:** Unity Editor에서 `InventoryUI` Addressable 프리팹을 열어 `UI_Hud` 상속 필드 직렬화 상태를 저장하고, 실제 Game View에서 인벤토리 HUD와 다른 HUD 표시 흐름이 의도대로 동작하는지 확인 필요.

### 2. 줄바꿈 정책을 Git 정규화와 일치하도록 조정

**파일:** `.editorconfig`, `docs/WorkSummary.md`

- `.editorconfig`의 `end_of_line` 값을 `crlf`에서 `lf`로 변경
- 저장소의 `.gitattributes` (`* text=auto`) 및 Git LF 정규화 정책과 EditorConfig 줄바꿈 규칙이 서로 충돌하지 않도록 정리

**변경 이유:** Visual Studio에서 LF 정규화 경고가 반복된 원인이 UTF-8 인코딩이 아니라 줄바꿈 정책 불일치였기 때문. Git은 LF를 기준으로 정규화하는데 EditorConfig가 CRLF를 요구하고 있어 워킹트리에서 계속 충돌이 발생하고 있었음.

**검증:** 설정 확인 결과 `.editorconfig`는 `charset = utf-8`, `.gitattributes`는 `* text=auto`, 로컬 Git 설정은 `core.autocrlf=true`였음. 이번 변경으로 저장소 기준 줄바꿈 정책과 EditorConfig 규칙은 일치하게 됨.

**남은 TODO/이슈:** 이미 워킹트리에 `mixed` 또는 `crlf`로 남아 있는 파일은 필요 시 별도 renormalize가 필요할 수 있음. 이번 변경에서는 설정만 맞추고 대규모 파일 churn은 만들지 않음.

### 3. UI_Hud_Player 루트 HUD 구성 완성

**파일:** `Assets/02_Scripts/01_UI/Player/UI_Hud_Player.cs`, `Assets/02_Scripts/@Scripts/UI/Components/Player/UI_Panel_PlayerStatus.cs`, `Assets/03_Prefabs/UI/Player/UI_Hud_Player.prefab`, `docs/WorkSummary.md`

- `UI_Hud_Player`를 플레이어 HUD의 단일 루트 `UI_Hud`로 동작하도록 완성
- `UI_Hud_Player.Set(Player)`에서 `UI_Panel_PlayerStatus`와 `UI_Panel_PlayerInventory`를 함께 활성화하고 각각 플레이어/인벤토리에 바인딩하도록 구성
- `UI_Hud_Player.Set(PlayerInventory)` 오버로드를 추가해 인벤토리만 바인딩하는 호출도 지원
- `UI_Hud_Player.prefab` 루트에 누락되어 있던 `UI_Hud_Player` 컴포넌트를 추가해 `Extensions.ShowHud<UI_Hud_Player>()` 로드가 가능하도록 수정
- `UI_Panel_PlayerStatus`의 불필요한 `UnityEngine.Rendering.DebugUI` using을 제거

**변경 이유:** 현재 UI 프레임워크에서 `UI_Hud`는 한 번에 하나만 유지되는 루트 HUD 역할이므로, 플레이어 상태/인벤토리는 각각 독립 HUD가 아니라 `UI_Hud_Player` 아래의 `UI_Panel`로 묶여야 하기 때문.

**검증:** `dotnet build .\Assembly-CSharp.csproj --no-restore`를 실행했으며, HUD 변경으로 인한 신규 컴파일 오류는 확인되지 않음. 전체 빌드는 기존 `Firebase.Analytics` 네임스페이스 누락 오류 2건으로 실패.

**남은 TODO/이슈:** `Player.cs` 호출부는 현재 별도 작업 중이므로 이번 변경에서 수정하지 않음. 최종적으로는 `Extensions.ShowHud<UI_Hud_Player>("UI_Hud_Player")` 후 `Set(player)` 형태로 호출하는 것이 프레임워크에 맞음.

### 4. Player HUD 패널 초기 비활성화 보강

**파일:** `Assets/02_Scripts/01_UI/Player/UI_Hud_Player.cs`, `Assets/03_Prefabs/UI/Player/UI_PlayerInventory.prefab`, `docs/WorkSummary.md`

- `UI_Hud_Player.Initialize()`에서 상태 패널과 인벤토리 패널을 처음에는 모두 비활성화하도록 `SetInitialPanelState()`를 추가
- `Set(Player)` 호출 시 `UI_Panel_PlayerStatus`를 활성화한 뒤 플레이어를 바인딩하도록 정리
- 인벤토리 패널은 바인딩 뒤 `PlayerInventory.IsOpen` 상태에 따라 켜지거나 꺼지도록 정리
- `UI_PlayerInventory.prefab` 루트 오브젝트가 초기 비활성화 상태로 저장되어 있는지 확인

**변경 이유:** `UI_PlayerStatus`처럼 인벤토리 UI도 HUD 로드 직후에는 꺼져 있다가, 실제 플레이어/인벤토리 데이터가 연결되는 시점에 켜지는 흐름이 프레임워크와 프리팹 구성에 더 잘 맞기 때문.

**검증:** `UI_Hud_Player.cs`에서 `SetInitialPanelState()`가 `UI_PlayerStatus`와 `UI_PlayerInventory`를 모두 `SetActive(false)` 처리하고, 각 `Set(...)` 메서드에서 필요한 패널만 다시 `SetActive(true)` 처리하는 것을 확인. `UI_PlayerInventory.prefab` 루트 `GameObject`의 `m_IsActive` 값이 `0`인 것도 확인.

**남은 TODO/이슈:** Unity Editor Play Mode에서 `UI_Hud_Player`가 Addressables로 로드될 때 두 패널이 초기에 보이지 않고, `Set(player)` 이후 상태/인벤토리 패널이 정상 표시되는지 최종 확인 필요.

### 5. Inventory UI 참조 선바인딩 구조 적용

**파일:** `Assets/02_Scripts/01_UI/Player/Inventory/UI_Panel_PlayerInventory.cs`, `Assets/02_Scripts/01_UI/Player/Inventory/InventorySlotUI.cs`, `Assets/03_Prefabs/UI/Player/UI_PlayerInventory.prefab`, `docs/WorkSummary.md`

- `UI_Panel_PlayerInventory`의 슬롯 리스트와 장비 슬롯 UI 참조를 직렬화 필드로 변경해 에디터에서 유지할 수 있도록 수정
- `OnValidate()`에서 컨테이너/슬롯 참조가 비어 있으면 미리 수집하도록 추가
- `OnEnable()`에서 `FindObjectsByType`/`FindFirstObjectByType`로 로컬 플레이어 인벤토리를 찾던 경로를 제거하고, `UI_Hud_Player.Set(player)` 흐름에서 전달받은 인벤토리만 바인딩하도록 변경
- `InventorySlotUI`가 `Refresh()`마다 자식 Transform을 반복 탐색하지 않도록 `Awake()`/`OnValidate()`에서 참조를 한 번 해석하고 `referencesResolved`로 캐시하도록 수정
- `UI_PlayerInventory.prefab`의 `slotContainer`와 `equipSlotContainer`를 실제 RectTransform 참조로 연결

**변경 이유:** 인벤토리 UI가 활성화될 때마다 자식 오브젝트나 씬 전체를 탐색하면 비용과 동작 예측성이 나빠지므로, 프리팹/에디터 단계에서 가능한 참조를 미리 들고 있도록 하기 위함.

**검증:** `dotnet build .\Assembly-CSharp.csproj --no-restore`를 실행했으며, 이번 인벤토리 UI 변경으로 인한 신규 컴파일 오류는 확인되지 않음. 전체 빌드는 기존 `Firebase.Analytics` 네임스페이스 누락 오류 2건으로 실패.

**남은 TODO/이슈:** Unity Editor에서 프리팹을 한번 열거나 스크립트 리로드 후 `OnValidate()`가 실행되면 `slotUIs`, `headSlotUI`, `chestSlotUI`, `handSlotUI`가 에디터 직렬화 값으로 채워지는지 확인 필요. 현재 런타임 fallback은 참조 누락 방어용으로만 남겨둠.

### 6. Inventory UI 활성화 방식 및 슬롯 겹침 수정

**파일:** `Assets/02_Scripts/01_UI/Player/Inventory/UI_Panel_PlayerInventory.cs`, `Assets/03_Prefabs/UI/Player/UI_PlayerInventory.prefab`, `docs/WorkSummary.md`

- 인벤토리 표시/숨김을 `CanvasGroup` 알파 제어 대신 `gameObject.SetActive()` 기반으로 변경해 Status UI와 같은 활성화 방식으로 정리
- 비활성 상태에서도 살아있는 `UI_Hud_Player`가 `PlayerInventory.OnInventoryOpenChanged` 이벤트를 받아 다시 켤 수 있도록 패널 내부의 열림/닫힘 책임을 제거
- 슬롯 수집 로직이 하위 아이콘/텍스트까지 슬롯 후보로 잡지 않도록 직접 자식/한 단계 하위 슬롯 루트만 수집하게 제한
- 직렬화된 `slotUIs`가 잘못된 슬롯 루트를 들고 있으면 런타임에서 다시 수집하도록 검증 로직 추가
- `UI_PlayerInventory.prefab`의 루트 `CanvasGroup`을 제거하고, 닫힌 상태에서는 실제 오브젝트가 꺼지도록 프리팹/루트 HUD 초기 상태를 정리
- 슬롯 행 `HorizontalLayoutGroup`의 `Child Control Width`/`Child Force Expand Width`를 다시 켜 슬롯들이 한 지점에 겹치지 않고 행 안에 배치되도록 수정

**변경 이유:** 인벤토리 UI가 Status UI와 다르게 CanvasGroup으로만 숨겨지고, 슬롯 직렬화/수집 과정에서 잘못된 슬롯 후보나 레이아웃 설정이 섞이면 슬롯들이 서로 겹쳐 보일 수 있었기 때문.

**검증:** `dotnet build .\Assembly-CSharp.csproj --no-restore`를 실행했으며, 이번 인벤토리 UI 수정으로 인한 신규 컴파일 오류는 확인되지 않음. 전체 빌드는 기존 `Firebase.Analytics` 네임스페이스 누락 오류 2건으로 실패.

**남은 TODO/이슈:** Unity Editor Play Mode에서 인벤토리 토글 시 `UI_PlayerInventory` GameObject가 실제로 꺼졌다 켜지는지와, 두 줄 슬롯이 겹치지 않고 LayoutGroup 기준으로 정렬되는지 Game View 확인 필요.

### 7. Inventory UI 활성화 직후 레이아웃 강제 갱신 추가

**파일:** `Assets/02_Scripts/01_UI/Player/Inventory/UI_Panel_PlayerInventory.cs`, `docs/WorkSummary.md`

- `UI_Hud_Player`가 `SetActive(true)`로 인벤토리 패널을 다시 켠 뒤 `Canvas.ForceUpdateCanvases()`와 `LayoutRebuilder.ForceRebuildLayoutImmediate()`를 호출해 레이아웃을 즉시 갱신
- `RefreshUI()`와 `ClearSlots()` 이후에도 슬롯/장비 슬롯/루트 RectTransform의 레이아웃을 다시 계산하도록 보강
- Unity UI 레이아웃 API 사용을 위해 `UnityEngine.UI` 네임스페이스를 추가

**변경 이유:** 인벤토리 GameObject를 수동으로 껐다 켜면 슬롯 위치가 정상으로 돌아가는 현상은 데이터 문제가 아니라 Unity `LayoutGroup`의 레이아웃 갱신 타이밍 문제이기 때문. 코드 경로에서도 수동 토글과 같은 Canvas/Layout 갱신을 실행하도록 처리.

**검증:** `dotnet build .\Assembly-CSharp.csproj --no-restore`를 실행했으며, 이번 레이아웃 리빌드 코드로 인한 신규 컴파일 오류는 확인되지 않음. 전체 빌드는 기존 `Firebase.Analytics` 네임스페이스 누락 오류 2건으로 실패.

**남은 TODO/이슈:** Unity Editor Play Mode에서 최초 인벤토리 표시 시 수동 GameObject 토글 없이도 슬롯 위치가 정상화되는지 Game View 확인 필요.

### 8. Inventory UI 토글 입력 및 HUD 표시 책임 보정

**파일:** `Assets/02_Scripts/01_UI/Player/UI_Hud_Player.cs`, `Assets/02_Scripts/01_UI/Player/Inventory/UI_Panel_PlayerInventory.cs`, `Assets/02_Scripts/01_UI/Player/Inventory/InventorySlotUI.cs`, `Assets/02_Scripts/03_Entity/Player/PlayerInputHandler.cs`, `Assets/InputSystem_Actions.inputactions`, `Assets/InputSystem_Actions.cs`, `Assets/03_Prefabs/UI/Player/UI_PlayerInventory.prefab`, `docs/WorkSummary.md`

- `UI_Hud_Player`가 살아있는 루트 HUD로 `PlayerInventory.OnInventoryOpenChanged`를 구독하고, 자식 `UI_Panel_PlayerInventory` GameObject를 켜고 끄도록 표시 책임을 이동
- `UI_Panel_PlayerInventory`는 자기 자신을 열고 닫는 이벤트 구독을 하지 않고, 전달받은 `PlayerInventory` 데이터 바인딩/새로고침/레이아웃 리빌드만 담당하도록 정리
- 인벤토리 패널이 꺼진 상태에서도 HUD 루트는 계속 살아 있으므로, 토글 이벤트를 놓치지 않고 다시 켤 수 있게 구성
- `InputSystem_Actions`에 `InventoryToggle` 액션을 추가하고 `Tab`, `I`, Gamepad `Start`를 바인딩
- `InputActions_PlayerInputHandler`가 `InventoryToggle` 액션을 찾아 `PlayerInputData.InventoryTogglePressed`를 세팅하도록 연결
- `InventorySlotUI`는 런타임 반복 탐색 대신 `Awake()`/`OnValidate()`에서 자식 참조를 캐시하도록 유지

**변경 이유:** 인벤토리 UI를 `SetActive(false)`로 끄면 비활성화된 패널 자신은 다시 켜지는 책임을 안정적으로 수행할 수 없고, 별도로 `InventoryTogglePressed`를 세팅하는 입력 연결도 빠져 있어 `PlayerInventory.Toggle()` 이벤트가 발생하지 않았기 때문.

**검증:** `Assets/InputSystem_Actions.inputactions`를 PowerShell `ConvertFrom-Json`으로 파싱해 JSON 형식이 유효한 것을 확인. `uloop compile` 실행 결과 `Success: true`, `ErrorCount: 0`, `WarningCount: 0` 확인. 추가로 `dotnet build .\Assembly-CSharp.csproj --no-restore`를 실행했을 때도 이번 입력/HUD 변경으로 인한 신규 컴파일 오류는 확인되지 않았고, 전체 빌드는 기존 `Firebase.Analytics` 네임스페이스 누락 오류 2건으로 실패.

**남은 TODO/이슈:** Unity Editor Play Mode에서 `Tab` 또는 `I` 입력 시 `UI_PlayerInventory`가 켜지고, 다시 누르면 꺼지는지 직접 확인 필요. 현재 Codex 세션에서는 `uloop compile`까지만 성공했고, Play Mode 조작/스크린샷 검증용 추가 uloop 명령은 권한 승인 사용량 제한으로 실행하지 못함. Firebase Analytics 참조 문제는 이번 작업 범위 밖의 기존 빌드 실패 원인으로 남아 있음.

### 9. Player Jump Ground Check and Running Jump 보정

**파일:** `Assets/02_Scripts/03_Entity/Player/PlayerMotor.cs`, `Assets/02_Scripts/03_Entity/Player/PlayerState/Root/PlayerGroundState.cs`, `Assets/02_Scripts/03_Entity/Player/PlayerState/Root/PlayerActionState.cs`, `docs/WorkSummary.md`

- `PlayerMotor`에 `CanJump` 프로퍼티를 추가해 점프 시작 가능 여부를 `CharacterController.isGrounded` 기준으로 분리
- `PlayerLocomotionState`의 점프 조건을 `WasGroundedRecently` 대신 `CanJump`로 변경해 공중에서 연속 점프가 다시 허용되지 않도록 수정
- Run/Walk 상태 업데이트가 스킵되는 점프 프레임에도 현재 이동 입력과 Sprint 여부를 기준으로 수평 속도를 먼저 넣고 Air 상태로 전환하도록 `StartJump()` 추가
- 점프 직후 아직 지면 감지 상태로 남아 있는 프레임에는 `airVelocity`에 현재 수평 속도를 저장해 달리기 점프의 초기 관성을 잃지 않도록 보정
- Action 상태의 점프 인터럽트와 개발용 키보드 fallback 점프도 `CanJump` 기준으로 맞춤

**변경 이유:** 기존 점프 조건이 `WasGroundedRecently`와 느슨한 SphereCast 기반 지면 감지에 묶여 있어, 점프 연타 시 실제로는 공중인데도 점프가 다시 허용될 여지가 있었다. 또한 Locomotion Root에서 점프를 먼저 처리하며 Run 하위 상태의 수평 속도 설정이 그 프레임에 실행되지 않아 달리기 점프의 추진력이 사라질 수 있었다.

**검증:** `uloop compile --wait-for-domain-reload true`를 시도했으나 현재 uloop가 `Another execution is already in progress` 상태라 컴파일/PlayMode 검증을 완료하지 못함.

**남은 TODO/이슈:** uloop 실행 락이 풀린 뒤 Unity 컴파일과 PlayMode에서 Space 연타 시 공중 재점프가 막히는지, Shift+W 상태에서 Space 입력 시 달리기 속도를 유지하며 점프하는지 확인 필요.

### 10. Player Hunger Max Value 직렬화 복구

**파일:** `Assets/02_Scripts/03_Entity/Player/PlayerStat/PlayerStatData.cs`, `Assets/02_Scripts/03_Entity/Player/PlayerStat/PlayerStatus.cs`, `docs/WorkSummary.md`

- `PlayerStatData.MaxHunger`를 `HungerDrain * DayDurationMinutes * 2` 계산 프로퍼티에서 직렬화 필드로 변경
- `PlayerStatData.CurHunger` 필드를 복구해 `PlayerStatData.asset`에 저장된 `CurHunger: 150` 값을 런타임에서 읽을 수 있도록 수정
- `PlayerStatus` 초기화 시 현재 허기를 `data.MaxHunger`가 아니라 `data.CurHunger`에서 가져오도록 변경

**변경 이유:** `PlayerStatData.asset`에는 `MaxHunger: 150`, `CurHunger: 150`이 저장돼 있었지만, 코드에서 `MaxHunger`가 계산 프로퍼티로 선언되어 직렬화 값을 무시하고 있었다. 이 때문에 UI가 의도한 `150/150` 대신 계산식 결과인 큰 값을 표시할 수 있었다.

**검증:** `PlayerStatData.asset`이 Addressables `PlayerStatData` 주소로 연결되어 있고, 해당 asset에 `MaxHunger: 150`, `CurHunger: 150`이 저장돼 있음을 확인. `uloop compile --wait-for-domain-reload true`는 현재 uloop가 `Another execution is already in progress` 상태라 완료하지 못함.

**남은 TODO/이슈:** uloop 실행 락이 풀린 뒤 Unity 컴파일과 PlayMode에서 Status UI 허기 표시가 `150/150`으로 나오는지 확인 필요.

### 11. Inventory Toggle Input 제거

**파일:** `Assets/InputSystem_Actions.inputactions`, `Assets/InputSystem_Actions.cs`, `Assets/02_Scripts/03_Entity/Player/PlayerInputData.cs`, `Assets/02_Scripts/03_Entity/Player/PlayerInputHandler.cs`, `Assets/02_Scripts/03_Entity/Player/PlayerInventory.cs`, `docs/WorkSummary.md`

- 인게임 하단바 인벤토리는 항상 표시되는 HUD이므로 별도 `InventoryToggle` 입력 액션을 제거
- `Tab`, `I`, Gamepad `Start`에 묶었던 `InventoryToggle` 바인딩 제거
- `PlayerInputData.InventoryTogglePressed`와 `PlayerInputHandler`의 수동 액션 검색/구독 로직 제거
- `PlayerInventory`의 `IsOpen`, `OnInventoryOpenChanged`, `Toggle()` 경로 제거

**변경 이유:** 현재 인벤토리는 열고 닫는 패널이 아니라 항상 하단바에 떠 있는 인게임 UI이므로, 별도 토글 입력과 open/close 상태를 갖는 것이 실제 UX와 맞지 않았다.

**검증:** `rg`로 `InventoryToggle`, `InventoryTogglePressed`, `OnInventoryOpenChanged`, `IsOpen`, `Toggle()` 참조가 남지 않았음을 확인. `Assets/InputSystem_Actions.inputactions`는 PowerShell `ConvertFrom-Json` 파싱을 통과했고, `git diff --check`도 통과.

**남은 TODO/이슈:** Unity 컴파일 및 PlayMode에서 하단 인벤토리가 항상 표시되는지 최종 확인 필요.

### 12. Player HUD Inventory Binding 복구

**파일:** `Assets/02_Scripts/01_UI/Player/UI_Hud_Player.cs`, `Assets/02_Scripts/01_UI/Player/Inventory/UI_Panel_PlayerInventory.cs`, `Assets/02_Scripts/@Scripts/UI/Components/Player/UI_Panel_PlayerStatus.cs`, `docs/WorkSummary.md`

- `UI_Hud_Player.Player`를 setter가 있는 프로퍼티로 변경해 `Player.cs`에서 `hud.Player = this`가 들어온 즉시 Status/Inventory 자식 패널에 Player를 전달하도록 수정
- `UI_Hud_Player.Start()`가 부모 `UI_Panel.Start()`를 가리지 않도록 `protected override void Start()`로 변경하고 `base.Start()` 호출 추가
- `UI_Panel_PlayerInventory.Player` setter에서 `player.Inventory`를 `Bind()`하도록 수정해 슬롯 UI가 실제 `PlayerInventory` 데이터를 받도록 복구
- `UI_Panel_PlayerStatus.Player` setter에서 PlayerStatus를 즉시 갱신하도록 보정

**변경 이유:** 인벤토리 슬롯 UI는 떠 있어도 `UI_Panel_PlayerInventory`에 `PlayerInventory`가 바인딩되지 않으면 `RefreshUI()`가 `ClearSlots()`만 실행하므로 아이템 아이콘이 표시될 수 없었다. 하단바 인벤토리는 항상 보이는 HUD이므로 Player 바인딩 시점에 인벤토리 데이터도 항상 연결되어야 한다.

**검증:** `git diff --check` 통과. 아이템 데이터 확인 결과 `Tool_Axe`, `Tool_Pickaxe`, `Tool_Torch`만 icon이 할당되어 있고, 다수의 ItemData asset은 `icon: {fileID: 0}`으로 비어 있음을 확인.

**남은 TODO/이슈:** Unity 컴파일 및 PlayMode에서 실제 아이템 획득 후 아이콘이 표시되는지 확인 필요. icon 필드가 비어 있는 아이템들은 코드가 정상이어도 아이콘이 표시되지 않으므로 데이터 할당 필요.

### 13. 장착 도구 내구도 파손 및 구독 관리 연결

**파일:** `Assets/02_Scripts/03_Entity/Player/PlayerInventory.cs`, `Assets/02_Scripts/03_Entity/Player/PlayerFirstPersonCameraController.cs`, `Assets/02_Scripts/04_Item/Item.cs`, `Assets/02_Scripts/04_Item/ItemData/ItemData.cs`, `Assets/02_Scripts/04_Item/ItemData/ItemData_ResourceItem.cs`, `Assets/02_Scripts/04_Item/SO/IEquipable.cs`, `Assets/02_Scripts/04_Item/SO/Item_SurvivalTool.cs`, `Assets/02_Scripts/04_Item/SO/Item_CombatGear.cs`, `Assets/02_Scripts/04_Item/SO/Item_Resource.cs`, `Assets/02_Scripts/04_Item/SO/Item_Booty.cs`, `docs/WorkSummary.md`

- `IEquipable`에 `IsUsable` bool 프로퍼티와 `OnBroken` 이벤트를 추가해, 내구도 0 도달 시 아이템이 직접 `IsUsable = false`로 파손 상태를 발행하도록 변경
- `Item_SurvivalTool`과 `Item_CombatGear`가 내구도 감소 후 파손 상태만 알리고, 실제 장착 슬롯 제거와 장착 프리팹 정리는 플레이어 쪽 흐름에서 처리되도록 변경
- `PlayerFirstPersonCameraController`가 기존 `PlayerInventory.OnEquippedItemChanged` 이벤트 체인에서 장착 프리팹을 생성한 뒤 실제 `IEquipable` 인스턴스를 `PlayerInventory`에 등록하고, 해제 시 등록을 풀도록 연결
- `PlayerInventory`가 장착 인스턴스의 `OnBroken`을 구독/해제하고, 파손 이벤트를 받으면 해당 장착 슬롯을 반환 없이 비워 인벤토리/UI/장착 뷰가 함께 갱신되도록 구현
- 도구 사용 레이캐스트가 자원 노드에 데미지를 적용한 뒤 장착 도구의 내구도를 감소시키도록 연결
- 인벤토리 슬롯과 월드 아이템 흐름이 `ItemDataSO` 기준으로 맞도록 `Item` 및 아이템 파생 컴포넌트의 상속 관계를 정리

**변경 이유:** 장착 프리팹의 내구도 파손을 아이템 내부 `Destroy()`로 처리하면 플레이어 인벤토리의 장착 상태, UI, 1인칭 장착 뷰가 서로 어긋날 수 있어, 아이템은 파손 상태만 알리고 플레이어 인벤토리가 장착 해제/파괴 흐름을 책임지게 하기 위함.

**검증:** `dotnet build .\Assembly-CSharp.csproj --no-restore` 실행 결과 이번 아이템/인벤토리 변경으로 인한 컴파일 오류는 해소됨. 최종 빌드는 기존 `Firebase.Analytics` 네임스페이스 참조 오류 2건(`LockResolver.cs`, `AnalyticsSDK_Firebase.cs`)으로 실패. `uloop compile`은 현재 PowerShell 세션에서 `uloop` 명령을 찾을 수 없어 실행하지 못함.

**남은 TODO/이슈:** Unity Editor에서 장착 도구 사용 후 내구도가 0이 되는 순간 손 슬롯이 비워지고 장착 프리팹이 제거되는지 PlayMode 확인 필요. 기존 Firebase Analytics 참조 문제 해결 후 전체 빌드 재확인 필요.

### 14. 변경된 런타임 아이템 구조 기준 파손 처리 재연결

**파일:** `Assets/02_Scripts/04_Item/SO/Item_SurvivalTool.cs`, `Assets/02_Scripts/04_Item/SO/Item_CombatGear.cs`, `Assets/02_Scripts/03_Entity/Player/PlayerFirstPersonCameraController.cs`, `Assets/02_Scripts/03_Entity/Player/PlayerInventory.cs`, `Assets/02_Scripts/04_Item/ItemData/ItemData_ResourceItem.cs`, `docs/WorkSummary.md`

- `Item_SurvivalTool`과 `Item_CombatGear`가 변경된 런타임 `ItemData` 구조에서 `IEquipable`의 `OnBroken`, `IsUsable`, `ItemData`를 직접 구현하도록 보정
- 내구도 사용 로직이 내구도 0 도달 시 `IsUsable = false`를 통해 파손 이벤트를 발행하고, 중복 파손 알림은 방지하도록 정리
- 1인칭 장착 프리팹 초기화 후 `Item` 컴포넌트가 아니라 `item.itemData as IEquipable` 런타임 인스턴스를 `PlayerInventory`에 등록하도록 변경
- 월드 아이템 줍기 흐름이 변경된 `Item` 구조에 맞게 `ItemDataSO`와 런타임 `IStackable` 수량을 사용하도록 수정
- `ItemData_ResourceItem.stackCount`의 불필요한 `new` 키워드를 제거해 현재 `ItemData` 베이스 클래스와 맞춤

**변경 이유:** 유진 작업 이후 장착 가능 동작이 `Item` MonoBehaviour가 아니라 런타임 `ItemData` 인스턴스 쪽에 위치하게 되어, 파손 구독 대상과 내구도 상태 변경 흐름도 실제 장착 인스턴스 기준으로 다시 연결해야 했다.

**검증:** `dotnet build .\Assembly-CSharp.csproj --no-restore` 실행 결과 아이템 파손 처리와 변경된 아이템 구조 관련 컴파일 오류는 해소됨. 전체 빌드는 기존 `Firebase.Analytics` 네임스페이스 참조 오류 2건(`LockResolver.cs`, `AnalyticsSDK_Firebase.cs`)으로 실패. `git diff --check`는 통과했으며 줄바꿈 변환 경고만 확인됨.

**남은 TODO/이슈:** Unity PlayMode에서 장착 도구 내구도가 0이 되는 순간 손 슬롯이 비워지고 장착 프리팹이 제거되는지 확인 필요. 전체 빌드 통과를 위해서는 기존 Firebase Analytics 참조 문제 해결이 별도로 필요.

### 15. 아이템 prefab의 런타임 데이터 컴포넌트 참조 제거

**파일:** `Assets/02_Scripts/04_Item/Item.cs`, `Assets/03_Prefabs/Item_Prefabs/DroppedItem_Base.prefab`, `Assets/03_Prefabs/Item_Prefabs/Item/Item_SurvivalTools/*.prefab`, `Assets/03_Prefabs/Item_Prefabs/Item/Item_CombatGears/*.prefab`, `Assets/03_Prefabs/Item_Prefabs/Item/Item_Resources/*.prefab`, `Assets/03_Prefabs/Item_Prefabs/Item/Item_Booty/*.prefab`, `Assets/03_Prefabs/Item_Prefabs/Item/Item_Food/*.prefab`, `docs/WorkSummary.md`

- `Item_SurvivalTool`, `Item_CombatGear`, `Item_Resource`, `Item_Booty`, `ItemData_Food`, `ItemData_ResourceItem`가 더 이상 `MonoBehaviour`가 아닌 런타임 데이터 클래스이므로, 해당 스크립트 GUID를 물고 있던 아이템 prefab 컴포넌트를 공통 `Item` 컴포넌트로 교체
- 기존 prefab의 `itemData` 참조는 `Item._itemSO`로 옮겨 `ItemDataSO` 연결을 보존
- 자원/전리품/음식 prefab의 기존 `stackCount` 값은 공통 `Item`의 직렬화 필드로 보존
- `Item`이 초기화 시 직렬화된 `stackCount`를 사용해 `ItemData.CreateFromSO(_itemSO, stackCount)`를 호출하도록 수정
- `DroppedItem_Base.prefab`도 더 이상 런타임 데이터 스크립트를 컴포넌트로 참조하지 않도록 공통 `Item` 컴포넌트로 변경

**변경 이유:** Unity는 prefab의 `MonoBehaviour` 컴포넌트를 복원할 때 참조된 클래스가 `MonoBehaviour`/네이티브 확장 타입이 아니면 `'... is missing the class attribute 'ExtensionOfNativeClass'!'` 오류를 발생시킨다. 유진 작업 이후 아이템 타입별 클래스들이 런타임 데이터 클래스로 바뀌었는데 prefab에는 예전 컴포넌트 참조가 남아 있어 장착 prefab `Instantiate` 시점에 오류가 났다.

**검증:** `rg`로 prefab/scene/asset 안에 예전 런타임 데이터 클래스 GUID가 남아 있지 않음을 확인. `dotnet build .\Assembly-CSharp.csproj --no-restore`는 이번 prefab/Item 변경 관련 오류 없이 진행되었고, 기존 `Firebase.Analytics` 네임스페이스 참조 오류 2건(`LockResolver.cs`, `AnalyticsSDK_Firebase.cs`)으로 최종 실패.

**남은 TODO/이슈:** Unity Editor PlayMode에서 장착 도구 prefab을 다시 생성해 `ExtensionOfNativeClass` 오류가 사라졌는지 확인 필요. 기존 Firebase Analytics 참조 문제는 별도 해결 필요.

### 16. 장착 토치 조명 카메라 표시 보정

**파일:** `Assets/02_Scripts/03_Entity/Player/PlayerFirstPersonCameraController.cs`, `docs/WorkSummary.md`

- 장착 도구를 `ViewModel` 레이어로 옮길 때 `Light` 컴포넌트가 붙은 오브젝트는 `Default` 레이어에 남기도록 분리
- 장착 토치일 때 자식 `Light`를 활성화하고, 색상/강도/range를 플레이어 시야용 최소값으로 보강
- 토치 조명의 `cullingMask`를 전체 레이어로 열어 월드 오브젝트와 ViewModel 양쪽에 영향을 줄 수 있도록 설정
- `equippedLightLayerName`, `equippedTorchLightIntensity`, `equippedTorchLightRange`, `equippedTorchLightColor` 직렬화 필드를 추가해 Inspector에서 손전등 느낌을 조절할 수 있게 함

**변경 이유:** 기존 장착 흐름은 토치 prefab 전체를 `ViewModel` 레이어로 바꾸고 기본 카메라에서는 `ViewModel`을 제외했기 때문에, 토치의 실제 `Light`도 월드 카메라 쪽 조명 후보에서 빠질 수 있었다. 또한 prefab 원본 조명 range가 매우 작아 손에 들었을 때 플레이어 시야에서 주변을 밝히는 느낌이 약했다.

**검증:** `dotnet build .\Assembly-CSharp.csproj --no-restore` 실행 결과 이번 토치 조명 변경 관련 컴파일 오류는 없었고, 기존 `Firebase.Analytics` 네임스페이스 참조 오류 2건(`LockResolver.cs`, `AnalyticsSDK_Firebase.cs`)으로 최종 실패.

**남은 TODO/이슈:** Unity PlayMode에서 토치를 장착한 뒤 Game View 기준으로 토치 메시와 주변 월드 조명이 보이는지 직접 확인 필요. 밝기가 과하면 `PlayerFirstPersonCameraController`의 토치 조명 직렬화 값을 조정하면 됨.
