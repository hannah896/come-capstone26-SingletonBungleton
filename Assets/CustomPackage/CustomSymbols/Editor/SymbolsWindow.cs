using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

// IActiveBuildTargetChanged를 상속받아 플랫폼 변경을 감지합니다.
public class SymbolsWindow : EditorWindow, IActiveBuildTargetChanged
{
    private const string LastUsedSettingPath = "LastUsedSettingPath";

    [SerializeField]
    private CustomSymbolsSO _settingSO;
    private SerializedObject _serializedSettings;
    private Vector2 _scrollPosition = Vector2.zero;

    // 인터페이스 구현: 콜백 순서
    public int callbackOrder => 0;

    [MenuItem("Tools/Custom Symbols")]
    public static void ShowWindow() => GetWindow<SymbolsWindow>("Custom Symbols").Show();

    private void OnEnable()
    {
        string lastUsedPath = EditorPrefs.GetString(LastUsedSettingPath, "");
        if (!string.IsNullOrEmpty(lastUsedPath))
        {
            _settingSO = AssetDatabase.LoadAssetAtPath<CustomSymbolsSO>(lastUsedPath);
            if (_settingSO != null) _serializedSettings = new SerializedObject(_settingSO);
        }
    }

    // 플랫폼이 변경되었을 때 실행되는 콜백
    public void OnActiveBuildTargetChanged(BuildTarget previousTarget, BuildTarget newTarget)
    {
        if (_settingSO == null)
        {
            string path = EditorPrefs.GetString(LastUsedSettingPath, "");
            _settingSO = AssetDatabase.LoadAssetAtPath<CustomSymbolsSO>(path);
        }
        if (_settingSO != null)
        {
            ApplySymbolsToPlatform(newTarget);
            Debug.Log($"[Symbols] 플랫폼 변경 감지: {newTarget}에 맞춰 심볼이 업데이트되었습니다.");
        }
    }

    private void OnGUI()
    {
        OnGUI_Settings();
        if (_settingSO == null) return;
        _serializedSettings?.Update();
        EditorGUILayout.Space(10);

        // --- 기능 버튼부 ---
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("현재 프로젝트 심볼 가져오기", GUILayout.Height(30))) SyncFromProject();
        if (GUILayout.Button("현재 플랫폼에 강제 적용", GUILayout.Height(30))) ApplySymbolsToPlatform(EditorUserBuildSettings.activeBuildTarget);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(10);

        // --- 메인 테이블 헤더 ---
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
        DrawSymbolTable();
        EditorGUILayout.EndScrollView();
        _serializedSettings?.ApplyModifiedProperties();
    }

    private void DrawSymbolTable()
    {
        EditorGUILayout.BeginVertical("box");

        // --- 헤더 부분 (생략 없이 유지) ---
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("Symbol Name", GUILayout.Width(290));
        GUILayout.Label("All", EditorStyles.toolbarButton, GUILayout.Width(44));
        GUILayout.Label("Win", EditorStyles.toolbarButton, GUILayout.Width(44));
        GUILayout.Label("Mac", EditorStyles.toolbarButton, GUILayout.Width(44));
        GUILayout.Label("AOS", EditorStyles.toolbarButton, GUILayout.Width(44));
        GUILayout.Label("iOS", EditorStyles.toolbarButton, GUILayout.Width(44));
        GUILayout.Label("");
        EditorGUILayout.EndHorizontal();
        int indexToRemove = -1; // 삭제할 인덱스를 저장할 변수
        for (int i = 0; i < _settingSO.customAllSymbols.Count; i++)
        {
            string symbol = _settingSO.customAllSymbols[i];
            EditorGUILayout.BeginHorizontal();

            // 이름 수정
            EditorGUI.BeginChangeCheck();
            string newName = EditorGUILayout.TextField(symbol, GUILayout.Width(298));
            if (EditorGUI.EndChangeCheck())
            {
                UpdateSymbolNameEverywhere(symbol, newName);
                _settingSO.customAllSymbols[i] = newName;
            }
            GUILayout.Space(10);

            // 1. All 체크박스
            bool isAll = IsSymbolInAllPlatforms(symbol);
            EditorGUI.BeginChangeCheck();
            bool nextAll = EditorGUILayout.Toggle(isAll, GUILayout.Width(41));
            if (EditorGUI.EndChangeCheck())
            {
                SetSymbolInAllPlatforms(symbol, nextAll);
            }

            // 2. 개별 플랫폼 체크박스
            ToggleSymbolInList(symbol, _settingSO.windowPlatformSymbols, 41);
            ToggleSymbolInList(symbol, _settingSO.macPlatformSymbols, 41);
            ToggleSymbolInList(symbol, _settingSO.aosPlatformSymbols, 41);
            ToggleSymbolInList(symbol, _settingSO.iosPlatformSymbols, 41);

            // 삭제 버튼 클릭 시 인덱스만 기록
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                indexToRemove = i;
            }
            EditorGUILayout.EndHorizontal();
        }

        // --- 루프가 끝난 "밖에서" 안전하게 삭제 처리 ---
        if (indexToRemove != -1)
        {
            string symbolToDelete = _settingSO.customAllSymbols[indexToRemove];
            RemoveSymbolFromEverywhere(symbolToDelete);
            _settingSO.customAllSymbols.RemoveAt(indexToRemove);
            EditorUtility.SetDirty(_settingSO);
            AssetDatabase.SaveAssets();

            // 삭제 후 레이아웃 충돌을 방지하기 위해 GUI를 바로 종료하고 다음 프레임에 다시 그림
            GUIUtility.ExitGUI();
        }
        if (GUILayout.Button("+ Add New Symbol", GUILayout.Height(25)))
        {
            _settingSO.customAllSymbols.Add("NEW_SYMBOL");
            EditorUtility.SetDirty(_settingSO);
        }
        EditorGUILayout.EndVertical();
    }
    
    // 텍스트 필드에서 이름을 수정했을 때 다른 리스트들의 이름도 동기화해주는 헬퍼
    private void UpdateSymbolNameEverywhere(string oldName, string newName)
    {
        UpdateNameInList(_settingSO.allPlatformSymbols, oldName, newName);
        UpdateNameInList(_settingSO.windowPlatformSymbols, oldName, newName);
        UpdateNameInList(_settingSO.macPlatformSymbols, oldName, newName);
        UpdateNameInList(_settingSO.aosPlatformSymbols, oldName, newName);
        UpdateNameInList(_settingSO.iosPlatformSymbols, oldName, newName);
    }
    
    private void UpdateNameInList(List<string> list, string oldName, string newName)
    {
        int idx = list.IndexOf(oldName);
        if (idx != -1) list[idx] = newName;
    }

    private void ToggleSymbolInList(string symbol, List<string> list, float width)
    {
        bool exists = list.Contains(symbol);
        EditorGUI.BeginChangeCheck();
        bool nextVal = EditorGUILayout.Toggle(exists, GUILayout.Width(width));
        if (EditorGUI.EndChangeCheck())
        {
            if (nextVal)
                list.Add(symbol);
            else
            {
                list.Remove(symbol);
                // 개별 플랫폼이 하나라도 해제되면 AllPlatform 리스트에서도 제거
                _settingSO.allPlatformSymbols.Remove(symbol);
            }
            EditorUtility.SetDirty(_settingSO);
        }
    }

    // 4개 플랫폼에 모두 포함되어 있는지 확인 (All 체크박스 상태 결정)
    private bool IsSymbolInAllPlatforms(string symbol)
    {
        return _settingSO.allPlatformSymbols.Contains(symbol) ||
               (_settingSO.windowPlatformSymbols.Contains(symbol) &&
                _settingSO.macPlatformSymbols.Contains(symbol) &&
                _settingSO.aosPlatformSymbols.Contains(symbol) &&
                _settingSO.iosPlatformSymbols.Contains(symbol));
    }

    // All 체크박스를 조작했을 때 4개 플랫폼 일괄 적용/해제
    private void SetSymbolInAllPlatforms(string symbol, bool activate)
    {
        if (activate)
        {
            if (!_settingSO.allPlatformSymbols.Contains(symbol)) _settingSO.allPlatformSymbols.Add(symbol);
            if (!_settingSO.windowPlatformSymbols.Contains(symbol)) _settingSO.windowPlatformSymbols.Add(symbol);
            if (!_settingSO.macPlatformSymbols.Contains(symbol)) _settingSO.macPlatformSymbols.Add(symbol);
            if (!_settingSO.aosPlatformSymbols.Contains(symbol)) _settingSO.aosPlatformSymbols.Add(symbol);
            if (!_settingSO.iosPlatformSymbols.Contains(symbol)) _settingSO.iosPlatformSymbols.Add(symbol);
        }
        else
        {
            _settingSO.allPlatformSymbols.Remove(symbol);
            _settingSO.windowPlatformSymbols.Remove(symbol);
            _settingSO.macPlatformSymbols.Remove(symbol);
            _settingSO.aosPlatformSymbols.Remove(symbol);
            _settingSO.iosPlatformSymbols.Remove(symbol);
        }
        EditorUtility.SetDirty(_settingSO);
    }

    // 삭제 버튼 클릭 시 모든 리스트에서 제거하는 헬퍼 함수
    private void RemoveSymbolFromEverywhere(string symbol)
    {
        _settingSO.allPlatformSymbols.Remove(symbol);
        _settingSO.windowPlatformSymbols.Remove(symbol);
        _settingSO.macPlatformSymbols.Remove(symbol);
        _settingSO.aosPlatformSymbols.Remove(symbol);
        _settingSO.iosPlatformSymbols.Remove(symbol);
    }

    // 프로젝트에서 현재 설정된 심볼 긁어오기
    private void SyncFromProject()
    {
        string currentSymbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
        string[] splitSymbols = currentSymbols.Split(';');
        foreach (var s in splitSymbols)
        {
            string trimmed = s.Trim();
            if (!string.IsNullOrEmpty(trimmed) && !_settingSO.customAllSymbols.Contains(trimmed))
            {
                _settingSO.customAllSymbols.Add(trimmed);
            }
        }
        EditorUtility.SetDirty(_settingSO);
    }

    // 실제 플랫폼에 적용하는 로직
    private void ApplySymbolsToPlatform(BuildTarget target)
    {
        BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
        List<string> resultSymbols = new List<string>(_settingSO.allPlatformSymbols);

        // 대상 플랫폼에 따른 추가 심볼 합치기
        switch (target)
        {
            case BuildTarget.StandaloneWindows:
            case BuildTarget.StandaloneWindows64:
                resultSymbols.AddRange(_settingSO.windowPlatformSymbols);
                break;
            case BuildTarget.StandaloneOSX:
                resultSymbols.AddRange(_settingSO.macPlatformSymbols);
                break;
            case BuildTarget.Android:
                resultSymbols.AddRange(_settingSO.aosPlatformSymbols);
                break;
            case BuildTarget.iOS:
                resultSymbols.AddRange(_settingSO.iosPlatformSymbols);
                break;
        }

        // 중복 제거 및 적용
        string joined = string.Join(";", resultSymbols.Distinct());
        PlayerSettings.SetScriptingDefineSymbolsForGroup(group, joined);
    }

    private void OnGUI_Settings()
    {
        // ... (기존 OnGUI_Settings 코드와 동일하되, _serializedSettings 할당 부분 유지)
        EditorGUILayout.Space(10);
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        _settingSO = (CustomSymbolsSO)EditorGUILayout.ObjectField(_settingSO, typeof(CustomSymbolsSO), false);
        if (EditorGUI.EndChangeCheck() && _settingSO != null)
        {
            _serializedSettings = new SerializedObject(_settingSO);
            EditorPrefs.SetString(LastUsedSettingPath, AssetDatabase.GetAssetPath(_settingSO));
        }
        if (GUILayout.Button("Create New", GUILayout.Width(80f)))
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Symbols Settings", "SymbolsSettings", "asset", "");
            if (!string.IsNullOrEmpty(path))
            {
                CustomSymbolsSO newSettings = CreateInstance<CustomSymbolsSO>();
                AssetDatabase.CreateAsset(newSettings, path);
                AssetDatabase.SaveAssets();
                _settingSO = newSettings;
                _serializedSettings = new SerializedObject(_settingSO);
                EditorPrefs.SetString(LastUsedSettingPath, path);
            }
        }
        EditorGUILayout.EndHorizontal();
    }
}