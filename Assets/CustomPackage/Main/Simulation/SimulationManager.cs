using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 게임 시뮬레이션의 핵심 로직을 담당하는 매니저.
/// 로컬 및 서버에서 동일한 코드로 실행되어 결정론적 결과를 보장합니다.
/// </summary>
public class SimulationManager : CoreManager
{
    #region Fields

    // 명령 타입별 실행기 매핑
    private readonly Dictionary<CommandType, ICommandExecutor> _executors = new();

    // 기본 명령 실행기 (등록된 실행기가 없을 때 사용)
    private ICommandExecutor _defaultExecutor;

    // 틱 누적 시간
    private float _tickAccumulator;

    #endregion

    #region Properties

    // 현재 시뮬레이션 틱
    public uint CurrentTick { get; private set; }

    // 틱당 시간 (초), 기본값 20틱/초
    public float TickDuration { get; private set; } = 1f / 20f;

    // 시뮬레이션 실행 중 여부
    public bool IsRunning { get; private set; }

    // 시뮬레이션 일시정지 여부
    public bool IsPaused { get; private set; }

    #endregion

    #region Events

    // 틱 진행 시 발생
    public event Action<uint> OnTick;

    // 시뮬레이션 시작 시 발생
    public event Action OnSimulationStarted;

    // 시뮬레이션 종료 시 발생
    public event Action OnSimulationStopped;

    #endregion

    #region Initialization

    protected override async UniTask OnInitializeAsync()
    {
        await base.OnInitializeAsync();

        CurrentTick = 0;
        IsRunning = false;
        IsPaused = false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[SimulationManager] Initialized");
#endif
    }

    #endregion

    #region Configuration

    /// <summary>
    /// 틱 레이트를 설정합니다.
    /// </summary>
    /// <param name="ticksPerSecond">초당 틱 수</param>
    public void SetTickRate(int ticksPerSecond)
    {
        TickDuration = 1f / ticksPerSecond;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[SimulationManager] Tick rate set to {ticksPerSecond} ticks/sec");
#endif
    }

    /// <summary>
    /// 명령 실행기를 등록합니다.
    /// </summary>
    public void RegisterExecutor(CommandType type, ICommandExecutor executor)
    {
        _executors[type] = executor;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[SimulationManager] Executor registered for {type}");
#endif
    }

    /// <summary>
    /// 여러 명령 타입에 대해 동일한 실행기를 등록합니다.
    /// </summary>
    public void RegisterExecutor(ICommandExecutor executor, params CommandType[] types)
    {
        foreach (var type in types)
        {
            _executors[type] = executor;
        }
    }

    /// <summary>
    /// 기본 명령 실행기를 설정합니다.
    /// </summary>
    public void SetDefaultExecutor(ICommandExecutor executor)
    {
        _defaultExecutor = executor;
    }

    #endregion

    #region Simulation Control

    /// <summary>
    /// 시뮬레이션을 시작합니다.
    /// </summary>
    public void StartSimulation()
    {
        if (IsRunning) return;

        CurrentTick = 0;
        _tickAccumulator = 0f;
        IsRunning = true;
        IsPaused = false;

        Main.Loop.OnUpdate += Update;
        OnSimulationStarted?.Invoke();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[SimulationManager] Simulation started");
#endif
    }

    /// <summary>
    /// 특정 틱부터 시뮬레이션을 시작합니다. (리플레이, 재접속 등)
    /// </summary>
    public void StartSimulation(uint startTick)
    {
        CurrentTick = startTick;
        StartSimulation();
    }

    /// <summary>
    /// 시뮬레이션을 정지합니다.
    /// </summary>
    public void StopSimulation()
    {
        if (!IsRunning) return;

        IsRunning = false;
        IsPaused = false;

        Main.Loop.OnUpdate -= Update;
        OnSimulationStopped?.Invoke();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[SimulationManager] Simulation stopped at tick {CurrentTick}");
#endif
    }

    /// <summary>
    /// 시뮬레이션을 일시정지합니다.
    /// </summary>
    public void Pause()
    {
        if (!IsRunning || IsPaused) return;

        IsPaused = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[SimulationManager] Simulation paused");
#endif
    }

    /// <summary>
    /// 시뮬레이션을 재개합니다.
    /// </summary>
    public void Resume()
    {
        if (!IsRunning || !IsPaused) return;

        IsPaused = false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[SimulationManager] Simulation resumed");
#endif
    }

    /// <summary>
    /// 강제로 특정 틱으로 설정합니다. (서버 동기화용)
    /// </summary>
    public void SetTick(uint tick)
    {
        CurrentTick = tick;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[SimulationManager] Tick forced to {tick}");
#endif
    }

    /// <summary>
    /// 현재 시뮬레이션 시간을 초 단위로 반환합니다.
    /// </summary>
    public float GetSimulationTime()
    {
        return CurrentTick * TickDuration;
    }

    #endregion

    #region Tick Processing

    // 매 프레임 호출
    private void Update(float deltaTime)
    {
        if (!IsRunning || IsPaused) return;

        // 멀티플레이어에서는 고정 틱 레이트 유지, 싱글에서는 게임 속도 반영
        var speed = Main.Command != null && Main.Command.IsMultiplayer
            ? 1f
            : (Main.Loop?.GameSpeed ?? 1f);

        _tickAccumulator += deltaTime * speed;

        while (_tickAccumulator >= TickDuration)
        {
            _tickAccumulator -= TickDuration;
            ProcessTick();
        }
    }

    // 단일 틱 처리
    private void ProcessTick()
    {
        Main.Command?.ProcessCommands(CurrentTick);
        OnTick?.Invoke(CurrentTick);
        CurrentTick++;
    }

    #endregion

    #region Command Execution

    /// <summary>
    /// 명령을 실행합니다. CommandManager에서 호출됩니다.
    /// </summary>
    public void ExecuteCommand(GameCommand command)
    {
        if (_executors.TryGetValue(command.Type, out var executor))
        {
            executor.Execute(command);
        }
        else if (_defaultExecutor != null)
        {
            _defaultExecutor.Execute(command);
        }
        else
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[SimulationManager] No executor found for command type: {command.Type}");
#endif
        }
    }

    #endregion

    #region Cleanup

    public override void Clear()
    {
        base.Clear();

        StopSimulation();
        _executors.Clear();
        _defaultExecutor = null;
        _tickAccumulator = 0f;

        OnTick = null;
        OnSimulationStarted = null;
        OnSimulationStopped = null;
    }

    #endregion
}

#region Command Executor

/// <summary>
/// 명령 실행기 인터페이스.
/// 각 명령 타입별로 구현하여 게임 로직을 처리합니다.
/// </summary>
public interface ICommandExecutor
{
    /// <summary>
    /// 명령을 실행합니다.
    /// </summary>
    void Execute(GameCommand command);
}

/// <summary>
/// 여러 명령 타입을 처리할 수 있는 기본 실행기 클래스.
/// 상속하여 게임별 로직을 구현합니다.
/// </summary>
public abstract class CommandExecutorBase : ICommandExecutor
{
    public void Execute(GameCommand command)
    {
        switch (command.Type)
        {
            case CommandType.Move:
                OnMove(command);
                break;
            case CommandType.Stop:
                OnStop(command);
                break;
            case CommandType.Hold:
                OnHold(command);
                break;
            case CommandType.Attack:
                OnAttack(command);
                break;
            case CommandType.AttackMove:
                OnAttackMove(command);
                break;
            case CommandType.Build:
                OnBuild(command);
                break;
            case CommandType.Train:
                OnTrain(command);
                break;
            case CommandType.UseSkill:
            case CommandType.UseSkillOnTarget:
            case CommandType.UseSkillOnPosition:
                OnUseSkill(command);
                break;
            case CommandType.SetRallyPoint:
                OnSetRallyPoint(command);
                break;
            default:
                OnCustomCommand(command);
                break;
        }
    }

    // 이동 명령 처리
    protected virtual void OnMove(GameCommand command) { }

    // 정지 명령 처리
    protected virtual void OnStop(GameCommand command) { }

    // 홀드 명령 처리
    protected virtual void OnHold(GameCommand command) { }

    // 공격 명령 처리
    protected virtual void OnAttack(GameCommand command) { }

    // 공격 이동 명령 처리
    protected virtual void OnAttackMove(GameCommand command) { }

    // 건설 명령 처리
    protected virtual void OnBuild(GameCommand command) { }

    // 유닛 생산 명령 처리
    protected virtual void OnTrain(GameCommand command) { }

    // 스킬 사용 명령 처리
    protected virtual void OnUseSkill(GameCommand command) { }

    // 랠리포인트 설정 명령 처리
    protected virtual void OnSetRallyPoint(GameCommand command) { }

    // 커스텀 명령 처리
    protected virtual void OnCustomCommand(GameCommand command) { }
}

#endregion
