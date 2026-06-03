# Player 시스템 구조도

## 전체 데이터 흐름

```
Unity Input System (.inputactions)
        │
        ▼
┌─────────────────────────┐
│ InputActions_Player     │  콜백 수신 → 데이터 기록
│ InputHandler            │
└────────┬────────────────┘
         │ Write
         ▼
┌─────────────────────────┐
│ PlayerInputData         │  순수 데이터 (MoveInput, JumpPressed 등)
└────────┬────────────────┘
         │ Read
         ▼
┌─────────────────────────┐
│ StateMachine (HFSM)     │  입력 읽기 → 상태 전환 판단 → Motor에 의도 전달
│  Root → Sub             │
└────────┬────────────────┘
         │ SetHorizontalVelocity, RotateToward, SetVerticalVelocity
         ▼
┌─────────────────────────┐
│ PlayerMotor             │  CharacterController 기반 최종 이동 수행
│  ├ PlayerGroundDetector │  지면/경사면 감지
│  └ PlayerGravity        │  중력 시뮬레이션
└─────────────────────────┘
```

## Player.cs (중앙 허브)

오케스트레이터 역할. 컴포넌트를 소유하고 실행 순서를 제어한다.

```
Player (MonoBehaviour)
  ├── PlayerRootStateMachine  machine     # 상태 머신
  ├── Animator                animator    # 애니메이션
  ├── PlayerMotor             motor       # 이동/물리
  ├── PlayerStatus            stat        # 스탯 (런타임)
  └── PlayerInputData         inputData   # 입력 데이터
```

### 실행 순서 (매 프레임)

```
OnLoopUpdate(deltaTime)
  1. machine.OnUpdate()        ← State가 입력 읽고 Motor에 의도 전달
  2. motor.Tick()              ← 지면감지 → 중력 → 경사보정 → CC.Move
  3. inputData.ConsumeEventInputs()  ← 이벤트 입력 플래그 리셋

OnLoopGameUpdate(deltaTime)
  1. machine.OnGameUpdate()    ← GameSpeed 적용된 업데이트
```

---

## 컴포넌트 상세

### 1. InputActions_PlayerInputHandler

| 항목 | 내용 |
|------|------|
| **위치** | `Player/PlayerInputHandler.cs` |
| **부모** | `InputActions` (추상 클래스) |
| **역할** | Unity Input System 콜백 → `PlayerInputData`에 값 기록 |
| **원칙** | 이동 로직 없음. 데이터 기록만 담당 |

**바인딩하는 입력:**

| 입력 | 타입 | 콜백 |
|------|------|------|
| Move | 연속 (Vector2) | performed → MoveInput, canceled → zero |
| Look | 연속 (Vector2) | performed → LookInput, canceled → zero |
| Sprint | 연속 (bool) | performed → true, canceled → false |
| Jump | 이벤트 | performed → JumpPressed = true |
| Attack | 이벤트 | performed → AttackPressed = true |
| Crouch | 이벤트 | performed → CrouchPressed = true |
| Interact | 이벤트 | performed → InteractPressed = true |

**구현해야 할 기능:**

- [ ] Dodge/Roll 입력 바인딩
- [ ] 인벤토리 토글 입력 (Tab 등)
- [ ] 퀵슬롯 입력 (1~9 숫자키)
- [ ] ESC/Pause 입력
- [ ] 입력 리바인딩 지원 (키 재설정)

---

### 2. PlayerInputData

| 항목 | 내용 |
|------|------|
| **위치** | `Player/PlayerInputData.cs` |
| **역할** | 순수 데이터 클래스. InputHandler가 쓰고, State가 읽는다 |

**현재 필드:**

| 분류 | 필드 | 설명 |
|------|------|------|
| 연속 | `MoveInput` (Vector2) | WASD / 스틱 |
| 연속 | `LookInput` (Vector2) | 마우스 델타 |
| 연속 | `SprintHeld` (bool) | Shift 홀드 |
| 이벤트 | `JumpPressed` | 한 프레임만 true |
| 이벤트 | `AttackPressed` | 한 프레임만 true |
| 이벤트 | `CrouchPressed` | 한 프레임만 true |
| 이벤트 | `InteractPressed` | 한 프레임만 true |
| 파생 | `HasMoveInput` | sqrMagnitude > 0.01f |

**구현해야 할 기능:**

- [ ] `DodgePressed` 이벤트 입력 추가
- [ ] `InventoryTogglePressed` 이벤트 입력 추가
- [ ] `QuickSlotIndex` (int) — 퀵슬롯 번호 입력
- [ ] 입력 버퍼링 (점프/공격 등 선입력)

---

### 3. PlayerMotor

| 항목 | 내용 |
|------|------|
| **위치** | `Player/PlayerMotor.cs` |
| **부모** | `MonoBehaviour` + `RequireComponent(CharacterController)` |
| **역할** | 수평 이동, 수직 이동(중력/점프), 회전을 최종 수행 |
| **원칙** | 입력을 모름. State가 전달한 velocity만 실행 |

**외부 API (State가 호출):**

| 메서드 | 설명 |
|--------|------|
| `SetHorizontalVelocity(dir, speed)` | 수평 이동 속도 설정 |
| `SetVerticalVelocity(vy)` | 수직 속도 직접 설정 (점프) |
| `SetGravityEnabled(bool)` | 중력 ON/OFF (사다리, 수영) |
| `RotateToward(dir, speed, dt)` | 이동 방향으로 부드러운 회전 |

**Tick() 처리 순서:**

```
1. groundDetector.Update()     — 지면 감지 (SphereCast)
2. gravity.Update()            — 중력 가속/지면 밀착
3. 경사면 보정                  — ProjectOnSlope
4. 가파른 경사 미끄러짐          — SteepSlope slide
5. 수직 속도 합산               — finalVelocity.y = gravity
6. cc.Move(finalVelocity * dt) — CharacterController 이동
7. 회전 적용                    — Slerp 회전
8. moveVelocity 초기화          — 다음 프레임 대비
```

**구현해야 할 기능:**

- [ ] 넉백(Knockback) 처리 — 외부 힘 적용 API (`AddForce` or `ApplyKnockback`)
- [ ] 대시(Dash) 속도 처리 — 순간 가속 API
- [ ] 수영/사다리 등 특수 이동 모드
- [ ] 스냅 이동 (낙하 시 지면 스냅)
- [ ] CC 충돌 콜백 (`OnControllerColliderHit`) 활용

---

### 4. PlayerGroundDetector

| 항목 | 내용 |
|------|------|
| **위치** | `Player/PlayerGroundDetector.cs` |
| **역할** | SphereCast 기반 지면/경사면 감지 |

**제공 정보:**

| 프로퍼티 | 설명 |
|----------|------|
| `IsGrounded` | 현재 지면 접촉 여부 |
| `WasGroundedRecently` | 코요테 타임 적용 (0.15초) |
| `SlopeAngle` | 현재 경사 각도 |
| `IsOnSlope` | 경사면 위 (0.1° ~ 45°) |
| `IsSteepSlope` | 가파른 경사면 (> 45°) |
| `GroundNormal` | 지면 법선 벡터 |

**구현해야 할 기능:**

- [ ] 지면 머티리얼 감지 (발소리 변경용)
- [ ] 물/용암 등 특수 지면 판별
- [ ] 엣지 감지 (절벽 가장자리 판별)

---

### 5. PlayerGravity

| 항목 | 내용 |
|------|------|
| **위치** | `Player/PlayerGravity.cs` |
| **역할** | Rigidbody 없이 중력 시뮬레이션 |

**설정값:**

| 값 | 기본 | 설명 |
|----|------|------|
| `gravityAcceleration` | -20 | 중력 가속도 |
| `maxFallSpeed` | -40 | 최대 낙하 속도 |
| `groundedPullDown` | -2 | 지면 밀착용 미세 음수 |

**구현해야 할 기능:**

- [ ] 가변 점프 높이 (버튼 짧게/길게 누르기에 따른 중력 배율)
- [ ] 낙하 가속 보정 (하강 시 더 빠르게 — 게임 느낌 개선)
- [ ] 공중 체류 시간 (Hang time) 지원

---

### 7. PlayerStatus

| 항목 | 내용 |
|------|------|
| **위치** | `Player/PlayerStat/PlayerStatus.cs` |
| **역할** | 런타임 스탯 관리. `PlayerStatData`(SO)에서 초기값 로드 |

**스탯 분류:**

| 카테고리 | 필드 | 설명 |
|----------|------|------|
| **체력** | MaxHp, CurrentHp | HP |
| **전투** | Attack, Defense, MinAttackPeriod | 공격/방어 |
| **이동** | MoveSpeed, SprintMultiplier, JumpForce | 이동 관련 |
| **허기** | MaxHunger, CurrentHunger, HungerDrain, HungerHPDecreaseRate | 배고픔 |
| **Ego** | MaxEgo, CurrentEgo, EgoDecreaseRate | Ego |
| **체온** | Temperature, ColdHPDecreaseRate, HotHPDecreaseRate, MaxCold/HotTemperature | 체온 |
| **습도** | Wetness, MaxWetness, WetnessTemperatureDecreaseRate, WaterproofRate | 젖음 |

**구현해야 할 기능:**

- [ ] 스탯 변경 이벤트 시스템 (`OnHpChanged`, `OnHungerChanged` 등) — UI 바인딩용
- [ ] 허기 자동 감소 로직 (시간 경과 시 HungerDrain)
- [ ] 허기 0일 때 HP 자동 감소
- [ ] Ego 감소 로직 (밤, 몬스터 근처 등)
- [ ] 체온 시스템 — 환경 온도에 따른 변화, 극한 온도 시 HP 감소
- [ ] 습도 시스템 — 비/물 접촉 시 습도 증가, 습도에 따른 체온 하락
- [ ] 회복 메서드 (`Heal`, `Eat`, `Rest` 등)
- [ ] 버프/디버프 시스템 (장비, 음식, 환경 효과로 스탯 변동)
- [ ] 사망 판정 후 부활 처리

---

### 8. PlayerAnimData / PlayerAnimHashKey

| 항목 | 내용 |
|------|------|
| **위치** | `Player/PlayerAnimData.cs`, `Player/PlayerAnimHashKey.cs` |
| **역할** | 애니메이션 해시 캐싱 + 재생 헬퍼 |

**등록된 애니메이션 해시:**

| 분류 | 해시 |
|------|------|
| Locomotion | idle, walk, trace, run |
| Action | pick, mine, chop, dig, ignite, cook, inspect, build |
| Combat/Special | attack, sleep, dead, hurt |

**구현해야 할 기능:**

- [ ] 각 State의 `OnEnter()`에서 `PlayLocomotionAnimation()` 호출 연결
- [ ] 블렌드 트리 파라미터 설정 (Speed 파라미터로 Walk↔Run 블렌딩)
- [ ] 애니메이션 이벤트 콜백 (공격 히트 타이밍, 발소리 등)
- [ ] 상체/하체 레이어 분리 (이동하면서 공격)
- [ ] IK 처리 (아이템 잡기, 지면 적응)

---

## 상태 머신 (HFSM) 구조

```
PlayerRootStateMachine
│
├── PlayerLocomotionState (Root) ─── 일반 이동
│   ├── PlayerIdleState (Sub)        정지
│   ├── PlayerWalkState (Sub)        걷기 (MoveSpeed)
│   ├── PlayerRunState (Sub)         달리기 (MoveSpeed × SprintMultiplier)
│   └── PlayerAirState (Sub)         공중 (airControlFactor = 0.5)
│
├── PlayerActionState (Root) ─────── 상호작용 행동
│   ├── PlayerPickState (Sub)        채집
│   ├── PlayerMineState (Sub)        채광
│   ├── PlayerChopState (Sub)        벌목
│   ├── PlayerDigState (Sub)         땅파기
│   ├── PlayerIgniteState (Sub)      불붙이기
│   ├── PlayerCookState (Sub)        요리
│   ├── PlayerInspectState (Sub)     조사
│   └── PlayerBuildState (Sub)       건설
│
├── PlayerAttackState (Root) ─────── 공격
├── PlayerHurtState (Root) ────────── 피격
├── PlayerDeadState (Root) ────────── 사망
└── PlayerSleepState (Root) ───────── 수면
```

### 상태 전환 규칙 (현재 구현됨)

```
Locomotion:
  Idle ──(HasMoveInput)──→ Walk
  Idle ──(HasMoveInput + SprintHeld)──→ Run
  Walk ──(!HasMoveInput)──→ Idle
  Walk ──(SprintHeld)──→ Run
  Run  ──(!HasMoveInput)──→ Idle
  Run  ──(!SprintHeld)──→ Walk
  Any  ──(JumpPressed + WasGroundedRecently)──→ Air
  Any  ──(!IsGrounded && !WasGroundedRecently)──→ Air (낙하)
  Air  ──(IsGrounded + VerticalVelocity ≤ 0)──→ Idle

Root 전환:
  Locomotion ──(AttackPressed)──→ Attack
  Attack ──(애니메이션 완료)──→ Locomotion  (TODO)
  Hurt ──(IsDead)──→ Dead
  Hurt ──(!IsDead)──→ Locomotion
```

### 구현해야 할 상태 전환

- [ ] Locomotion → Action (Interact 입력 + 상호작용 가능 오브젝트 근처)
- [ ] Action → Locomotion (행동 애니메이션 완료)
- [ ] Locomotion → Hurt (외부에서 데미지 수신 시)
- [ ] Locomotion → Sleep (수면 오브젝트 상호작용)
- [ ] Sleep → Locomotion (수면 완료 / 중단 입력)
- [ ] Attack 콤보 시스템 (Attack → Attack 연계)
- [ ] Dodge/Roll 상태 추가 (무적 프레임 포함)
- [ ] 공중 공격 상태

---

## 파일 구조

```
Assets/02_Scripts/03_Entity/Player/
├── Player.cs                          # 중앙 허브 (오케스트레이터)
├── PlayerMotor.cs                     # 이동/물리 실행
├── PlayerGroundDetector.cs            # 지면 감지
├── PlayerGravity.cs                   # 중력 시뮬레이션
├── PlayerInputHandler.cs              # 입력 콜백 → 데이터 기록
├── PlayerInputData.cs                 # 입력 데이터
├── PlayerAnimData.cs                  # 애니메이션 헬퍼
├── PlayerAnimHashKey.cs               # 애니메이션 해시 캐싱
├── PlayerRootStateMachine.cs          # HFSM 루트
├── PlayerRootStateBase.cs             # Root 상태 베이스
├── PlayerSubStateBase.cs              # Sub 상태 베이스
│
├── PlayerStat/
│   ├── PlayerStatData.cs              # ScriptableObject (초기값)
│   └── PlayerStatus.cs                # 런타임 스탯
│
└── PlayerState/
    ├── PlayerIdleState.cs             # Idle (Sub)
    ├── Root/
    │   ├── PlayerGroundState.cs       # = PlayerLocomotionState
    │   ├── PlayerActionState.cs       # 상호작용 (Root)
    │   ├── PlayerAttackState.cs       # 공격 (Root)
    │   ├── PlayerHurtState.cs         # 피격 (Root)
    │   ├── PlayerDeadState.cs         # 사망 (Root)
    │   └── PlayerSleepState.cs        # 수면 (Root)
    ├── Locomotion/
    │   └── PlayerAirState.cs          # 공중 (Sub)
    ├── Sub/Ground/
    │   ├── PlayerWalkState.cs         # 걷기 (Sub)
    │   └── PlayerRunState.cs          # 달리기 (Sub)
    └── Action/
        ├── PlayerPickState.cs         # 채집
        ├── PlayerMineState.cs         # 채광
        ├── PlayerChopState.cs         # 벌목
        ├── PlayerDigState.cs          # 땅파기
        ├── PlayerIgniteState.cs       # 불붙이기
        ├── PlayerCookState.cs         # 요리
        ├── PlayerInspectState.cs      # 조사
        └── PlayerBuildState.cs        # 건설
```

---

## 우선순위별 구현 로드맵

### P0 — 핵심 (게임 루프에 필수)

| 기능 | 담당 컴포넌트 | 설명 |
|------|--------------|------|
| 애니메이션 연결 | AnimData + 각 State | 각 상태 진입 시 애니메이션 재생 |
| 공격 시스템 완성 | AttackState + Motor | 히트 판정, 애니메이션 완료 감지, Locomotion 복귀 |
| 피격 → 사망 흐름 | HurtState + Status | 외부 데미지 수신, HP 감소, Dead 전환 |
| 허기 시스템 | Status | 시간 경과에 따른 자동 감소, 0일 때 HP 감소 |
| 상호작용 진입 | Locomotion → Action | Interact 입력 시 근처 오브젝트 판별 후 Action 전환 |

### P1 — 게임플레이 확장

| 기능 | 담당 컴포넌트 | 설명 |
|------|--------------|------|
| 넉백 처리 | Motor | 외부 힘 적용 (피격, 폭발) |
| 스탯 이벤트 시스템 | Status | HP/허기/Ego 변화 이벤트 → UI 바인딩 |
| 카메라 충돌 | CameraController | 벽 뒤 카메라 방지 |
| 체온/습도 시스템 | Status | 환경 요소와 스탯 연동 |
| Ego 시스템 | Status | 밤/몬스터 근처 감소 |

### P2 — 게임 느낌 개선

| 기능 | 담당 컴포넌트 | 설명 |
|------|--------------|------|
| 가변 점프 높이 | Gravity | 버튼 유지 시간에 따른 높이 변화 |
| 입력 버퍼링 | InputData | 선입력 지원 (착지 직전 점프 등) |
| 카메라 흔들림 | CameraController | 피격/폭발 시 화면 진동 |
| 공격 콤보 | AttackState | 연속 공격 체인 |
| Dodge/Roll | 새 상태 추가 | 회피 + 무적 프레임 |

### P3 — 폴리싱

| 기능 | 담당 컴포넌트 | 설명 |
|------|--------------|------|
| 발소리 / 지면 머티리얼 | GroundDetector | 지면 종류별 사운드 |
| 상체/하체 애니메이션 분리 | AnimData | 이동 + 공격 동시 |
| IK | AnimData | 아이템 잡기, 발 지면 적응 |
| FOV 변화 | CameraController | 달리기 시 시야 확대 |
| Lock-on 카메라 | CameraController | 전투 시 적 고정 |
| 키 리바인딩 | InputHandler | 사용자 키 설정 |
