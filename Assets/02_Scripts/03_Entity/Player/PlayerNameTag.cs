using System;
using TMPro;
using UnityEngine;

/// <summary>
/// 다른 플레이어 캐릭터 머리 위에 이름을 띄우는 이름표.
///
/// 캔버스를 쓰지 않는다. 3D TextMeshPro(월드 오브젝트)를 런타임에 자식으로 만들어 붙이므로
/// 플레이어 프리팹을 건드릴 필요가 없다. <see cref="NetworkPlayerSync"/>가 남의 캐릭터에만 붙인다.
/// (1인칭이라 자기 캐릭터 이름은 보이지 않아도 된다)
///
/// - 월드 오브젝트라 지형/벽 뒤에 가려지고, 멀어지면 자연스럽게 작아진다
/// - 매 프레임 카메라와 같은 방향을 바라보게 회전한다 (빌보드)
/// - 이름은 이름 제공 함수를 주기적으로 조회해 갱신한다 (방 참가 직후 이름이 늦게 도착해도 반영)
/// </summary>
public class PlayerNameTag : MonoBehaviour
{
    #region Fields
    // 머리 꼭대기에서 이름표까지 추가 높이
    private const float HeadOffset = 0.35f;

    // 글자 크기 (3D TMP 기준 — 약 0.3m 높이)
    private const float FontSize = 3f;

    // 이 거리보다 멀면 숨긴다
    private const float MaxVisibleDistance = 30f;

    // 이름 재조회 간격(초)
    private const float NameRefreshInterval = 0.5f;

    private TextMeshPro _text;
    private Func<string> _nameProvider;
    private float _height = 2f;
    private float _refreshTimer;
    private Camera _camera;
    private bool _subscribed;
    #endregion

    /// <summary>
    /// 이름 제공 함수를 연결한다. 풀에서 재사용될 때마다 다시 호출해도 안전하다.
    /// </summary>
    public void Bind(Func<string> nameProvider)
    {
        _nameProvider = nameProvider;

        EnsureText();
        CacheHeight();
        RefreshName();

        if (!_subscribed)
        {
            Main.Loop.OnLateUpdate += OnLateUpdate;
            _subscribed = true;
        }
    }

    // 이름표 텍스트 오브젝트를 한 번만 생성
    private void EnsureText()
    {
        if (_text != null) return;

        var go = new GameObject("NameTag");
        go.layer = 0; // Default — 1인칭 전용 레이어(ViewModel)에 올라가지 않도록
        go.transform.SetParent(transform, false);

        _text = go.AddComponent<TextMeshPro>();
        _text.alignment = TextAlignmentOptions.Center;
        _text.fontSize = FontSize;
        _text.textWrappingMode = TextWrappingModes.NoWrap;
        _text.color = Color.white;
        _text.rectTransform.sizeDelta = new Vector2(10f, 1f);

        // 한글이 깨지지 않도록 UI와 같은 폰트를 사용
        TMP_FontAsset font = FindFont();
        if (font != null) _text.font = font;

        // 밝은 배경에서도 읽히도록 외곽선
        _text.outlineWidth = 0.2f;
        _text.outlineColor = Color.black;
    }

    // TextManager가 로드해 둔 폰트 중 첫 번째를 사용
    private static TMP_FontAsset FindFont()
    {
        FontsSo fonts = Main.Text?.FontSo;
        if (fonts == null || fonts.dictFontsData == null) return null;

        foreach (var pair in fonts.dictFontsData)
        {
            if (pair.Value != null) return pair.Value;
        }
        return null;
    }

    // 캐릭터 키 높이 계산 (CharacterController 기준, 없으면 렌더러 bounds)
    private void CacheHeight()
    {
        if (TryGetComponent(out CharacterController controller))
        {
            _height = controller.center.y + controller.height * 0.5f;
            return;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        _height = bounds.max.y - transform.position.y;
    }

    private void RefreshName()
    {
        if (_text == null || _nameProvider == null) return;

        string playerName = _nameProvider();
        if (_text.text != playerName) _text.text = playerName;
    }

    private void OnLateUpdate(float deltaTime)
    {
        if (_text == null) return;

        _refreshTimer -= deltaTime;
        if (_refreshTimer <= 0f)
        {
            _refreshTimer = NameRefreshInterval;
            RefreshName();
        }

        if (_camera == null) _camera = Camera.main;
        if (_camera == null) return;

        Transform tag = _text.transform;
        tag.position = transform.position + Vector3.up * (_height * transform.lossyScale.y + HeadOffset);

        // 멀거나 이름이 없으면 숨김
        bool visible = !string.IsNullOrEmpty(_text.text) &&
                       (tag.position - _camera.transform.position).sqrMagnitude <= MaxVisibleDistance * MaxVisibleDistance;
        if (_text.enabled != visible) _text.enabled = visible;
        if (!visible) return;

        // 카메라와 같은 방향을 바라보게 (글자가 뒤집히지 않도록 카메라 회전을 그대로 사용)
        tag.rotation = _camera.transform.rotation;
    }

    private void OnDestroy()
    {
        if (_subscribed && Main.Instance != null && Main.Loop != null)
            Main.Loop.OnLateUpdate -= OnLateUpdate;
    }
}
