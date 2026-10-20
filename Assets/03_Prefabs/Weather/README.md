# 비 파티클

`Rain.prefab`을 씬에 배치하고 Play하면 Main Camera 주변에서 비가 내립니다.
게임 씬에 자동 배치되지는 않으므로 원하는 씬에 직접 한 번 배치합니다.

- `RainEffect.Intensity`: 0~1 범위. 인스펙터에서 실행 중에도 조정할 수 있습니다.
- `RainEffect.SetIntensity(0.3f)`: 약한 비. 0은 새 빗방울 생성을 멈추고 남은 빗방울은 사라질 때까지 떨어집니다.
- `RainEffect.SetTarget(player.transform)`: 지정한 대상을 추적합니다. 대상이 없으면 Main Camera를 사용합니다.
- `Follow Main Camera`를 끄고 대상을 비워 두면 배치한 위치에 비를 내립니다.
- `Teleport Distance`: 추적 대상이 멀리 이동하거나 바뀌면 이전 빗방울을 지우는 거리입니다.

루트 위치 기준 12m 위의 `RainDrops`에서 가로·세로 24m 범위에 생성합니다.
최대 강도에서는 초당 1,200개를 생성하고 빗방울은 월드 좌표에서 초당 약 20m로 떨어집니다.
일반 이동 시 이미 생성한 빗방울이 카메라와 함께 끌려가지 않습니다.

`RainDrops`의 Particle System에서 Shape/Emission/Velocity over Lifetime을 조정하고,
`08_Materials/Weather/Rain.mat`의 Rain Color로 색과 투명도를 조정할 수 있습니다.
URP 전용 셰이더가 빗줄기를 계산하므로 별도 텍스처나 외부 패키지를 사용하지 않습니다.

지면과 구조물의 정적 콜라이더에 닿으면 빗방울이 사라집니다(Ground/Structure 레이어).
콜라이더가 없는 표면과 동적 콜라이더에는 충돌하지 않습니다.
물 튀김, 빗소리, 자동 날씨 변경, 플레이어 젖음/기온 연동은 이 프리팹에 포함하지 않습니다.
