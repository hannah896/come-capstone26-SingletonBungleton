using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class Editor_MapBaker : EditorWindow
{
    private MapBakerSettings settings;
    private Tilemap floorTilemap;
    private GameObject objectContainer;
    private Vector2 scrollPosition;
    private int macroRuleMaxIterations = 50;

    [MenuItem("Tools/Map Baker")]
    public static void ShowWindow() => GetWindow<Editor_MapBaker>("Map Baker");

    private void OnGUI()
    {
        settings = (MapBakerSettings)EditorGUILayout.ObjectField("Settings Asset", settings, typeof(MapBakerSettings), false);
        if (settings == null) return;

        floorTilemap = (Tilemap)EditorGUILayout.ObjectField("Floor Tilemap", floorTilemap, typeof(Tilemap), true);
        objectContainer = (GameObject)EditorGUILayout.ObjectField("Object Container", objectContainer, typeof(GameObject), true);

        EditorGUILayout.Space(8);
        macroRuleMaxIterations = EditorGUILayout.IntSlider("Macro Rule Iterations", macroRuleMaxIterations, 1, 100);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        SerializedObject so = new SerializedObject(settings);
        so.Update();
        EditorGUILayout.PropertyField(so.FindProperty("MapImage"));
        EditorGUILayout.PropertyField(so.FindProperty("MapSize"));
        EditorGUILayout.PropertyField(so.FindProperty("FloorMappings"), true);
        EditorGUILayout.PropertyField(so.FindProperty("ObjectMappings"), true);
        so.ApplyModifiedProperties();
        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("Bake Floor", GUILayout.Height(30))) BakeFloor();
        if (GUILayout.Button("Bake Objects", GUILayout.Height(30))) BakeObjects();
    }

    private void BakeFloor()
    {
        if (!floorTilemap || !settings.MapImage) return;
        floorTilemap.ClearAllTiles();
        float bW = (float)settings.MapImage.width / settings.MapSize.x;
        float bH = (float)settings.MapImage.height / settings.MapSize.y;

        for (int y = 0; y < settings.MapSize.y; y++)
            for (int x = 0; x < settings.MapSize.x; x++)
            {
                Color c = settings.MapImage.GetPixel(Mathf.FloorToInt(x * bW), Mathf.FloorToInt(y * bH));
                FloorMapping best = null; float minD = float.MaxValue;
                foreach (var m in settings.FloorMappings)
                {
                    float d = Vector4.Distance(new Color(c.r, c.g, c.b, 1), new Color(m.Color.r, m.Color.g, m.Color.b, 1));
                    if (d < minD) { minD = d; best = m; }
                }
                if (best != null) floorTilemap.SetTile(new Vector3Int(x, y, 0), best.Tile);
            }
    }

    private void BakeObjects()
    {
        if (!objectContainer || !settings.MapImage) return;
        for (int i = objectContainer.transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(objectContainer.transform.GetChild(i).gameObject);

        // 부모에 Grid가 없다면 추가하여 기준점 확보
        Grid parentGrid = objectContainer.GetComponent<Grid>();
        if (!parentGrid) parentGrid = objectContainer.AddComponent<Grid>();
        parentGrid.cellLayout = GridLayout.CellLayout.Isometric;
        parentGrid.cellSize = new Vector3(1, 0.5f, 1);

        int w = settings.MapSize.x, h = settings.MapSize.y;
        int mw = w / 2, mh = h / 2;
        int[,] macro = new int[mw, mh];
        float bW = (float)settings.MapImage.width / w, bH = (float)settings.MapImage.height / h;

        for (int my = 0; my < mh; my++)
            for (int mx = 0; mx < mw; mx++)
            {
                macro[mx, my] = -1;
                Color avg = SampleAvgColor2x2(mx, my, bW, bH);
                avg.a = 1f;
                for (int i = 0; i < settings.ObjectMappings.Count; i++)
                {
                    var m = settings.ObjectMappings[i];
                    if (Vector4.Distance(avg, new Color(m.Color.r, m.Color.g, m.Color.b, 1)) < m.Sensitivity)
                    {
                        macro[mx, my] = i; break;
                    }
                }
            }

        // 보정 로직
        for (int it = 0; it < macroRuleMaxIterations; it++)
        {
            bool changed = false; int[,] next = (int[,])macro.Clone();
            for (int y = 0; y < mh; y++) for (int x = 0; x < mw; x++)
                {
                    int cur = macro[x, y], l = (x > 0) ? macro[x - 1, y] : -2, r = (x < mw - 1) ? macro[x + 1, y] : -2, d = (y > 0) ? macro[x, y - 1] : -2, u = (y < mh - 1) ? macro[x, y + 1] : -2;
                    if (cur == -1)
                    {
                        int fill = (l >= 0 && l == r) ? l : (d >= 0 && d == u) ? d : -1;
                        if (fill == -1) fill = Majority3Of4(l, r, d, u);
                        if (fill != -1) { next[x, y] = fill; changed = true; }
                    }
                    else if (l != cur && r != cur && d != cur && u != cur) { next[x, y] = -1; changed = true; }
                }
            macro = next; if (!changed) break;
        }

        int[,] finalGrid = new int[w, h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                finalGrid[x, y] = macro[Mathf.Clamp(x / 2, 0, mw - 1), Mathf.Clamp(y / 2, 0, mh - 1)];

        GenerateUnits(finalGrid, parentGrid);
    }

    private void GenerateUnits(int[,] grid, Grid parentGrid)
    {
        int w = settings.MapSize.x, h = settings.MapSize.y;
        bool[,] visited = new bool[w, h];
        Dictionary<int, Transform> groupParents = new Dictionary<int, Transform>();

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int mapIdx = grid[x, y];
                if (mapIdx == -1 || visited[x, y]) continue;

                var m = settings.ObjectMappings[mapIdx];
                List<Vector2Int> cells = new List<Vector2Int>();

                if (m.Mass) FloodFill(x, y, mapIdx, grid, visited, cells);
                else { cells.Add(new Vector2Int(x, y)); visited[x, y] = true; }

                if (!groupParents.ContainsKey(mapIdx))
                {
                    GameObject p = new GameObject($"Group_{m.Name}");
                    p.transform.SetParent(objectContainer.transform);
                    p.transform.localPosition = Vector3.zero;
                    groupParents[mapIdx] = p.transform;
                }
                CreateUnit(cells, m, groupParents[mapIdx], parentGrid);
            }
    }

    private void CreateUnit(List<Vector2Int> cells, ObjectMapping m, Transform p, Grid parentGrid)
    {
        GameObject go = new GameObject($"{m.Name}_{(m.Mass ? "Mass" : "Single")}_{cells[0].x}_{cells[0].y}");
        go.transform.SetParent(p);

        // 1. 유닛의 위치를 첫 번째 타일의 월드 좌표로 설정
        Vector3 worldPos = parentGrid.CellToWorld(new Vector3Int(cells[0].x, cells[0].y, 0));
        go.transform.position = worldPos;

        Tilemap tm = go.AddComponent<Tilemap>();
        go.AddComponent<TilemapRenderer>().sortOrder = TilemapRenderer.SortOrder.TopLeft;

        // 2. 타일을 유닛 기준으로 배치 (상대 좌표 사용)
        foreach (var c in cells)
        {
            // 유닛이 이미 worldPos에 있으므로, 내부 타일은 cells[0]과의 차이만큼만 배치
            Vector3Int relativePos = new Vector3Int(c.x - cells[0].x, c.y - cells[0].y, 0);
            tm.SetTile(relativePos, m.Tile);
        }

        go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        go.AddComponent<TilemapCollider2D>().compositeOperation = Collider2D.CompositeOperation.Merge;
        go.AddComponent<CompositeCollider2D>().geometryType = CompositeCollider2D.GeometryType.Outlines;

        if (!go.TryGetComponent(out Build_ObjectType bot)) bot = go.AddComponent<Build_ObjectType>();
        bot.Init(m.Type, cells.Count);
    }

    private void FloodFill(int x, int y, int idx, int[,] grid, bool[,] visited, List<Vector2Int> cells)
    {
        Stack<Vector2Int> s = new Stack<Vector2Int>(); s.Push(new Vector2Int(x, y));
        while (s.Count > 0)
        {
            Vector2Int c = s.Pop();
            if (c.x < 0 || c.x >= settings.MapSize.x || c.y < 0 || c.y >= settings.MapSize.y) continue;
            if (visited[c.x, c.y] || grid[c.x, c.y] != idx) continue;
            visited[c.x, c.y] = true; cells.Add(c);
            s.Push(new Vector2Int(c.x + 1, c.y)); s.Push(new Vector2Int(c.x - 1, c.y));
            s.Push(new Vector2Int(c.x, c.y + 1)); s.Push(new Vector2Int(c.x, c.y - 1));
        }
    }

    private Color SampleAvgColor2x2(int mx, int my, float bw, float bh)
    {
        Color sum = Color.black;
        for (int i = 0; i < 2; i++) for (int j = 0; j < 2; j++)
            {
                int px = Mathf.Clamp(Mathf.FloorToInt((mx * 2 + i + 0.5f) * bw), 0, settings.MapImage.width - 1);
                int py = Mathf.Clamp(Mathf.FloorToInt((my * 2 + j + 0.5f) * bh), 0, settings.MapImage.height - 1);
                sum += settings.MapImage.GetPixel(px, py);
            }
        return sum * 0.25f;
    }

    private int Majority3Of4(int l, int r, int d, int u)
    {
        if (l >= 0 && ((l == r && l == d) || (l == r && l == u) || (l == d && l == u))) return l;
        if (r >= 0 && r == d && r == u) return r;
        return -1;
    }
}