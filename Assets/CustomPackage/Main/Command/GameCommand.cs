using System;
using UnityEngine;

/// <summary>
/// 플레이어의 단일 명령을 캡슐화한 구조체.
/// 네트워크 전송 및 로컬 실행의 기본 단위입니다.
/// </summary>
[Serializable]
public struct GameCommand
{
    /// <summary>
    /// 명령이 실행될 시뮬레이션 틱
    /// </summary>
    public uint Tick;

    /// <summary>
    /// 명령을 내린 플레이어 ID
    /// </summary>
    public ushort PlayerId;

    /// <summary>
    /// 명령 종류
    /// </summary>
    public CommandType Type;

    /// <summary>
    /// 명령 수정자 플래그
    /// </summary>
    public CommandFlags Flags;

    /// <summary>
    /// 목표 좌표 (이동, 건설 위치 등)
    /// </summary>
    public Vector2 TargetPos;

    /// <summary>
    /// 대상 오브젝트 ID들 (선택된 유닛, 공격 대상 등)
    /// </summary>
    public FixedArray8<int> TargetIds;

    /// <summary>
    /// 추가 정수 파라미터 (건물 타입, 스킬 ID 등)
    /// </summary>
    public int IntParam;

    /// <summary>
    /// 추가 실수 파라미터 (데미지 배율 등)
    /// </summary>
    public float FloatParam;

    /// <summary>
    /// 새로운 GameCommand를 생성합니다.
    /// </summary>
    public static GameCommand Create(
        CommandType type,
        ushort playerId,
        Vector2 targetPos = default,
        CommandFlags flags = CommandFlags.None,
        int intParam = 0)
    {
        return new GameCommand
        {
            Type = type,
            PlayerId = playerId,
            TargetPos = targetPos,
            Flags = flags,
            IntParam = intParam,
            TargetIds = new FixedArray8<int>()
        };
    }

    /// <summary>
    /// 대상 ID를 추가한 새로운 명령을 반환합니다.
    /// </summary>
    public GameCommand WithTargets(params int[] ids)
    {
        var copy = this;
        copy.TargetIds = FixedArray8<int>.FromArray(ids);
        return copy;
    }

    public override string ToString()
    {
        return $"[Tick:{Tick}] Player{PlayerId}: {Type} at {TargetPos} (Targets:{TargetIds.Count})";
    }
}

/// <summary>
/// 명령 종류를 정의하는 열거형.
/// 게임에서 플레이어가 수행할 수 있는 모든 액션을 포함합니다.
/// </summary>
public enum CommandType : byte
{
    None = 0,

    // === 유닛 기본 명령 ===
    Move = 1,           // 이동
    Stop = 2,           // 정지
    Hold = 3,           // 홀드 (위치 고수)
    Attack = 4,         // 공격
    AttackMove = 5,     // 공격 이동
    Patrol = 6,         // 정찰

    // === 건물 관련 ===
    Build = 10,         // 건물 건설
    CancelBuild = 11,   // 건설 취소
    Upgrade = 12,       // 업그레이드

    // === 유닛 생산 ===
    Train = 20,         // 유닛 생산
    CancelTrain = 21,   // 생산 취소
    SetRallyPoint = 22, // 랠리포인트 설정

    // === 스킬/능력 ===
    UseSkill = 30,      // 스킬 사용
    UseSkillOnTarget = 31, // 대상 지정 스킬
    UseSkillOnPosition = 32, // 위치 지정 스킬

    // === 자원/경제 ===
    Gather = 40,        // 자원 채집
    ReturnResource = 41, // 자원 반납

    // === 그룹 관련 ===
    SetGroup = 50,      // 컨트롤 그룹 지정
    SelectGroup = 51,   // 컨트롤 그룹 선택

    // === 기타 ===
    Surrender = 100,    // 항복
    Pause = 101,        // 일시정지 요청
}

/// <summary>
/// 명령 수정자 플래그.
/// 기본 명령에 추가적인 옵션을 적용합니다.
/// </summary>
[Flags]
public enum CommandFlags : byte
{
    None = 0,

    /// <summary>
    /// 명령을 대기열에 추가 (Shift+클릭)
    /// </summary>
    Queued = 1 << 0,

    /// <summary>
    /// 강제 실행 (아군 공격 등)
    /// </summary>
    Forced = 1 << 1,

    /// <summary>
    /// 즉시 실행 (대기열 무시)
    /// </summary>
    Instant = 1 << 2,

    /// <summary>
    /// 자동 공격 비활성화
    /// </summary>
    NoAutoAttack = 1 << 3,
}

/// <summary>
/// 고정 크기 배열 구조체.
/// 네트워크 직렬화에 유리하며 GC 할당을 방지합니다.
/// </summary>
[Serializable]
public struct FixedArray8<T> where T : unmanaged
{
    public T Item0, Item1, Item2, Item3, Item4, Item5, Item6, Item7;
    public byte Count;

    public const int MaxLength = 8;

    public T this[int index]
    {
        get => index switch
        {
            0 => Item0,
            1 => Item1,
            2 => Item2,
            3 => Item3,
            4 => Item4,
            5 => Item5,
            6 => Item6,
            7 => Item7,
            _ => throw new IndexOutOfRangeException($"Index {index} is out of range for FixedArray8")
        };
        set
        {
            switch (index)
            {
                case 0: Item0 = value; break;
                case 1: Item1 = value; break;
                case 2: Item2 = value; break;
                case 3: Item3 = value; break;
                case 4: Item4 = value; break;
                case 5: Item5 = value; break;
                case 6: Item6 = value; break;
                case 7: Item7 = value; break;
                default: throw new IndexOutOfRangeException($"Index {index} is out of range for FixedArray8");
            }
        }
    }

    /// <summary>
    /// 배열로부터 FixedArray8을 생성합니다.
    /// </summary>
    public static FixedArray8<T> FromArray(T[] array)
    {
        var result = new FixedArray8<T>();
        if (array == null) return result;

        result.Count = (byte)Math.Min(array.Length, MaxLength);
        for (int i = 0; i < result.Count; i++)
        {
            result[i] = array[i];
        }
        return result;
    }

    /// <summary>
    /// 요소를 추가합니다. 최대 8개까지 저장 가능합니다.
    /// </summary>
    public bool TryAdd(T item)
    {
        if (Count >= MaxLength) return false;
        this[Count] = item;
        Count++;
        return true;
    }

    /// <summary>
    /// 일반 배열로 변환합니다.
    /// </summary>
    public T[] ToArray()
    {
        var result = new T[Count];
        for (int i = 0; i < Count; i++)
        {
            result[i] = this[i];
        }
        return result;
    }
}
