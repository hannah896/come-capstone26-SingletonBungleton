# 몬스터 스폰 규칙 데이터

| 폴더 | ScriptableObject 타입 | 내용 | 연결 위치 |
| --- | --- | --- | --- |
| `BiomeSets/` | `SpawnRuleSet` | 바이옴별 자연 스폰·최초 배치 규칙 묶음 | `BiomeData.BiomeSpawnRule` |
| `Natural/` | `NatureSpawnRule` | 플레이어 주변 자연 스폰 규칙 | `SpawnRuleSet.MonsterRules` |
| `Source/` | `SourceSpawnRule` | 월드에 배치된 집·둥지의 몬스터 생성 조건 | `DisposeRuleSet.SpawnerRules[].monsterSpawnRule` |
| `Initial/` | `InitialSpawnRule` | 월드 생성 시 한 번 확정할 몬스터 배치 규칙 | `SpawnRuleSet.InitialMonsterRules` |
| `Periodic/` | `PeriodicSpawnRule` | 월드 시계 기준 반복 생성 규칙 | `WorldSettings.PeriodicMonsterSpawnRules` |

각 방식의 `*Example.asset`은 `MonsterCatalog`에 등록된 `Mischief`를 사용하는 예시 데이터다. 네 예시 모두 현재 월드의 규칙 목록에 **연결하지 않았다**. 연결하면 실제 몬스터 수와 위치가 바뀐다. `Natural/MobSpawn.asset`은 기존 데이터로 보존했으며 예시와 별개다.

1. **자연 스폰**: 바이옴이 참조하는 `BiomeSets/DesertSpawnRuleSet` 등의 `MonsterRules`에 `Natural/Natural_MischiefExample`을 추가한다. 플레이어 기준 최소·최대 생성 거리는 각 규칙이 아닌 `WorldSettings.DynamicSpawnSettings`에서 공통 설정한다.
2. **스폰 오브젝트**: 몬스터 집 등 표시할 프리팹을 Addressables에 등록한다. 바이옴의 `BiomeDisposeRule > SpawnerRules`에 항목을 추가하고 `prefabKey`에는 프리팹의 Addressable 키, `monsterSpawnRule`에는 `Source/Source_MischiefExample`을 지정한다. `minCount/maxCount`는 지역마다 배치할 집의 수다. 배치 타일과 ID는 월드 시드로 결정된다. 집 프리팹에는 기존 `MonsterSpawner` 컴포넌트를 붙이지 않는다. 생성 판단은 배치 ID를 사용하는 `WorldMonsterSpawnDirector`가 담당한다. 집을 부술 수 있게 만들 경우 기존 배치 오브젝트처럼 파괴 상태를 청크의 배치 ID로 기록해야 스폰이 중단된다. 이 저장소에는 연결할 몬스터 집 프리팹이 아직 없다.
3. **월드 최초 배치**: 바이옴의 `BiomeSpawnRule > InitialMonsterRules`에 `Initial/Initial_MischiefExample`을 추가한다. 월드 생성 시 몬스터의 타일과 ID가 확정되고, 실제 프리팹은 플레이어가 가까워졌을 때 생성된다. 사망 상태와 살아 있는 몬스터의 위치·체력은 저장된다.
4. **일정 주기**: `WorldSettings > PeriodicMonsterSpawnRules`에 `Periodic/Periodic_MischiefExample`을 추가한다. `ruleId`는 저장 상태의 키이므로 만든 뒤 유지한다. 주기는 월드 시계의 게임 시간으로 계산하며, 큰 시간 건너뛰기 후 밀린 회차를 한꺼번에 실행하지 않는다.

네 방식 모두 싱글플레이 또는 멀티플레이 호스트에서 실제 몬스터를 만들고, 멀티플레이 클라이언트는 기존 `NetworkMonsterDirector`의 복제를 받는다. 추가 세 방식의 공통 개체 수 제한은 `WorldSettings > DynamicSpawnSettings > MaxAdditionalMonsters`에서 설정한다. 자연 스폰의 `MaxSpawnSlots`는 별개다.
