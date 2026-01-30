using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SO_Build_Building))]
public class Editor_Build_Building : Editor
{
    private const int GridSize = 5;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        SO_Build_Building building = (target as SO_Build_Building);
        if (building == null) return;

        int center = GridSize / 2;
        float btnSize = 35f;

        Rect rect = GUILayoutUtility.GetRect(GridSize * btnSize * 1.5f, GridSize * btnSize * 1.2f);
        Vector2 editorCenter = rect.center;

        for (int y = 0; y < GridSize; y++)
        {
            for (int x = 0; x < GridSize; x++)
            {
                float posX = (x - y) * (btnSize * 1f);
                float posY = (x + y) * (btnSize * 0.5f);

                Rect btnRect = new Rect(editorCenter.x + posX - btnSize / 2f,
                                        rect.y + posY, btnSize+1f, btnSize+1f);

                Vector2Int currentCoord = new Vector2Int(x - center, y - center);
                bool isOccupied = building.Footprint.Exists(c => c == currentCoord);

                Color defaultColor = GUI.color;
                if (currentCoord == Vector2Int.zero) GUI.color = Color.cyan;
                else if (isOccupied) GUI.color = Color.green;

                if (GUI.Button(btnRect, ""))
                {
                    if (!isOccupied) building.Footprint.Add(currentCoord);
                    else if (currentCoord != Vector2Int.zero) building.Footprint.Remove(currentCoord);
                    EditorUtility.SetDirty(building);
                }
                GUI.color = defaultColor;
            }
        }

        GUILayout.Space(5);

        if (GUILayout.Button("Footprint 초기화"))
        {
            building.Footprint.Clear();
            building.Footprint.Add(Vector2Int.zero);
            EditorUtility.SetDirty(building);
        }
    }
}