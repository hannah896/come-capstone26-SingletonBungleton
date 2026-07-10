#if PHOTON_FUSION
using System;
using Fusion;
using UnityEngine;

/// <summary>
/// Fusion Input System을 통해 동기화되는 입력 데이터 구조체.
/// CommandManager에서 수집한 명령을 네트워크를 통해 전달합니다.
/// </summary>
public struct NetworkInputData : INetworkInput
{
    // 명령 타입
    public CommandType CommandType;

    // 명령 플래그
    public CommandFlags CommandFlags;

    // 목표 좌표
    public Vector2 TargetPos;

    // 추가 정수 파라미터
    public int IntParam;

    // 추가 실수 파라미터
    public float FloatParam;

    // 대상 ID들 (최대 8개, IL Weaver 호환을 위해 비제네릭 구조체 사용)
    public NetworkTargetIds TargetIds;

    // 로컬 캐릭터 상태 보고 (Host 모드: 클라 로컬 시뮬레이션 결과를 호스트가 확정)
    public Vector3 CharacterPosition;
    public float CharacterYaw;
    public NetworkBool HasCharacterState;

    // 유효한 명령이 포함되어 있는지 여부
    public bool HasCommand => CommandType != CommandType.None;
}

/// <summary>
/// 네트워크 입력용 고정 크기 int 배열.
/// Fusion IL Weaver가 제네릭 구조체를 직렬화할 수 없으므로, 비제네릭으로 구현합니다.
/// </summary>
public struct NetworkTargetIds : INetworkStruct
{
    public int Item0, Item1, Item2, Item3, Item4, Item5, Item6, Item7;
    public byte Count;

    public const int MaxLength = 8;

    public int this[int index]
    {
        get => index switch
        {
            0 => Item0, 1 => Item1, 2 => Item2, 3 => Item3,
            4 => Item4, 5 => Item5, 6 => Item6, 7 => Item7,
            _ => throw new IndexOutOfRangeException()
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
                default: throw new IndexOutOfRangeException();
            }
        }
    }

    /// <summary>
    /// FixedArray8로부터 변환합니다.
    /// </summary>
    public static NetworkTargetIds FromFixedArray(FixedArray8<int> source)
    {
        var result = new NetworkTargetIds { Count = source.Count };
        for (int i = 0; i < source.Count; i++)
        {
            result[i] = source[i];
        }
        return result;
    }

    /// <summary>
    /// FixedArray8로 변환합니다.
    /// </summary>
    public FixedArray8<int> ToFixedArray()
    {
        var result = new FixedArray8<int>();
        for (int i = 0; i < Count; i++)
        {
            result.TryAdd(this[i]);
        }
        return result;
    }
}
#endif
