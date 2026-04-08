# 플레이어 애니메이션 전환 구조

## 개요

플레이어 애니메이션은 **HFSM(계층적 상태 머신)** + **Animator Bool 파라미터** 조합으로 제어됩니다.  
코드(HFSM)가 상태를 결정하고, Animator는 Bool 조건에 따라 애니메이션을 전환합니다.

---

## 전환 흐름 요약

```
입력 (InputSystem)
    ↓
PlayerXxxState.Update()   ← HFSM이 상태 판단
    ↓
PlayLocomotionAnimation() ← Bool 파라미터 조작
    ↓
Animator Controller        ← Bool 조건으로 애니메이션 전환
```

---

## 코드 전환 위치

### 1. 애니메이션 Bool 조작 — `PlayerAnimData.cs`

```csharp
// 상태 진입 시: 모든 로코모션 Bool 리셋 → 대상만 true
public void PlayLocomotionAnimation(int animHash)
{
    ResetLocomotionBools();          // Idle, Walk, Run, Jump, Trace 전부 false
    animator.SetBool(animHash, true); // 대상 파라미터만 true
}

// 상태 퇴장 시: 해당 Bool을 false
public void StopAnimation(int animHash)
{
    animator.SetBool(animHash, false);
}
```

### 2. 파라미터 Hash 정의 — `PlayerAnimHashKey.cs`

| 프로퍼티 | Animator 파라미터명 | 타입 |
|---------|------------------|------|
| `Idle`  | `"Idle"`         | Bool |
| `Walk`  | `"Walk"`         | Bool |
| `Run`   | `"Run"`          | Bool |
| `Jump`  | `"Jump"`         | Bool |
| `Trace` | `"Trace"`        | Bool |

### 3. 상태별 호출 위치

| 파일 | OnEnter (진입) | OnExit (퇴장) |
|------|--------------|-------------|
| `PlayerIdleState.cs` | `PlayLocomotionAnimation(Idle)` | `StopAnimation(Idle)` |
| `PlayerWalkState.cs` | `PlayLocomotionAnimation(Walk)` | `StopAnimation(Walk)` |
| `PlayerRunState.cs`  | `PlayLocomotionAnimation(Run)`  | `StopAnimation(Run)`  |
| `PlayerAirState.cs`  | `PlayLocomotionAnimation(Jump)` | `StopAnimation(Jump)` |
| `PlayerTraceState.cs`| `PlayLocomotionAnimation(Trace)`| `StopAnimation(Trace)`|

---

## HFSM 상태 전환 조건 (코드)

### Idle → Walk / Run
```
// PlayerIdleState.Update()
if (Input.HasMoveInput)
    if (Input.SprintHeld) → ground.ChangeToRun()
    else                  → ground.ChangeToWalk()
```

### Walk → Idle / Run
```
// PlayerWalkState.Update()
if (!Input.HasMoveInput) → ground.ChangeToIdle()
if (Input.SprintHeld)    → ground.ChangeToRun()
```

### Run → Idle / Walk
```
// PlayerRunState.Update()
if (!Input.HasMoveInput) → ground.ChangeToIdle()
if (!Input.SprintHeld)   → ground.ChangeToWalk()
```

### Air → Idle (착지)
```
// PlayerAirState.Update()
if (Motor.IsGrounded && Motor.VerticalVelocity <= 0f)
    → locomotion.ChangeToIdle()
```

---

## Animator Controller 전환 구조 (FemalePlayer.controller)

`PlayerLocomotionState` 서브 스테이트 머신 내부:

```
Entry ──────────────────────────────→ Idle (기본 진입)

Idle    ──[Walk=true]──→ Walk
Idle    ──[Run=true]───→ Run
Idle    ──[Jump=true]──→ Jumping

Walk    ──[Idle=true]──→ Idle
Walk    ──[Run=true]───→ Run
Walk    ──[Jump=true]──→ Jumping

Run     ──[Idle=true]──→ Idle
Run     ──[Walk=true]──→ Walk
Run     ──[Jump=true]──→ Jumping

Jumping ──[Idle=true]──→ Idle
Jumping ──[Walk=true]──→ Walk
Jumping ──[Run=true]───→ Run
```

**모든 Transition 공통 설정:**
- `Has Exit Time`: false (즉각 전환)
- `Transition Duration`: 0.15s
- `Can Transition To Self`: false

---

## 새 애니메이션 상태 추가 방법

1. `PlayerAnimHashKey.cs` — 파라미터명 문자열과 Hash 프로퍼티 추가
2. `PlayerAnimData.cs` — `ResetLocomotionBools()`에 새 파라미터 추가
3. Unity Animator — Bool 파라미터 추가, 기존 상태들과 Transition 연결
4. 해당 State 클래스 — `OnEnter`에서 `PlayLocomotionAnimation`, `OnExit`에서 `StopAnimation` 호출
