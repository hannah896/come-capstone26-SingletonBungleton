using System;
using System.Collections;
using Blossom.Preference;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

public class UI_LoadingCanvas : UI_Panel_Popup
{
    private const float LoadingTime = 5f;
    private const float SpawnProgressMultiplier = 0.2f;

    private const float StartPosMinX = -10f;
    private const float StartPosMaxX = -8f;
    private const float StartPosMinY = -4f;
    private const float StartPosMaxY = 4f;

    private const float MoveDistance = 50f;

    private const int ArrowDistanceMin = 2;
    private const int ArrowDistanceMax = 15;

    private UI_Image _imgLoadingBar;
    private PlayPrefs _playPrefs;

    private float _showTime = 0;

    public event Action<float> onChangeProgress;

    private float _spawnProgress = 0;

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;

        _imgLoadingBar = gameObject.FindChild<UI_Image>("Img_Bar_F");
        _imgLoadingBar.SetFill(0);

        return true;
    }

    public void Set()
    {
        Initialize();
        _playPrefs = Prefs.Get<PlayPrefs>();
        _showTime = 0;
        _spawnProgress = 0;
        onChangeProgress += SetProgressBar;
        StartCoroutine(OnLoadingCoroutine());
    }

    private void OnDisable()
    {
        UnregisterUpdate();
    }

    private IEnumerator OnLoadingCoroutine()
    {
        onChangeProgress?.Invoke(_showTime / LoadingTime);
        while (_showTime < LoadingTime || !Main.Loading.IsLoadingSDK)
        {
            yield return null;
            _showTime += Time.deltaTime;
            onChangeProgress?.Invoke(_showTime / LoadingTime);
        }

        Main.Scene.ChangeScene("GameScene");

        // 첫 세션 타임에서 하루가 지난 시점에서 앱 오픈 광고 실행
        if (_playPrefs.FirstSessionTime.GetElapsedTime() > 24 * 60f)
        {
            //Main.Ads.ShowAppOpenAd();
        }
        Destroy(gameObject);
    }

    private void SetProgressBar(float value)
    {
        _imgLoadingBar.SetFill(value);
    }

    private void UnregisterUpdate()
    {
        onChangeProgress -= SetProgressBar;
    }
}
