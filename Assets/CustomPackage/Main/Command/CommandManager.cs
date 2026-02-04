using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 게임 명령의 생성, 검증, 라우팅을 담당하는 매니저.
/// 로컬/멀티플레이어 모드에 따라 명령의 흐름을 제어합니다.
/// </summary>
public class CommandManager : CoreManager
{
    #region Fields

    // 대기 중인 명령 큐
    private readonly Queue<GameCommand> _pendingCommands = new();

    // 실행 대기 중인 명령 큐
    private readonly Queue<GameCommand> _executionQueue = new();

    // 클라이언트 예측용 버퍼 (서버 확인 전 로컬 실행한 명령들)
    private readonly List<GameCommand> _predictedCommands = new();

    // 명령 검증 델리게이트
    private Func<GameCommand, bool> _commandValidator;

#if PHOTON_FUSION
    // 현재 프레임의 입력 데이터 (Fusion OnInput에서 수집됨)
    private NetworkInputData _currentInput;
#endif

    #endregion

    #region Properties

    // 현재 멀티플레이어 모드인지 여부
    public bool IsMultiplayer { get; private set; }

    // 현재 플레이어의 ID
    public ushort LocalPlayerId { get; private set; }

    // 클라이언트 예측 사용 여부
    public bool UseClientPrediction { get; set; } = false;

    public int PendingCommandCount => _executionQueue.Count;

    #endregion

    #region Events

    // 명령이 생성되었을 때 발생
    public event Action<GameCommand> OnCommandCreated;

    // 명령이 실행되기 직전에 발생
    public event Action<GameCommand> OnCommandExecuting;

    // 명령이 실행된 후 발생
    public event Action<GameCommand> OnCommandExecuted;

    #endregion

    #region Initialization

    protected override async UniTask OnInitializeAsync()
    {
        await base.OnInitializeAsync();

        LocalPlayerId = 0;
        IsMultiplayer = false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[CommandManager] Initialized");
#endif
    }

    #endregion

    #region Configuration

    /// <summary>
    /// 멀티플레이어 모드를 설정합니다.
    /// </summary>
    /// <param name="isMultiplayer">멀티플레이어 여부</param>
    /// <param name="playerId">서버에서 할당받은 플레이어 ID</param>
    public void SetMultiplayerMode(bool isMultiplayer, ushort playerId = 0)
    {
        IsMultiplayer = isMultiplayer;
        LocalPlayerId = playerId;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[CommandManager] Mode: {(isMultiplayer ? "Multiplayer" : "Local")}, PlayerId: {playerId}");
#endif
    }

    /// <summary>
    /// 명령 검증 함수를 설정합니다.
    /// </summary>
    public void SetCommandValidator(Func<GameCommand, bool> validator)
    {
        _commandValidator = validator;
    }

    #endregion

    #region Command Creation

    /// <summary>
    /// 새로운 명령을 생성하고 처리 파이프라인에 추가합니다.
    /// </summary>
    /// <param name="type">명령 종류</param>
    /// <param name="targetPos">목표 좌표</param>
    /// <param name="flags">명령 플래그</param>
    /// <param name="intParam">추가 정수 파라미터</param>
    /// <param name="targetIds">대상 오브젝트 ID들</param>
    public void CreateCommand(
        CommandType type,
        Vector2 targetPos = default,
        CommandFlags flags = CommandFlags.None,
        int intParam = 0,
        params int[] targetIds)
    {
        var command = GameCommand.Create(type, LocalPlayerId, targetPos, flags, intParam);

        if (targetIds != null && targetIds.Length > 0)
        {
            command = command.WithTargets(targetIds);
        }

        EnqueueCommand(command);
    }

    /// <summary>
    /// 이동 명령을 생성합니다.
    /// </summary>
    public void CreateMoveCommand(Vector2 targetPos, bool queued = false, params int[] unitIds)
    {
        var flags = queued ? CommandFlags.Queued : CommandFlags.None;
        CreateCommand(CommandType.Move, targetPos, flags, 0, unitIds);
    }

    /// <summary>
    /// 공격 명령을 생성합니다.
    /// </summary>
    public void CreateAttackCommand(int targetId, bool queued = false, params int[] unitIds)
    {
        var flags = queued ? CommandFlags.Queued : CommandFlags.None;
        var command = GameCommand.Create(CommandType.Attack, LocalPlayerId, default, flags, targetId);
        command = command.WithTargets(unitIds);
        EnqueueCommand(command);
    }

    /// <summary>
    /// 건설 명령을 생성합니다.
    /// </summary>
    public void CreateBuildCommand(int buildingTypeId, Vector2 position, int builderId)
    {
        CreateCommand(CommandType.Build, position, CommandFlags.None, buildingTypeId, builderId);
    }

    /// <summary>
    /// 유닛 생산 명령을 생성합니다.
    /// </summary>
    public void CreateTrainCommand(int unitTypeId, int buildingId)
    {
        CreateCommand(CommandType.Train, default, CommandFlags.None, unitTypeId, buildingId);
    }

    /// <summary>
    /// 스킬 사용 명령을 생성합니다.
    /// </summary>
    public void CreateSkillCommand(int skillId, Vector2 targetPos, int casterId, int targetId = -1)
    {
        var type = targetId >= 0 ? CommandType.UseSkillOnTarget : CommandType.UseSkillOnPosition;
        var command = GameCommand.Create(type, LocalPlayerId, targetPos, CommandFlags.None, skillId);
        command = command.WithTargets(casterId, targetId);
        EnqueueCommand(command);
    }

    #endregion

    #region Command Routing

    /// <summary>
    /// 명령을 처리 파이프라인에 추가합니다.
    /// </summary>
    public void EnqueueCommand(GameCommand command)
    {
        if (!ValidateCommand(command))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[CommandManager] Command validation failed: {command}");
#endif
            return;
        }

        OnCommandCreated?.Invoke(command);

        if (IsMultiplayer)
        {
            RouteToServer(command);
        }
        else
        {
            command.Tick = Main.Simulation?.CurrentTick ?? 0;
            _executionQueue.Enqueue(command);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[CommandManager] Command enqueued: {command}");
#endif
    }

    // 서버로 명령 전송
    private void RouteToServer(GameCommand command)
    {
#if PHOTON_FUSION
        // Fusion Input System을 통해 명령을 전달
        _currentInput = new NetworkInputData
        {
            CommandType = command.Type,
            CommandFlags = command.Flags,
            TargetPos = command.TargetPos,
            IntParam = command.IntParam,
            FloatParam = command.FloatParam,
            TargetIds = NetworkTargetIds.FromFixedArray(command.TargetIds)
        };
#endif

        // 클라이언트 예측 처리
        if (UseClientPrediction)
        {
            command.Tick = Main.Simulation?.CurrentTick ?? 0;
            _predictedCommands.Add(command);
            _executionQueue.Enqueue(command);
        }
    }

    /// <summary>
    /// 서버로부터 확정된 명령을 수신했을 때 호출됩니다.
    /// </summary>
    public void OnCommandReceivedFromServer(GameCommand command)
    {
        if (UseClientPrediction && command.PlayerId == LocalPlayerId)
        {
            ReconcilePrediction(command);
        }
        else
        {
            _executionQueue.Enqueue(command);
        }
    }

    // 서버 확정 명령과 예측 명령 비교/조정
    private void ReconcilePrediction(GameCommand confirmedCommand)
    {
        _predictedCommands.RemoveAll(cmd =>
            cmd.Type == confirmedCommand.Type &&
            cmd.TargetPos == confirmedCommand.TargetPos);
    }

    #endregion

    #region Command Execution

    /// <summary>
    /// 매 틱마다 대기 중인 명령들을 처리합니다.
    /// SimulationManager에서 호출됩니다.
    /// </summary>
    public void ProcessCommands(uint currentTick)
    {
        while (_executionQueue.TryPeek(out var command) && command.Tick <= currentTick)
        {
            _executionQueue.Dequeue();
            ExecuteCommand(command);
        }
    }

    // 명령 실행
    private void ExecuteCommand(GameCommand command)
    {
        OnCommandExecuting?.Invoke(command);
        Main.Simulation?.ExecuteCommand(command);
        OnCommandExecuted?.Invoke(command);
    }

    // 명령 유효성 검증
    private bool ValidateCommand(GameCommand command)
    {
        if (command.Type == CommandType.None) return false;

        if (_commandValidator != null && !_commandValidator(command))
        {
            return false;
        }

        return true;
    }

    #endregion

#if PHOTON_FUSION

    #region Fusion Input

    /// <summary>
    /// NetworkManager.OnInput() 콜백에서 호출됩니다.
    /// 현재 프레임에 수집된 입력 데이터를 반환하고 초기화합니다.
    /// </summary>
    public NetworkInputData CollectInput()
    {
        var input = _currentInput;
        _currentInput = default;
        return input;
    }

    #endregion

#endif

    #region Cleanup

    /// <summary>
    /// 대기 중인 모든 명령을 취소합니다.
    /// </summary>
    public void ClearPendingCommands()
    {
        _pendingCommands.Clear();
        _executionQueue.Clear();
        _predictedCommands.Clear();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[CommandManager] All pending commands cleared");
#endif
    }

    public override void Clear()
    {
        base.Clear();

        ClearPendingCommands();
        _commandValidator = null;
        OnCommandCreated = null;
        OnCommandExecuting = null;
        OnCommandExecuted = null;
    }

    #endregion
}
