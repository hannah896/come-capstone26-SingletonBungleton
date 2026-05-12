# AGENTS.md

This file provides guidance to Codex (Codex.ai/code) when working with code in this repository.


## Mandatory workflow rules

After completing ANY coding task, you MUST update:

docs/worksummary.md

The summary must include:
- What changed
- Files modified
- Why the change was made
- Remaining TODOs or issues

This rule is mandatory for every completed task.
Never skip updating worksummary.md.

## File Encoding Rules

ALL text files must be saved using UTF-8 encoding.

This includes:
- .cs
- .md
- .json
- .yaml
- .yml
- .txt
- .shader

Never save files using:
- UTF-16
- ANSI
- EUC-KR
- CP949

Always preserve existing UTF-8 encoding when modifying files.


## 프로젝트 개요

Unity 6 기반 캡스톤 프로젝트 (팀명: SingletonBungleton). 3D 액션/서바이벌 장르 게임으로, Photon Fusion 멀티플레이어, Addressables 에셋 관리, UniTask 비동기 처리를 사용합니다.

## 빌드 및 실행

- Unity Editor에서 직접 빌드/실행 (CLI 빌드 스크립트 없음)
- 렌더링: URP (Universal Render Pipeline)
- Input System: Unity New Input System (`com.unity.inputsystem`)

## Git 브랜치 전략

- `main`: 메인 브랜치
- `Dev`: 개발 통합 브랜치
- `Feat/{이니셜}`: 팀원별 기능 브랜치 (예: `Feat/PHN`, `Feat/JYJ`, `Feat/KKB`)

## 핵심 아키텍처

### 매니저 시스템 (`Main.cs`)

`Main`이 게임의 진입점이자 모든 매니저의 중앙 허브. `Initializer.cs`에서 `[RuntimeInitializeOnLoadMethod]`로 앱 시작 시 `Main.Instance`를 호출하여 초기화합니다.

매니저는 **3단계 초기화 순서**를 가짐 (각 단계 내에서는 병렬 초기화):
1. **PrimaryManager** — `ResourceManager`, `DataManager`, `UIManager`, `JSAMManager`
2. **CoreManager** — `PoolManager`, `ScreenManager`, `LoopManager`, `InputManager`, `NetworkManager` 등
3. **ContentManager** — `GameManager`, `SceneManagerEx`

모든 매니저는 `Managers` 추상 클래스를 상속하며, `OnInitializeAsync()`를 오버라이드하여 초기화 로직 구현. 매니저 접근은 `Main.Resource`, `Main.UI`, `Main.Loop` 등 정적 프로퍼티로.

### LoopManager (커스텀 업데이트 루프)

Unity의 `Update()`를 직접 사용하지 않고, `LoopManager`의 이벤트에 등록하는 방식:
- `OnUpdate` — 일반 프레임 업데이트
- `OnGameUpdate` — 게임 속도(`GameSpeed`)가 적용된 업데이트. `GameProcessing.Processing` 상태일 때만 동작
- `OnFixedUpdate` / `OnLateUpdate`

엔티티는 `OnEnable`에서 `Main.Loop.OnGameUpdate += ...`, `OnDisable`에서 해제하는 패턴을 따름.

### ResourceManager (Addressables 래퍼)

Addressables 에셋 로딩을 래핑하며, 두 단계 캐시 사용:
- `Required` — 명시적 해제 전까지 유지
- `NonRequired` — 씬 변경 시 해제 가능 (`Clear()` 호출 시)

### UI 시스템 (`UIManager`)

3개 레이어로 구성: `HudLayer` (sortOrder 10), `PopupLayer` (sortOrder 20), `ScreenLayer` (sortOrder 1000). 모든 UI는 Addressables로 프리팹 로드 후 인스턴스화.

- `UI` ← `UI_Hud` / `UI_Popup` / `UI_Screen` 계층 구조
- Popup은 스택 기반 관리, 클릭 가드(배경 딤) 지원

### StateMachine (FSM / HFSM)

- **FSM**: `StateMachine<T>` + `StateBase` — 단순 상태 전환
- **HFSM**: `RootStateMachine` + `SubStateMachine` — 계층적 상태 머신. Root 상태가 내부에 Sub 상태 머신을 가짐
- Player는 `PlayerRootStateMachine` → `PlayerRootStateBase` / `PlayerSubStateBase` 구조 사용

### 싱글톤 패턴 (`Singleton.cs`)

4가지 싱글톤 변형 제공:
- `Singleton<T>` — 일반 C# 클래스용
- `SingletonWithMono<T>` — MonoBehaviour용 (DontDestroyOnLoad)
- `SingletonWithScene<T>` — 씬 내 유지 (씬 전환 시 파괴)
- `ScriptableObjectSingleton<T>` — ScriptableObject용 (`Resources/ConfigData/` 경로)

## 프로젝트 폴더 구조

```
Assets/
  00_Externals/          # 외부 에셋 (수정 금지)
  01_Scenes/             # Unity 씬 파일
  02_Scripts/
    @Scripts/            # 핵심 게임 스크립트
      Managers/          # Main.cs 및 각종 매니저
      99_Utils/          # Define, Enums, Extensions, Initializer
      UI/                # UI 컴포넌트 (Base/, Components/, Popups/, Scene/)
      Models/            # 데이터 모델 (Entity, Board)
      Scenes/            # 씬별 초기화 스크립트
    01_UI/               # UI 유틸리티
    02_StateMachine/     # FSM, HFSM 프레임워크
    03_Entity/           # Player 등 엔티티
    04_Item/             # 아이템 시스템
    @@Test/              # 팀원별 테스트 코드
    Generic/             # Singleton 등 범용 클래스
  CustomPackage/         # 재사용 가능한 커스텀 패키지
    Main/                # 핵심 매니저 구현체 (ResourceManager, UIManager, LoopManager, NetworkManager 등)
  Photon/                # Photon Fusion 네트워크
  Plugins/               # DOTween 등 플러그인
```

## 주요 의존성

- **UniTask** (`com.cysharp.unitask`) — 비동기 처리 (`async UniTask`)
- **DOTween** — 트윈 애니메이션
- **Photon Fusion** — 멀티플레이어 네트워킹
- **Addressables** (`com.unity.addressables`) — 에셋 번들 관리
- **JSAM** (`com.brogrammist.jsam`) — 오디오 관리
- **Firebase** — Analytics, Auth, Crashlytics, Messaging, Remote Config
- **Newtonsoft JSON** (`com.unity.nuget.newtonsoft-json`)
- **Unity Localization** (`com.unity.localization`)

## 코드 컨벤션

- 한글 주석 사용
- 매니저 클래스는 `PrimaryManager`, `CoreManager`, `ContentManager` 중 하나를 상속
- 비동기 메서드는 UniTask 사용 (`async UniTask`, `UniTask<T>`)
- 상태 머신의 상태 클래스는 `OnEnter()`, `OnExit()`, `Update()`, `FixedUpdate()` 오버라이드
