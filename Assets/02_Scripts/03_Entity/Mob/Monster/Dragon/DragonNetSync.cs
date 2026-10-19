#if PHOTON_FUSION
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// 최종 보스전 상태 복제 (기획 7단계). NetworkMonsterDirector가 호출한다.
/// - 호스트: Capture — 살아 있는 드래곤의 게이지·광폭화·각인, 기둥 부식, 월식 여부를 DragonNetState에 담는다
/// - 클라: Apply — 받은 값을 보스 표시값(HUD), 기둥 부식, 각인 표시, 월식 조명에 반영한다
/// 판정은 모두 호스트에만 있고 클라는 보여주기만 한다.
/// </summary>
public static class DragonNetSync
{
    private const int MaxPillars = 8;
    private static readonly List<DragonCover> s_pillars = new List<DragonCover>();

    public static DragonNetState Capture()
    {
        var state = new DragonNetState { EnrageLeft = ushort.MaxValue };

        var encounter = FinalBossEncounter.Current;
        if (encounter != null && encounter.IsEclipse) state.Flags |= DragonNetState.FlagEclipse;

        DragonCover.GetSorted(s_pillars);
        for (int i = 0; i < s_pillars.Count && i < MaxPillars; i++)
            state.PillarCorrosion |= (uint)(Mathf.Clamp(s_pillars[i].Corrosion, 0, 15) << (i * 4));

        var boss = FindBoss();
        if (boss == null) return state;

        state.Flags |= DragonNetState.FlagHasBoss;
        if (boss.IsShootdownGauge) state.Flags |= DragonNetState.FlagShootdown;
        if (boss.IsGroggy) state.Flags |= DragonNetState.FlagGroggy;
        if (boss.IsEnraged) state.Flags |= DragonNetState.FlagEnraged;
        state.Poise = (byte)Mathf.RoundToInt(Mathf.Clamp01(boss.PoiseRatio) * 255f);

        float enrage = boss.EnrageTimeLeft;
        if (enrage >= 0f) state.EnrageLeft = ToTenths(enrage);

        if (boss.TryGetMark(0, out var p0, out float l0)) { state.Mark0 = IdOf(p0); state.Mark0Left = ToTenths(l0); }
        if (boss.TryGetMark(1, out var p1, out float l1)) { state.Mark1 = IdOf(p1); state.Mark1Left = ToTenths(l1); }
        return state;
    }

    public static void Apply(NetworkRunner runner, in DragonNetState state)
    {
        FinalBossEncounter.Current?.ApplyNetEclipse((state.Flags & DragonNetState.FlagEclipse) != 0);

        DragonCover.GetSorted(s_pillars);
        for (int i = 0; i < s_pillars.Count && i < MaxPillars; i++)
            s_pillars[i].SetCorrosion((int)((state.PillarCorrosion >> (i * 4)) & 0xF));

        var boss = FindBoss();
        if (boss == null || (state.Flags & DragonNetState.FlagHasBoss) == 0) return;

        float enrage = state.EnrageLeft == ushort.MaxValue ? -1f : state.EnrageLeft / 10f;
        boss.ApplyNetDisplay(state.Poise / 255f,
            (state.Flags & DragonNetState.FlagShootdown) != 0,
            (state.Flags & DragonNetState.FlagGroggy) != 0,
            (state.Flags & DragonNetState.FlagEnraged) != 0,
            enrage);

        boss.ApplyNetMark(0, FindPlayer(runner, state.Mark0), state.Mark0Left / 10f);
        boss.ApplyNetMark(1, FindPlayer(runner, state.Mark1), state.Mark1Left / 10f);
    }

    // 살아 있는 드래곤 (동시에 하나만 싸운다)
    private static DragonBoss FindBoss()
    {
        foreach (var boss in DragonBoss.Active)
        {
            if (boss != null && boss.isActiveAndEnabled && boss.Status != null && !boss.Status.IsDead)
                return boss;
        }
        return null;
    }

    private static ushort ToTenths(float seconds)
        => (ushort)Mathf.Clamp(Mathf.RoundToInt(seconds * 10f), 0, ushort.MaxValue - 1);

    private static NetworkId IdOf(Player player)
        => player != null && player.TryGetComponent(out NetworkObject no) && no.IsValid ? no.Id : default;

    private static Player FindPlayer(NetworkRunner runner, NetworkId id)
    {
        if (!id.IsValid || runner == null) return null;
        return runner.TryFindObject(id, out var no) && no != null ? no.GetComponent<Player>() : null;
    }
}
#endif
