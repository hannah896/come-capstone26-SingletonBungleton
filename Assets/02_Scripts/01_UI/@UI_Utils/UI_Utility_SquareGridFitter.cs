using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class UI_Utility_SquareGridFitter : MonoBehaviour
{
    [SerializeField] private GridLayoutGroup _grid;
    [SerializeField] private RectTransform _root;
    [SerializeField] private int _columnCount = 2;
    [SerializeField] private float _spaceingRate = 0;
    [SerializeField] private TextAnchor textAnchor = TextAnchor.UpperCenter;

    private Vector2 _lastSize;

    private void OnEnable()
    {
#if UNITY_EDITOR
        EditorApplication.update += OnEditorUpdate;
#endif
        UpdateGrid();
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= OnEditorUpdate;
#endif
    }

#if UNITY_EDITOR
    private void OnEditorUpdate()
    {
        if (Application.isPlaying || this == null || _root == null) return;
        if (_root.rect.size != _lastSize)
        {
            UpdateGrid();
        }
    }

    private void OnValidate()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall -= UpdateGrid;
            EditorApplication.delayCall += UpdateGrid;
        }
    }
#endif

    private void OnRectTransformDimensionsChange()
    {
        UpdateGrid();
    }

    public void UpdateGrid()
    {
        if (_grid == null || _root == null || !gameObject.activeInHierarchy) return;

        int itemCount = 0;
        foreach (Transform child in _grid.transform)
            if (child.gameObject.activeSelf) itemCount++;

        if (itemCount == 0) return;

        int col = _columnCount;
        int row = Mathf.CeilToInt(itemCount / (float)col);

        float W = _root.rect.width - (_grid.padding.left + _grid.padding.right);
        float H = _root.rect.height - (_grid.padding.top + _grid.padding.bottom);

        if (W <= 0 || H <= 0) return;

        float xFromW = W / (col + (_spaceingRate * (col - 1)));
        float xFromH = H / (row + (_spaceingRate * (row - 1)));
        float finalCellSize = Mathf.Min(xFromW, xFromH);

        float finalSpacingX = (col > 1) ? (W - (finalCellSize * col)) / (col - 1) : 0;
        float finalSpacingY = finalCellSize * _spaceingRate;

        _grid.cellSize = new Vector2(finalCellSize, finalCellSize);
        _grid.spacing = new Vector2(finalSpacingX, finalSpacingY);
        _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        _grid.constraintCount = col;
        _grid.childAlignment = textAnchor;

        _lastSize = _root.rect.size;
    }
}