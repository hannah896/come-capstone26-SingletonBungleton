# 보스 강화학습 환경

`Assets/01_Scenes/Training/BossTrainingScene.unity` 에서 보스를 학습시킨다. **실제 플레이어용 공간이 아니다.**
학습이 끝난 모델(.onnx)을 최종 보스에 연결해 포털 안 보스로 쓴다.

## 구성
| 파일 | 역할 |
|---|---|
| `Assets/02_Scripts/06_BossTraining/Runtime/` | 아레나 규칙(보스 이동·근접·대시 판정, 스파링 플레이어 AI, 에피소드). ML-Agents 없이도 컴파일됨 |
| `Assets/02_Scripts/06_BossTraining/Agent/BossAgent.cs` | 관측·행동·보상. ML-Agents 설치 시에만 컴파일됨 |
| `MLAgents/boss_ppo.yaml` | PPO 학습 설정 |

## 1. 유니티 준비 (한 번만)
1. `Packages/manifest.json` 에 `"com.unity.ml-agents": "4.0.0"` (Package Manager → `+` → Add package by name 으로 넣어도 됨).
   - 4.0.0 은 Unity 6000.0 이상용이며 Inference Engine 2.2.1 을 쓴다.
   - 3.0.0 은 Sentis 2.1.0 을 요구하는데, Unity 6000.0 에는 Sentis 2.2.0(껍데기)이 내장돼 있어 `Tensor`/`Model` 타입을 못 찾아 컴파일 에러가 난다.
2. `보스 학습/아레나 씬 생성` → 아레나 프리팹과 학습 씬 생성
3. `보스 학습/보스 에이전트 연결` → 보스에 BossAgent 부착 (패키지가 설치돼 컴파일된 뒤에만 메뉴가 보인다)

## 2. 파이썬 준비 (한 번만)
ML-Agents 파이썬 패키지는 **파이썬 3.10.x** 가 필요하다 (3.12 는 안 됨). 3.10 을 설치한 뒤 cmd 에서 프로젝트 폴더 기준으로:
```bash
py -3.10 -m venv mlagents-venv
mlagents-venv\Scripts\activate
python -m pip install --upgrade pip
pip install torch~=2.2.1 --index-url https://download.pytorch.org/whl/cu121
pip install mlagents==1.1.0
mlagents-learn --help
```
- GPU 가 없으면 torch 줄에서 `--index-url ...` 를 빼고 설치한다.
- numpy/protobuf 충돌이 나면 `pip install "numpy<2" "protobuf==3.20.3"`.
- 유니티 패키지와 파이썬 패키지 버전은 서로 맞아야 통신된다 (위 조합 기준).

## 3. 학습
```bash
mlagents-learn MLAgents/boss_ppo.yaml --run-id=boss-ppo-01 --time-scale=20
```
터미널에 "Start training by pressing the Play button" 가 뜨면 유니티에서 `BossTrainingScene` 을 Play.
- 이어서 학습: `--resume`, 처음부터 다시: `--force`
- 진행 확인: `tensorboard --logdir results`
- 결과 모델: `results/boss-ppo-01/BossAgent.onnx`

## 4. 검증
- 환경만 확인하고 싶을 때: 보스 `Behavior Type = Heuristic Only` 로 두고 Play (다가가서 치는 단순 규칙으로 움직임)
- 학습 후: `.onnx` 를 보스 `Model` 칸에 넣고 `Behavior Type = Inference Only`
- 승률, 근접/대시 비율, 평균 거리를 보고 사람이 직접 플레이해 난이도를 확인한다.
