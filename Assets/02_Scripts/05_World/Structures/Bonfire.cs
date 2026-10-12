using UnityEngine;

public class Bonfire : StationBase
{
    public override StationType StationType => StationType.Bonfire;

    // 모닥불은 설치돼 있는 동안 계속 타므로, 켜져 있는 동안 타는 소리(3D 루프)를 붙여 둔다.
    // (사운드 재생/정지는 이벤트 구독이 아니라서 OnEnable/OnDisable에서 짝을 맞춘다 — 풀 반환·철거 시 멈춰야 함)
    private void OnEnable()
    {
        Extensions.PlaySFXOn(AudioLibrarySounds.FireBurning, transform);
    }

    private void OnDisable()
    {
        Extensions.StopSFXOn(AudioLibrarySounds.FireBurning, transform);
    }
}
