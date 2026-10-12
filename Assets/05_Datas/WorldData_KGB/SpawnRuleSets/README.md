# 몬스터 스폰 규칙 데이터

| 폴더 | ScriptableObject 타입 | 내용 | 연결 위치 |
| --- | --- | --- | --- |
| `BiomeSets/` | `SpawnRuleSet` | 바이옴별 자연 스폰·최초 배치 규칙 묶음 | `BiomeData.BiomeSpawnRule` |
| `Nature/` | `NatureSpawnRule` | 플레이어 주변 자연 스폰 규칙 | `SpawnRuleSet.NatureSpawnRules` |
| `Source/` | `SourceSpawnRule` | 월드에 배치된 집·둥지의 몬스터 생성 조건 | 스폰 프리팹 루트의 `MonsterSpawnSource.spawnRule` |
| `Initial/` | `InitialSpawnRule` | 월드 생성 시 한 번 확정할 몬스터 배치 규칙 | `SpawnRuleSet.InitialSpawnRules` |
| `Periodic/` | `PeriodicSpawnRule` | 월드 시계 기준 반복 생성 규칙 | `WorldSettings.PeriodicSpawnRules` |
| `Boss/` | `BossSpawnRule` | 만월 횟수에 따라 8종을 순환하는 보스 규칙 | `WorldSettings.BossSpawnRule` |

각 방식의 `*Example.asset`은 `MonsterCatalog`에 등록된 `Mischief`를 사용하는 예시 데이터다. 현재 사막 바이옴의 `InitialSpawnRules`에는 최초 배치 예시가 연결되어 있다. `WorldSettings.PeriodicSpawnRules`는 비어 있으며, 별도의 `WorldSettings.BossSpawnRule`에는 `Boss/FullMoonBossCycle`이 연결되어 있다.

1. **자연 스폰**: 바이옴이 참조하는 `BiomeSets/DesertSpawnRuleSet` 등의 `NatureSpawnRules`에 `Nature/Nature_Demon` 등 몬스터별 규칙을 추가한다. 플레이어 기준 최소·최대 생성 거리는 각 규칙이 아닌 `WorldSettings.DynamicSpawnSettings`에서 공통 설정한다.
2. **스폰 오브젝트**: 몬스터 집 등 표시할 프리팹을 Addressables에 등록하고, 프리팹 루트에 `MonsterSpawnSource` 컴포넌트를 붙인다. `spawnRule`에 `Source/Source_TreantMinion` 또는 `Source/Source_TreantTree` 등 생성 규칙을 연결한다. 컴포넌트의 `placementSettings`에 배치 반경·허용 높이 차이·월드 시작점과의 최소 거리를 지정한다. `SourceSpawnRule`은 몬스터 종류·시간대·주기·확률·생성 수·생존 수·생성 반경·활성화 거리만 담당한다.

바이옴의 `BiomeDisposeRule > SpawnerRules`에는 `ruleName`, `prefabKey`, `minCount`, `maxCount`만 입력한다. `prefabKey`는 스폰 프리팹의 Address이고 `minCount/maxCount`는 지역마다 배치할 집의 수다. 프리팹의 몬스터 생성 규칙을 이 목록에서 다시 연결하지 않는다. 현재 `TreeantMinionSpawner`와 `TreeantTreeSpawner`에는 각각 대응하는 Source 규칙과 기존 배치 조건이 연결되어 있다. 어느 바이옴에 얼마나 배치할지는 `SpawnerRules`에서 설정한다.

월드 생성 과정은 같은 프리팹 에셋을 한 번 읽어 설정을 캐시하고, 실제 인스턴스를 만들지 않고 배치 타일·ID와 별도의 논리 스폰 소스 목록을 만든다. `WorldMonsterSpawnDirector`가 이 목록을 받아 생성·저장·복구를 관리하므로 호스트의 청크가 로드되지 않아도 원격 플레이어 주변에서 검사할 수 있다. 집 프리팹에는 독립적으로 생성하는 기존 `MonsterSpawner` 컴포넌트를 추가하지 않는다. 집을 부술 수 있게 만들 경우 기존 배치 오브젝트처럼 청크의 배치 ID에 파괴 상태를 기록해야 스폰이 중단된다. 프리팹 배치 조건을 바꾼 뒤에는 새 월드로 확인한다.
3. **월드 최초 배치**: 바이옴의 `BiomeSpawnRule > InitialSpawnRules`에 `Initial/Initial_MischiefExample`을 추가한다. 월드 생성 시 몬스터의 타일과 ID가 확정되고, 실제 프리팹은 플레이어가 가까워졌을 때 생성된다. 사망 상태와 살아 있는 몬스터의 위치·체력은 저장된다.
4. **일정 주기**: `WorldSettings > PeriodicSpawnRules`에 `Periodic/Periodic_Mischief`을 추가한다. 이 규칙은 바이옴별 `SpawnRuleSet`을 거치지 않고 월드 전역에서 실행한다. `ruleId`는 저장 상태의 키이므로 만든 뒤 유지한다. `intervalDays`에 반복 간격을 게임 내 일수로 입력한다. 예를 들어 `intervalDays = 3`, `firstDelayDays = 3`이면 새 월드 시작 후 3일 뒤 처음 검사하고 이후 3일마다 검사한다. `firstDelayDays = 0`이면 첫 검사를 즉시 시작한다. 내부에서만 월드 시계의 게임 초로 변환하며, 기존 저장본의 다음 검사 시각은 그대로 복원한다. 큰 시간 건너뛰기 후 밀린 회차를 한꺼번에 실행하지 않는다. 현재 Mischief 규칙은 7일 간격·첫 지연 3일로 설정되어 있다.
5. **만월 보스**: Create 메뉴의 `Scriptable Objects/TestWorld/BossSpawnRule`로 생성하고 `WorldSettings > BossSpawnRule`에 하나만 연결한다. `bossMonsterKeys`의 Element 0~7에 #1~#8의 카탈로그 키를 입력한다. 만월 번호는 날짜로 계산하고 `(만월 번호 - 1) % 8`로 한 종류만 선택한다. 현재 달 주기에서는 5·13·21·29·37·45·53·61일차에 #1~#8, 69일차에 다시 #1이 선택된다. 빈 슬롯은 다른 슬롯으로 대체하거나 순서를 압축하지 않으며, 키를 설정하기 전에는 해당 회차가 생성되지 않는다. 현재 #1은 `Devil`, #2는 `Treeguard`, #3은 `CyclopsBatMage`이며, 나머지 5개 슬롯은 비워 두었다.

보스 데이터에는 반복 간격·생성 수량·고정 위치 모드가 없다. `allowedTimePhases`로 하루 중 허용 시간대를, `minDistanceFromPlayer`/`maxDistanceFromPlayer`로 생성 거리를 설정한다. 현재는 모든 시간대, 25~40m이다. 살아 있는 플레이어 한 명을 무작위로 선택하고 기존 주기 스폰의 지면·점유·제단 제외·충돌 검사로 유효한 위치를 탐색한다. 생성 위치는 모든 플레이어로부터 최소 거리를 확보한다.

이전 보스가 살아 있거나 비활성 상태로 보관되어 있으면 이번 만월은 건너뛰고, 처치 후 다음 만월의 날짜에 맞는 슬롯을 선택한다. 실패한 만월 예약은 다음 날에도 유지되고, 여러 만월을 건너뛰면 가장 최근 회차 하나만 처리한다. 마지막 처리 날짜·대기 날짜와 실제 생성한 몬스터 키·위치·체력을 저장하므로, 다음 만월이나 재접속 시 기존 보스 종류가 바뀌지 않는다. 기존 `full-moon-devil` 주기 저장본은 보스 일정으로 옮기고 기존 개체는 #1의 키로 복구한다. 저장 ID와 이동한 에셋 GUID는 유지했다.

다섯 방식 모두 싱글플레이 또는 멀티플레이 호스트에서 실제 몬스터를 만들고, 멀티플레이 클라이언트는 기존 `NetworkMonsterDirector`의 복제를 받는다. 집·최초 배치·주기 방식의 공통 개체 수 제한은 `WorldSettings > DynamicSpawnSettings > MaxAdditionalMonsters`에서 설정한다. 보스의 신규 생성과 비활성 보스 복원은 공통 제한을 우회하며, 살아 있는 보스는 월드당 한 마리만 유지한다. 자연 스폰의 `MaxSpawnSlots`는 별개다.
