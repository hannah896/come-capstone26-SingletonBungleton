using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public interface IPreviewVisualizer
{
    void Show(ItemDataSO item);
    void Hide();
    void SetVisible(bool visible);
    void UpdateView(
        Vector3 ghostPosition,
        Quaternion ghostRotation,
        Vector3 gridCenter,
        float gridSize,
        int gridRadius,
        float ghostYOffset,
        bool isValid,
        IPlacementValidator validator);
}


/// <summary>
/// 고스트뷰와 그리드뷰를 관리하여 배치 위치와 유효성을 시각적으로 표현하는 컴포넌트입니다.
/// </summary>
public class PreviewVisualizer : MonoBehaviour, IPreviewVisualizer
{
    [Header("Grid")]
    [SerializeField] private float gridSize = 1f;
    [SerializeField] private int gridRadius = 4;
    [SerializeField] private float ghostYOffset = 0.01f;

    [Header("Ghost Colors")]
    [SerializeField] private Color ghostValidColor = new Color(0f, 1f, 0f, 0.35f);
    [SerializeField] private Color ghostInvalidColor = new Color(1f, 0f, 0f, 0.35f);

    [Header("Grid Colors")]
    [SerializeField] private Color gridValidColor = new Color(0f, 1f, 0f, 0.15f);
    [SerializeField] private Color gridInvalidColor = new Color(1f, 0f, 0f, 0.15f);

    [Header("Optional Materials")]
    [SerializeField] private Material ghostMaterialTemplate;
    [SerializeField] private Material gridMaterialTemplate;

    private readonly List<GameObject> gridCells = new List<GameObject>();

    private readonly GhostMaterialApplier ghostApplier = new();


    private Material gridValidMaterial;
    private Material gridInvalidMaterial;

    private GameObject ghostInstance;
    private ItemDataSO activeItemData;

    private void Awake()
    {
        gridValidMaterial = PlacementMaterialFactory.CreateMaterial(gridMaterialTemplate, gridValidColor);
        gridInvalidMaterial = PlacementMaterialFactory.CreateMaterial(gridMaterialTemplate, gridInvalidColor);
    }

    public void Show(ItemDataSO item)
    {
        activeItemData = item;
        CreateGhostInstance();
        SetVisible(true);
    }

    // 고스트와 그리드 모두 숨기고 고스트 인스턴스는 파괴, 활성 아이템 데이터 초기화
    public void Hide()
    {
        DestroyGhost();
        SetGridActive(false);
        activeItemData = null;
    }

    public void SetVisible(bool visible)
    {
        if (ghostInstance != null)
            ghostInstance.SetActive(visible);

        SetGridActive(visible);
    }

    public void UpdateView(
        Vector3 ghostPosition,
        Quaternion ghostRotation,
        Vector3 gridCenter,
        float gridSize,
        int gridRadius,
        float ghostYOffset,
        bool isValid,
        IPlacementValidator validator)
    {
        if (activeItemData == null)
            return;

        UpdateGhostVisual(ghostPosition, ghostRotation, ghostYOffset, isValid);
        //if()
        UpdateGridVisual(gridCenter, gridSize, gridRadius, ghostYOffset, validator);
    }

    private void CreateGhostInstance()
    {
        Hide();
        if (activeItemData == null || activeItemData.placementPrefab == null) return;

        ghostInstance = Instantiate(activeItemData.placementPrefab);
        ghostInstance.name = $"{activeItemData.placementPrefab.name}_Ghost";

        foreach (Collider collider in ghostInstance.GetComponentsInChildren<Collider>())
            collider.enabled = false;

        ghostApplier.ApplyGhostMaterial(ghostInstance, ghostValidColor);

    }

    private void DestroyGhost()
    {
        if (ghostInstance != null)
            Destroy(ghostInstance);

        ghostApplier.Cleanup();  // 복제 머티리얼 정리 (누수 방지)
    }

    private void UpdateGhostVisual(Vector3 position, Quaternion rotation, float yOffset, bool isValid)
    {
        if (ghostInstance == null)
            return;

        ghostInstance.transform.SetPositionAndRotation(position + Vector3.up * yOffset, rotation);
        ghostApplier.SetTint(isValid ? ghostValidColor : ghostInvalidColor);  
    }

    private void UpdateGridVisual(Vector3 centerPosition, float gridSize, int gridRadius, float yOffset, IPlacementValidator validator)
    {
        int diameter = (gridRadius * 2) + 1;
        int totalCells = diameter * diameter;

        EnsureGridCellCount(totalCells, gridSize);

        int index = 0;
        for (int x = -gridRadius; x <= gridRadius; x++)
        {
            for (int z = -gridRadius; z <= gridRadius; z++)
            {
                Vector3 cellPosition = new Vector3(
                    centerPosition.x + (x * gridSize),
                    centerPosition.y + yOffset,
                    centerPosition.z + (z * gridSize));

                bool cellValid = validator == null || validator.IsCellValid(cellPosition, gridSize);
                GameObject cell = gridCells[index];
                Renderer renderer = cell.GetComponent<Renderer>();

                cell.transform.position = cellPosition;
                renderer.sharedMaterial = cellValid ? gridValidMaterial : gridInvalidMaterial;

                index++;
            }
        }
    }

    private void EnsureGridCellCount(int targetCount, float gridSize)
    {
        while (gridCells.Count < targetCount)
            gridCells.Add(CreateGridCell(gridSize));

        for (int i = 0; i < gridCells.Count; i++)
            gridCells[i].SetActive(i < targetCount);
    }

    private GameObject CreateGridCell(float cellSize)
    {
        GameObject cell = GameObject.CreatePrimitive(PrimitiveType.Quad);
        cell.name = "PlacementGridCell";

        Collider collider = cell.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        cell.transform.SetParent(transform, false);
        cell.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        cell.transform.localScale = new Vector3(cellSize, cellSize, 1f);

        Renderer renderer = cell.GetComponent<Renderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        return cell;
    }

    private void SetGridActive(bool active)
    {
        for (int i = 0; i < gridCells.Count; i++)
            gridCells[i].SetActive(active);
    }
}