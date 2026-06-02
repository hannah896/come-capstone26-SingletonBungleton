using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Object = UnityEngine.Object;

public class LobbyScene : SceneBase
{
    #region Properties

    // 로비 HUD
    public UI_HUD_LobbyScene Hud { get; private set; }

    // 로비 상태 — 상태 전환 시 이벤트를 발생시키는 허브 (UI_Hud_Lobby 등이 구독)
    public static LobbyState LobbyState
    {
        get => _lobbyState;
        set
        {
            if (_lobbyState == value) return;
            _lobbyState = value;

            switch (_lobbyState)
            {
                case LobbyState.Ready:
                    (Main.Scene.Current as LobbyScene)?.OnLobbyReady?.Invoke();
                    break;

                case LobbyState.Start:
                    (Main.Scene.Current as LobbyScene)?.OnLobbyStart?.Invoke();
                    break;
            }
        }
    }

    #endregion

    #region Fields

    private static LobbyState _lobbyState = LobbyState.None;

    public event Action OnLobbyReady;
    public event Action OnLobbyStart;

    // 소환한 로비 환경 오브젝트 (씬 퇴장 시 정리용)
    private readonly List<GameObject> _spawnedObjects = new();

    #endregion

    #region Scene Lifecycle

    public override async UniTask EnterScene(CancellationToken token)
    {
        // 로딩 화면(전환 오버레이)은 여기서 닫지 않는다.
        // SceneManagerEx.ChangeSceneAsync가 EnterScene 완료 후 finally에서 HideScreenAsync를 호출하므로,
        // 아래 맵 소환 + Spline + HUD가 모두 await로 끝난 뒤에야 로딩이 닫혀 로비가 "딱" 보인다.

        // #1. Addressable "LobbyScene" 라벨이 걸린 로비 환경 오브젝트들을 소환
        //     (Terrain/Water/Environment 등 정적 배경이라 풀링 대신 1회 인스턴스화)
        List<GameObject> prefabs = await Extensions.LoadAssetsByLabelAsync<GameObject>("LobbyScene", token: token);
        foreach (GameObject prefab in prefabs)
        {
            if (prefab == null) continue;
            _spawnedObjects.Add(Object.Instantiate(prefab));
        }

        // #2. 맵 주위를 빙빙 도는 배경 카메라 연출 (런타임 원형 Spline)
        if (_spawnedObjects.Count > 0)
        {
            Bounds mapBounds = CalculateBounds(_spawnedObjects);
            var orbit = new GameObject(nameof(LobbyBackgroundOrbit)).AddComponent<LobbyBackgroundOrbit>();
            orbit.Setup(mapBounds);
            _spawnedObjects.Add(orbit.gameObject);
        }

        Hud = await Extensions.ShowHud<UI_HUD_LobbyScene>();

        // SceneBase는 MonoBehaviour가 아니므로 OnEnable 대신 진입 시점에 초기 상태 설정
        LobbyState = LobbyState.Ready;
    }

    public override void ExitScene()
    {
        // 소환한 로비 환경 오브젝트 정리
        foreach (GameObject go in _spawnedObjects)
        {
            if (go != null) Object.Destroy(go);
        }
        _spawnedObjects.Clear();
    }

    #endregion

    #region Helpers

    // 소환한 오브젝트들의 Renderer를 모두 감싸는 월드 bounds 계산
    private static Bounds CalculateBounds(List<GameObject> objects)
    {
        Bounds bounds = default;
        bool initialized = false;

        foreach (GameObject go in objects)
        {
            if (go == null) continue;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (!initialized) { bounds = r.bounds; initialized = true; }
                else bounds.Encapsulate(r.bounds);
            }
        }

        return bounds;
    }

    #endregion
}

public enum LobbyState
{
    None = -1,
    Ready,
    Start,
}
