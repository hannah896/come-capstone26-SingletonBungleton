#if PHOTON_FUSION
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

    // 대상 ID들 (최대 8개)
    public FixedArray8<int> TargetIds;

    // 유효한 명령이 포함되어 있는지 여부
    public bool HasCommand => CommandType != CommandType.None;
}
#endif
