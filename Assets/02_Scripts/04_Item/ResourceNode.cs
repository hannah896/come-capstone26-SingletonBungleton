using UnityEngine;

/// <summary>
/// 채집 오브젝트 재생성 시스템
/// 기획서: 숲 3일, 광물 5일, 버섯/베리 1일 주기
/// GatherableObject와 함께 붙임
/// </summary>

[RequireComponent(typeof(GatherableObject))]
public class ResourceNode : MonoBehaviour
{
    [Header("재생성 설정")]
    [Tooltip("재생성까지 걸리는 시간 (초)")]
    public float respawnTime = 60f;

    [Header("비주얼")]
    [Tooltip("채집 완료 시 비활성화할 메시")]
    public GameObject visualObject;
    [Tooltip("재생성 중 표시할 이펙트 (선택)")]
    public GameObject respawningEffect;

    private GatherableObject _gatherable;
    private bool _isDepleted = false;
    private float _respawnTimer = 0f;

    private void Awake()
    {
        _gatherable = GetComponent<GatherableObject>();
    }

    private void Update()
    {
        if (!_isDepleted) return;

        _respawnTimer += Time.deltaTime;

        if (_respawnTimer >= respawnTime)
            Respawn();
    }

    public void OnDepleted()
    {
        _isDepleted = true;
        _respawnTimer = 0f;

        // 비주얼 숨기기
        if (visualObject != null)
            visualObject.SetActive(false);

        // 재생성 이펙트 표시
        if (respawningEffect != null)
            respawningEffect.SetActive(true);

        Debug.Log($"[자원] {gameObject.name} 고갈 → {respawnTime}초 후 재생성");
    }

    private void Respawn()
    {
        _isDepleted = false;
        _respawnTimer = 0f;

        // 비주얼 다시 표시
        if (visualObject != null)
            visualObject.SetActive(true);

        // 재생성 이펙트 숨기기
        if (respawningEffect != null)
            respawningEffect.SetActive(false);

        // GatherableObject 히트 카운트 초기화
        _gatherable.ResetHits();

        Debug.Log($"[자원] {gameObject.name} 재생성 완료!");
    }

    public void SetRespawnDays(float days)
    {
        respawnTime = days * 24f * 60f; // 일->초 변환
    }
}