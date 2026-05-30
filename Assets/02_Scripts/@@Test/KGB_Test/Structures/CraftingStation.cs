using System;
using UnityEngine;

/// <summary>
/// 마커, 기본적으로 크래프팅 스테이션은 상호작용이 불가능한 구조물로 간주.
/// TODO: OnPlayerEnter, OnPlayerExit 등의 이벤트를 통해 오버라이드 해 연결 되어있음을 나타내는 시각적 효과 구현 고려
/// </summary>
public abstract class CraftingStation : StationBase
{
    public override bool CanInteract(InteractionContext ctx) => false;
}

