# 지역별 수분 날씨

월드 생성 시 `WorldSimulationManager`가 지역 날씨와 화면 효과를 자동으로 초기화합니다.
실제 게임에서는 비와 눈 프리팹을 씬에 수동 배치하지 않아도 됩니다.

각 지역은 독립적인 수분량 0~100과 난수 상태를 가집니다.
맑을 때 100까지 충전하면 비/눈이 시작되고, 강수 중에는 충전을 멈추고 0까지 소모합니다.
맑음과 강수에 진입할 때마다 최소~최대 일수 사이의 기간을 균등 추첨하고
`100 / 선택한 기간`으로 하루당 충전/소모 속도를 계산합니다.
같은 바이옴이라도 지역이 다르면 일정이 독립적이며, 방문하지 않은 지역도 월드 시간에 따라 진행됩니다.

| 바이옴 | 강수 | 강수가 그친 뒤 맑은 기간 | 한 번의 강수 기간 |
|---|---|---|---|
| Plains / Starts | 비 | 1~1.5일 | 1.5~3.5일 |
| HighLands | 비 | 10~15일 | 0.5~1일 |
| Snow | 눈 | 1.5~2.5일 | 0.5~1일 |
| Desert / RockyDesert | 없음 | 계속 맑음 | 없음 |

`05_Datas/WorldData_KGB/Biomes`의 각 바이옴 에셋에서 **Weather**를 편집합니다.
Forest/Swamp/Rocky/Savanna는 아직 주기를 지정하지 않아 강수가 비활성화되어 있습니다.
이 바이옴에도 강수가 필요하면 Precipitation과 네 기간 값을 입력합니다.
활성화한 강수의 기간은 모두 0보다 커야 하며 최소값이 최대값 이하여야 합니다.

새 월드는 수분 0의 맑음으로 시작합니다. 현재 기본 배속에서는 하루가 현실 24분이므로
Plains의 첫 비까지 24~36분이 걸립니다. 월드 시계 배속/시간 건너뛰기도 날씨에 함께 적용됩니다.
저장에는 수분, 현재 단계와 선택한 기간, 난수 상태가 포함되어 다음 주기도 이어집니다.
날씨 항목이 없는 이전 저장 파일은 불러온 시점부터 새로운 맑은 주기를 시작합니다.
멀티플레이에서는 호스트가 추첨하고 클라이언트는 확정된 기간과 수분을 수신합니다.

`05_Datas/WorldData_KGB/WorldWeatherSettings.asset`에서 비/눈 프리팹,
전환 시간, 비/눈 강도와 눈 생성량을 조절합니다. `WorldSettings.asset`에 연결되어 있습니다.
효과는 로컬 플레이어의 지역을 기준으로 전환하고, 생성된 각 입자의 위치도 확인해
맑은 이웃 지역이나 사막으로 비/눈이 새는 것을 막습니다.
기존 Snow.prefab과 외부 눈 원본은 보존하며 화면용 런타임 복제본만 강수에 맞게 설정합니다.

```csharp
var weather = WorldWeatherController.Instance;
if (weather != null && weather.TryGetWeather(transform.position, out var region))
{
    bool wet = region.IsPrecipitating;
    double moisture = region.Moisture;
    PrecipitationKind kind = region.Kind;
}
```

하늘/조명과 빗소리도 로컬 플레이어의 지역 날씨를 따라 전환됩니다.
`WorldSkyBoxSettings.asset`의 각 시간대에서 RainSkybox/SnowSkybox와 WeatherSkyExposure를 설정합니다.
새벽에는 강수 하늘 노출을 0.15, 아침 0.8, 오후 1, 황혼 0.4로 설정했습니다.
Rain/Snow Light/Ambient Multiplier로 시간대 기본 조명에 적용할 날씨 보정을 조절합니다.
Skybox 머티리얼은 교체 방식이며 조명은 강수 전환 비율에 따라 변합니다.
외부 Skybox 원본은 수정하지 않고 런타임 복제본의 노출만 보정합니다.

`WorldWeatherSettings.asset`의 RainLoop/RainVolume에서 빗소리와 최대 음량을 설정합니다.
빗소리는 2D 루프로 재생하며 강수 전환에 따라 부드럽게 켜지고 꺼집니다.
기존 JSAM의 마스터/SFX 음량과 음소거를 따르고, 눈이나 맑은 지역에서는 정지합니다.
플레이어 체온/젖음은 아직 지역 날씨와 연결하지 않았습니다.

# 비 프리팹 단독 테스트

`Rain.prefab`만 별도로 확인하려면 테스트 씬에 배치하고 Play합니다.
단독 테스트에서는 Main Camera 주변에서 인스펙터 강도에 따라 계속 비가 내립니다.

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
