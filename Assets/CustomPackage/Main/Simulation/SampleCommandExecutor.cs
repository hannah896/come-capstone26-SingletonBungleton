using UnityEngine;

/// <summary>
/// 게임별 명령 실행기 구현 예시.
/// 이 클래스를 참고하여 실제 게임 로직을 구현합니다.
/// </summary>
public class SampleCommandExecutor : CommandExecutorBase
{
    // 게임 시스템 참조 (예시)
    // private UnitManager _unitManager;
    // private BuildingManager _buildingManager;

    /// <summary>
    /// 실행기를 초기화하고 SimulationManager에 등록합니다.
    /// </summary>
    public void Initialize()
    {
        // SimulationManager에 기본 실행기로 등록
        Main.Simulation.SetDefaultExecutor(this);

        // 또는 특정 명령 타입에만 등록
        // Main.Simulation.RegisterExecutor(CommandType.Move, this);
        // Main.Simulation.RegisterExecutor(CommandType.Attack, this);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[SampleCommandExecutor] Initialized and registered");
#endif
    }

    protected override void OnMove(GameCommand command)
    {
        // 이동 명령 처리
        // 1. command.TargetIds에서 이동할 유닛들의 ID 가져오기
        // 2. command.TargetPos로 유닛들 이동

        for (int i = 0; i < command.TargetIds.Count; i++)
        {
            int unitId = command.TargetIds[i];
            Vector2 destination = command.TargetPos;

            // 실제 구현:
            // var unit = _unitManager.GetUnit(unitId);
            // unit?.MoveTo(destination, command.Flags.HasFlag(CommandFlags.Queued));

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Command] Unit {unitId} moving to {destination}");
#endif
        }
    }

    protected override void OnStop(GameCommand command)
    {
        // 정지 명령 처리
        for (int i = 0; i < command.TargetIds.Count; i++)
        {
            int unitId = command.TargetIds[i];

            // 실제 구현:
            // var unit = _unitManager.GetUnit(unitId);
            // unit?.Stop();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Command] Unit {unitId} stopped");
#endif
        }
    }

    protected override void OnAttack(GameCommand command)
    {
        // 공격 명령 처리
        int targetId = command.IntParam;

        for (int i = 0; i < command.TargetIds.Count; i++)
        {
            int attackerId = command.TargetIds[i];

            // 실제 구현:
            // var attacker = _unitManager.GetUnit(attackerId);
            // var target = _unitManager.GetUnit(targetId);
            // attacker?.Attack(target, command.Flags.HasFlag(CommandFlags.Queued));

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Command] Unit {attackerId} attacking {targetId}");
#endif
        }
    }

    protected override void OnBuild(GameCommand command)
    {
        // 건설 명령 처리
        int buildingTypeId = command.IntParam;
        Vector2 position = command.TargetPos;
        int builderId = command.TargetIds.Count > 0 ? command.TargetIds[0] : -1;

        // 실제 구현:
        // var builder = _unitManager.GetUnit(builderId);
        // var buildingType = DataManager.GetBuildingData(buildingTypeId);
        // _buildingManager.StartConstruction(buildingType, position, builder);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[Command] Building type {buildingTypeId} at {position} by builder {builderId}");
#endif
    }

    protected override void OnTrain(GameCommand command)
    {
        // 유닛 생산 명령 처리
        int unitTypeId = command.IntParam;
        int buildingId = command.TargetIds.Count > 0 ? command.TargetIds[0] : -1;

        // 실제 구현:
        // var building = _buildingManager.GetBuilding(buildingId);
        // var unitType = DataManager.GetUnitData(unitTypeId);
        // building?.QueueUnit(unitType);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[Command] Training unit type {unitTypeId} from building {buildingId}");
#endif
    }

    protected override void OnUseSkill(GameCommand command)
    {
        // 스킬 사용 명령 처리
        int skillId = command.IntParam;
        int casterId = command.TargetIds.Count > 0 ? command.TargetIds[0] : -1;
        int targetId = command.TargetIds.Count > 1 ? command.TargetIds[1] : -1;
        Vector2 targetPos = command.TargetPos;

        // 실제 구현:
        // var caster = _unitManager.GetUnit(casterId);
        // var skill = caster?.GetSkill(skillId);
        // if (command.Type == CommandType.UseSkillOnTarget)
        //     skill?.CastOnTarget(_unitManager.GetUnit(targetId));
        // else
        //     skill?.CastOnPosition(targetPos);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[Command] Unit {casterId} using skill {skillId} at {targetPos} (target: {targetId})");
#endif
    }

    protected override void OnSetRallyPoint(GameCommand command)
    {
        // 랠리포인트 설정 명령 처리
        int buildingId = command.TargetIds.Count > 0 ? command.TargetIds[0] : -1;
        Vector2 rallyPoint = command.TargetPos;

        // 실제 구현:
        // var building = _buildingManager.GetBuilding(buildingId);
        // building?.SetRallyPoint(rallyPoint);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[Command] Building {buildingId} rally point set to {rallyPoint}");
#endif
    }
}
