// 파일: Assets/Editor/LocalizationEnumGenerator.cs

#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using UnityEngine;
using UnityEditor;

public class LocalizationEnumGenerator : EditorWindow
{
    #region Constants

    // EditorPrefs 키
    private const string PREF_CSV_FILENAME = "LocalizationEnumGenerator_CsvFileName";
    private const string PREF_ENUM_OUTPUT_FOLDER = "LocalizationEnumGenerator_EnumOutputFolder";
    private const string PREF_ENUM_NAME = "LocalizationEnumGenerator_EnumName";
    private const string PREF_DATA_ASSET_FOLDER = "LocalizationEnumGenerator_DataAssetFolder";

    #endregion

    #region Fields

    // CSV 설정
    private string csvFileName = "base.csv";
    private string _foundCsvPath = "";

    // Enum 출력 설정
    private DefaultAsset _enumOutputFolder;
    private string enumName = "ELocalizedName";

    // 데이터 에셋 설정
    private LocalizedEnumSo _searchDataAsset;
    private DefaultAsset _dataAssetFolder;

    // UI 상태
    private Vector2 scroll;
    private List<string> previewNames = new List<string>();
    private string statusMessage = "";

    // 파싱 데이터
    private Dictionary<string, string> memberToEnglish = new Dictionary<string, string>(StringComparer.Ordinal);

    #endregion

    [MenuItem("Tools/Localization/Generate ELocalizedName")]
    public static void OpenWindow()
    {
        var window = GetWindow<LocalizationEnumGenerator>("Localization Enum Generator");
        window.LoadSearchDataAsset();
    }
    
    private void LoadSearchDataAsset()
    {
        // EditorPrefs에서 저장된 설정 불러오기
        LoadPrefs();

        // LocalizedEnumSo 자동 탐색
        _searchDataAsset = FindAssetInProject<LocalizedEnumSo>();

        if (_searchDataAsset == null)
        {
            statusMessage = "LocalizedEnumSo 에셋을 찾을 수 없습니다. Generate 시 지정된 폴더에 새로 생성됩니다.";
        }
        else
        {
            statusMessage = $"LocalizedEnumSo 발견: {AssetDatabase.GetAssetPath(_searchDataAsset)}";
        }

        // 윈도우 열릴 때 CSV 파일 자동 검색
        _foundCsvPath = FindCsvFileInAssets(csvFileName);
    }

    // EditorPrefs에서 설정 불러오기
    private void LoadPrefs()
    {
        csvFileName = EditorPrefs.GetString(PREF_CSV_FILENAME, "base.csv");
        enumName = EditorPrefs.GetString(PREF_ENUM_NAME, "ELocalizedName");

        // 폴더는 GUID로 저장/복원
        string enumFolderGuid = EditorPrefs.GetString(PREF_ENUM_OUTPUT_FOLDER, "");
        if (!string.IsNullOrEmpty(enumFolderGuid))
        {
            string path = AssetDatabase.GUIDToAssetPath(enumFolderGuid);
            if (!string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path))
            {
                _enumOutputFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
            }
        }

        string dataFolderGuid = EditorPrefs.GetString(PREF_DATA_ASSET_FOLDER, "");
        if (!string.IsNullOrEmpty(dataFolderGuid))
        {
            string path = AssetDatabase.GUIDToAssetPath(dataFolderGuid);
            if (!string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path))
            {
                _dataAssetFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
            }
        }
    }

    // EditorPrefs에 설정 저장
    private void SavePrefs()
    {
        EditorPrefs.SetString(PREF_CSV_FILENAME, csvFileName);
        EditorPrefs.SetString(PREF_ENUM_NAME, enumName);

        // 폴더는 GUID로 저장
        if (_enumOutputFolder != null)
        {
            string path = AssetDatabase.GetAssetPath(_enumOutputFolder);
            string guid = AssetDatabase.AssetPathToGUID(path);
            EditorPrefs.SetString(PREF_ENUM_OUTPUT_FOLDER, guid);
        }
        else
        {
            EditorPrefs.SetString(PREF_ENUM_OUTPUT_FOLDER, "");
        }

        if (_dataAssetFolder != null)
        {
            string path = AssetDatabase.GetAssetPath(_dataAssetFolder);
            string guid = AssetDatabase.AssetPathToGUID(path);
            EditorPrefs.SetString(PREF_DATA_ASSET_FOLDER, guid);
        }
        else
        {
            EditorPrefs.SetString(PREF_DATA_ASSET_FOLDER, "");
        }
    }

    // 윈도우가 닫힐 때 설정 저장
    private void OnDisable()
    {
        SavePrefs();
    }

    // Assets 전체에서 특정 타입의 에셋을 탐색
    private T FindAssetInProject<T>() where T : UnityEngine.Object
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");

        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        return null;
    }

    // 폴더 경로 가져오기 (DefaultAsset → string)
    private string GetFolderPath(DefaultAsset folder)
    {
        if (folder == null) return null;

        string path = AssetDatabase.GetAssetPath(folder);
        if (AssetDatabase.IsValidFolder(path))
        {
            return path;
        }

        return null;
    }

private void OnGUI()
    {
        GUILayout.Label("CSV → Enum 변환기", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        // ========== CSV 파일 설정 ==========
        EditorGUILayout.LabelField("CSV 파일 설정", EditorStyles.boldLabel);
        csvFileName = EditorGUILayout.TextField("CSV 파일 이름", csvFileName);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("검색", GUILayout.Width(60)))
        {
            _foundCsvPath = FindCsvFileInAssets(csvFileName);
        }
        EditorGUILayout.LabelField(_foundCsvPath, EditorStyles.helpBox);
        EditorGUILayout.EndHorizontal();

        if (string.IsNullOrEmpty(_foundCsvPath))
        {
            EditorGUILayout.HelpBox($"'{csvFileName}' 파일을 찾지 못했습니다.", MessageType.Warning);
        }

        EditorGUILayout.Space();

        // ========== Enum 출력 설정 ==========
        EditorGUILayout.LabelField("Enum 출력 설정", EditorStyles.boldLabel);

        _enumOutputFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            "출력 폴더",
            _enumOutputFolder,
            typeof(DefaultAsset),
            false);

        // 폴더 유효성 검사
        if (_enumOutputFolder != null && !AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(_enumOutputFolder)))
        {
            EditorGUILayout.HelpBox("폴더만 선택할 수 있습니다.", MessageType.Error);
            _enumOutputFolder = null;
        }

        enumName = EditorGUILayout.TextField("Enum 이름", enumName);

        // 출력 경로 미리보기
        string enumOutputPath = GetEnumOutputPath();
        if (!string.IsNullOrEmpty(enumOutputPath))
        {
            EditorGUILayout.LabelField("출력 경로", enumOutputPath, EditorStyles.helpBox);
        }
        else
        {
            EditorGUILayout.HelpBox("출력 폴더를 선택해주세요.", MessageType.Warning);
        }

        EditorGUILayout.Space();

        // ========== 데이터 에셋 설정 ==========
        EditorGUILayout.LabelField("LocalizedEnumSo 설정", EditorStyles.boldLabel);

        // 자동 탐색된 에셋 표시
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.ObjectField("탐색된 에셋", _searchDataAsset, typeof(LocalizedEnumSo), false);
        EditorGUI.EndDisabledGroup();

        // 재탐색 버튼
        if (GUILayout.Button("Assets에서 재탐색", GUILayout.Width(120)))
        {
            _searchDataAsset = FindAssetInProject<LocalizedEnumSo>();
            if (_searchDataAsset != null)
            {
                statusMessage = $"LocalizedEnumSo 발견: {AssetDatabase.GetAssetPath(_searchDataAsset)}";
            }
            else
            {
                statusMessage = "LocalizedEnumSo를 찾지 못했습니다.";
            }
        }

        // 에셋이 없을 경우 생성 폴더 선택
        if (_searchDataAsset == null)
        {
            _dataAssetFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                "생성 폴더",
                _dataAssetFolder,
                typeof(DefaultAsset),
                false);

            if (_dataAssetFolder != null && !AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(_dataAssetFolder)))
            {
                EditorGUILayout.HelpBox("폴더만 선택할 수 있습니다.", MessageType.Error);
                _dataAssetFolder = null;
            }

            EditorGUILayout.HelpBox("LocalizedEnumSo가 없습니다. Generate 시 위 폴더에 새로 생성됩니다.", MessageType.Info);
        }

        EditorGUILayout.Space();

        // ========== 버튼 영역 ==========
        if (GUILayout.Button("Preview (미리보기)"))
        {
            if (string.IsNullOrEmpty(_foundCsvPath))
            {
                _foundCsvPath = FindCsvFileInAssets(csvFileName);
            }
            TryParseCsvAndPreview();
        }

        if (previewNames.Count > 0)
        {
            EditorGUILayout.LabelField($"Preview ({previewNames.Count}개)", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(200));
            foreach (var p in previewNames)
                EditorGUILayout.LabelField(p, EditorStyles.label, GUILayout.MinWidth(10));
            EditorGUILayout.EndScrollView();
        }

        EditorGUILayout.Space();

        // Generate 버튼 활성화 조건 확인
        bool canGenerate = !string.IsNullOrEmpty(_foundCsvPath) &&
                           !string.IsNullOrEmpty(GetEnumOutputPath()) &&
                           (_searchDataAsset != null || _dataAssetFolder != null);

        EditorGUI.BeginDisabledGroup(!canGenerate);
        if (GUILayout.Button("Generate (생성)"))
        {
            if (EditorUtility.DisplayDialog("Enum 생성 확인",
                    "CSV에 없는 기존 enum 키들은 제거됩니다. 계속하시겠습니까?", "계속", "취소"))
            {
                GenerateEnumFile();
            }
        }
        EditorGUI.EndDisabledGroup();

        if (!canGenerate)
        {
            EditorGUILayout.HelpBox("Generate 하려면 CSV 파일, 출력 폴더, 데이터 에셋(또는 생성 폴더)이 필요합니다.", MessageType.Info);
        }

        if (!string.IsNullOrEmpty(statusMessage))
        {
            EditorGUILayout.HelpBox(statusMessage, MessageType.None);
        }

        EditorGUILayout.EndVertical();
    }

    // Enum 출력 경로 계산
    private string GetEnumOutputPath()
    {
        string folderPath = GetFolderPath(_enumOutputFolder);
        if (string.IsNullOrEmpty(folderPath) || string.IsNullOrEmpty(enumName))
            return null;

        return $"{folderPath}/{enumName}.cs";
    }

    // CSV 전체 텍스트에서 "레코드"(한 행) 리스트를 추출.
    // 큰따옴표로 감싼 필드 안의 개행은 무시(레코드 내부로 포함)한다.
private static List<string> ParseCsvRecords(string text)
    {
        var records = new List<string>();
        if (text == null) return records;

        var sb = new StringBuilder();
        bool inQuotes = false;
        int i = 0;
        while (i < text.Length)
        {
            char c = text[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < text.Length && text[i + 1] == '"')
                {
                    sb.Append('"');
                    i += 2;
                    continue;
                }
                else
                {
                    inQuotes = !inQuotes;
                    i++;
                    continue;
                }
            }
            if (!inQuotes && (c == '\r' || c == '\n'))
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++; 
                }
                records.Add(sb.ToString());
                sb.Clear();
                i++;
                continue;
            }
            
            sb.Append(c);
            i++;
        }

        if (sb.Length > 0 || text.EndsWith("\n") || text.EndsWith("\r"))
            records.Add(sb.ToString());

        return records;
    }
    // Assets 폴더 내에서 CSV 파일을 이름으로 검색
    private string FindCsvFileInAssets(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            statusMessage = "파일 이름을 입력해주세요.";
            return "";
        }

        // 확장자가 없으면 .csv 추가
        if (!fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".csv";
        }

        // 파일 이름에서 확장자 제거하여 검색
        string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

        // AssetDatabase로 검색 (확장자 없이 검색)
        string[] guids = AssetDatabase.FindAssets(nameWithoutExt);

        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);

            // .csv 파일인지 확인
            if (!assetPath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                continue;

            // 파일 이름이 정확히 일치하는지 확인
            string foundFileName = Path.GetFileName(assetPath);
            if (foundFileName.Equals(fileName, StringComparison.OrdinalIgnoreCase))
            {
                statusMessage = $"CSV 파일 발견: {assetPath}";
                return assetPath;
            }
        }

        statusMessage = $"'{fileName}' 파일을 Assets 폴더에서 찾을 수 없습니다.";
        return "";
    }

    private void TryParseCsvAndPreview()
    {
        statusMessage = "";
        previewNames.Clear();
        memberToEnglish.Clear();

        // _foundCsvPath가 비어있으면 먼저 검색 시도
        if (string.IsNullOrEmpty(_foundCsvPath))
        {
            _foundCsvPath = FindCsvFileInAssets(csvFileName);
        }

        if (string.IsNullOrEmpty(_foundCsvPath))
        {
            statusMessage = $"CSV 파일을 찾을 수 없습니다. 파일 이름을 확인해주세요: {csvFileName}";
            return;
        }

        string assetsPath = Directory.GetCurrentDirectory();
        string csvFullPath = Path.Combine(assetsPath, _foundCsvPath);

        if (!File.Exists(csvFullPath))
        {
            statusMessage = $"CSV 파일이 존재하지 않습니다: {csvFullPath}";
            return;
        }

        try
        {
            var fileText = File.ReadAllText(csvFullPath, Encoding.UTF8);
            var lines = ParseCsvRecords(fileText).ToArray();
            if (lines.Length == 0)
            {
                statusMessage = "CSV 파일이 비어있습니다.";
                return;
            }
            
            int startLine = 0;
            if (lines[0].ToLower().Contains("key") && lines[0].Contains(","))
                startLine = 1;

            var names = new List<string>();
            for (int i = startLine; i < lines.Length; ++i)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                var fields = ParseCsvLine(line);
                if (fields == null || fields.Count == 0) continue;

                string rawKey = fields[0]?.Trim() ?? "";
                if (string.IsNullOrEmpty(rawKey)) continue;

                string memberName = ToMinimalSanitizedEnumName(rawKey);
                if (string.IsNullOrWhiteSpace(memberName)) continue;
                
                string finalName = memberName;
                if (names.Contains(finalName))
                {
                    int idx = 1;
                    do
                    {
                        finalName = memberName + "_" + idx;
                        idx++;
                    } while (names.Contains(finalName));
                }

                names.Add(finalName);

                string english = "";
                if (fields.Count > 2)
                    english = fields[2] ?? "";
                
                english = english.Trim();
                if (english.StartsWith("\"") && english.EndsWith("\"") && english.Length >= 2)
                    english = english.Substring(1, english.Length - 2).Replace("\"\"", "\"");
                
                if (!memberToEnglish.ContainsKey(finalName))
                    memberToEnglish[finalName] = english;
                else
                    memberToEnglish[finalName] = english;
            }
            
            previewNames = names.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
            statusMessage = $"파싱 완료: {previewNames.Count}개의 멤버가 준비되었습니다.";
        }
        catch (Exception ex)
        {
            statusMessage = "파싱 중 오류: " + ex.Message;
        }
    }


    private void GenerateEnumFile()
    {
        TryParseCsvAndPreview();
        if (previewNames == null || previewNames.Count == 0)
        {
            statusMessage = "생성할 멤버가 없습니다. 먼저 Preview를 눌러 확인하세요.";
            return;
        }

        // 출력 경로 확인
        string outputRelativePath = GetEnumOutputPath();
        if (string.IsNullOrEmpty(outputRelativePath))
        {
            statusMessage = "출력 폴더를 선택해주세요.";
            return;
        }

        Dictionary<string, int> merged = new Dictionary<string, int>(StringComparer.Ordinal);

        try
        {
            string outPath = Path.Combine(Directory.GetCurrentDirectory(), outputRelativePath);

            var dir = Path.GetDirectoryName(outPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            
            var existingMapAll = new Dictionary<string, int>(StringComparer.Ordinal);
            if (File.Exists(outPath))
            {
                try
                {
                    var text = File.ReadAllText(outPath, Encoding.UTF8);
                    existingMapAll = ParseEnumFile(text);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("기존 enum 파일 파싱 중 오류: " + ex.Message);
                    existingMapAll = new Dictionary<string, int>(StringComparer.Ordinal);
                }
            }
            
            var desiredKeys = previewNames.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
            
            // 'merged' 딕셔너리를 여기서 채웁니다.
            merged = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var k in desiredKeys)
            {
                if (existingMapAll.TryGetValue(k, out int val))
                {
                    merged[k] = val; // 보존
                }
            }
            
            var usedValues = new HashSet<int>(merged.Values);
            int currentMax = usedValues.Count > 0 ? usedValues.Max() : 0;
            if (currentMax < 0) currentMax = 0;
            
            var existingNamesSorted = merged.Keys.OrderBy(x => x, StringComparer.Ordinal).ToList();
            
            foreach (var key in desiredKeys)
            {
                if (merged.ContainsKey(key))
                    continue; 
                
                string prevName = null, nextName = null;
                int insertIndex = existingNamesSorted.BinarySearch(key, StringComparer.Ordinal);
                if (insertIndex < 0) insertIndex = ~insertIndex;
                if (insertIndex - 1 >= 0) prevName = existingNamesSorted[insertIndex - 1];
                if (insertIndex < existingNamesSorted.Count) nextName = existingNamesSorted[insertIndex];

                int prevVal = int.MinValue;
                int nextVal = int.MaxValue;
                if (prevName != null) prevVal = merged[prevName];
                if (nextName != null) nextVal = merged[nextName];

                int assigned = int.MinValue;
                long startCandidate = (prevVal == int.MinValue) ? 1 : (long)prevVal + 1;
                long endCandidate = (nextVal == int.MaxValue) ? (long)int.MaxValue : (long)nextVal - 1;

                if (startCandidate <= endCandidate)
                {
                    for (long c = startCandidate; c <= endCandidate; ++c)
                    {
                        if (c > int.MaxValue) break;
                        if (!usedValues.Contains((int)c))
                        {
                            assigned = (int)c;
                            break;
                        }
                    }
                }

                if (assigned == int.MinValue)
                {
                    int candidate = currentMax + 1;
                    while (usedValues.Contains(candidate))
                        candidate++;
                    assigned = candidate;
                    currentMax = Math.Max(currentMax, assigned);
                }

                merged[key] = assigned;
                usedValues.Add(assigned);
                existingNamesSorted.Insert(insertIndex, key);
            }
            
            var sb = new StringBuilder();
            sb.AppendLine("//이 파일은 Tool -> Localization 탭에서 생성한 스크립트입니다.");
            sb.AppendLine("//자동 생성: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("using System;");
            sb.AppendLine();
            sb.AppendLine($"public enum {enumName}");
            sb.AppendLine("{");
            sb.AppendLine("    NONE = 0,");
            foreach (var k in desiredKeys)
            {
                if (k.Equals("NONE", StringComparison.OrdinalIgnoreCase)) continue;
                if (!merged.TryGetValue(k, out int val)) continue;

                string english = "";
                if (memberToEnglish != null && memberToEnglish.TryGetValue(k, out var e))
                    english = e ?? "";

                sb.AppendLine($"    {k} = {val}, // \"{EscapeForComment(english)}\"");
            }
            sb.AppendLine("}");
            
            if (File.Exists(outPath))
            {
                try
                {
                    // 기존 백업 파일 정리 (최대 2개 유지)
                    CleanupOldBackups(outPath, maxBackups: 2);

                    // 새 백업 생성
                    var bak = outPath + ".bak_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    File.Copy(outPath, bak);
                }
                catch { }
            }

            File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(true));
            AssetDatabase.Refresh();
            int addedCount = merged.Keys.Except(existingMapAll.Keys).Count();
            int removedCount = existingMapAll.Keys.Except(desiredKeys).Count();
            
            statusMessage = $"생성/병합 성공: {outPath} (추가: {addedCount}, 제거: {removedCount})";
            
            // ▼▼▼ 핵심 추가 로직 ▼▼▼
            UpdateSearchDataAsset(merged, memberToEnglish);
            // ▲▲▲ 핵심 추가 로직 ▲▲▲
        }
        catch (Exception ex)
        {
            statusMessage = "파일 생성 중 오류: " + ex.Message;
        }
    }
    
    // LocalizedEnumSo 에셋 업데이트 (자동 탐색 → 덮어쓰기 또는 새로 생성)
    private void UpdateSearchDataAsset(Dictionary<string, int> mergedEnumMap, Dictionary<string, string> englishMap)
    {
        // 1. 에셋 탐색 또는 생성
        if (_searchDataAsset == null)
        {
            // Assets 전체에서 다시 탐색
            _searchDataAsset = FindAssetInProject<LocalizedEnumSo>();
        }

        // 여전히 없으면 새로 생성
        if (_searchDataAsset == null)
        {
            string folderPath = GetFolderPath(_dataAssetFolder);
            if (string.IsNullOrEmpty(folderPath))
            {
                statusMessage += "\n[오류] LocalizedEnumSo 생성 폴더가 지정되지 않았습니다.";
                return;
            }

            _searchDataAsset = ScriptableObject.CreateInstance<LocalizedEnumSo>();

            string assetPath = $"{folderPath}/LocalizedEnumSo.asset";

            // 폴더가 없으면 생성
            string fullDir = Path.Combine(Directory.GetCurrentDirectory(), folderPath);
            if (!Directory.Exists(fullDir))
                Directory.CreateDirectory(fullDir);

            AssetDatabase.CreateAsset(_searchDataAsset, assetPath);
            statusMessage += $"\nLocalizedEnumSo 생성: {assetPath}";
        }
        else
        {
            statusMessage += $"\nLocalizedEnumSo 덮어쓰기: {AssetDatabase.GetAssetPath(_searchDataAsset)}";
        }

        // 2. 데이터 업데이트
        _searchDataAsset.DictValueToString.Clear();

        // NONE = 0 추가
        _searchDataAsset.DictValueToString[0] = "None";

        foreach (var pair in mergedEnumMap)
        {
            if (pair.Value == 0) continue;

            if (englishMap.TryGetValue(pair.Key, out string englishText))
            {
                _searchDataAsset.DictValueToString[pair.Value] = englishText;
            }
            else
            {
                _searchDataAsset.DictValueToString[pair.Value] = "";
            }
        }

        // 3. 변경사항 저장
        EditorUtility.SetDirty(_searchDataAsset);
        AssetDatabase.SaveAssets();
        statusMessage += "\nLocalizedEnumSo 업데이트 완료.";
    }

private static string EscapeForComment(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "")
            .Replace("\n", "\\n"); // {\n} -> \\n 으로 수정
    }
    private static Dictionary<string, int> ParseEnumFile(string fileText)
    {
        var dict = new Dictionary<string, int>(StringComparer.Ordinal);
        var regex = new Regex(@"^\s*(?:@)?([A-Za-z0-9_]+)\s*=\s*(\d+)\s*,", RegexOptions.Multiline);
        var matches = regex.Matches(fileText);
        foreach (Match m in matches)
        {
            try
            {
                var name = m.Groups[1].Value;
                var val = int.Parse(m.Groups[2].Value);
                if (!dict.ContainsKey(name))
                    dict[name] = val;
            }
            catch { }
        }
        return dict;
    }
    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        if (line == null) return result;
        int i = 0;
        int len = line.Length;
        while (i < len)
        {
            while (i < len && (line[i] == ' ' || line[i] == '\t')) i++;
            if (i < len && line[i] == '"')
            {
                i++;
                var sb = new StringBuilder();
                while (i < len)
                {
                    if (line[i] == '"')
                    {
                        if (i + 1 < len && line[i + 1] == '"')
                        {
                            sb.Append('"');
                            i += 2;
                            continue;
                        }
                        else
                        {
                            i++;
                            break;
                        }
                    }
                    else
                    {
                        sb.Append(line[i]);
                        i++;
                    }
                }
                result.Add(sb.ToString());
                while (i < len && line[i] != ',') i++;
                if (i < len && line[i] == ',') i++;
            }
            else
            {
                int start = i;
                while (i < len && line[i] != ',') i++;
                result.Add(line.Substring(start, i - start).Trim()); // Trim() 추가
                if (i < len && line[i] == ',') i++;
            }
        }
        return result;
    }

    // 오래된 백업 파일 정리
    private static void CleanupOldBackups(string originalFilePath, int maxBackups)
    {
        try
        {
            string directory = Path.GetDirectoryName(originalFilePath);
            string fileName = Path.GetFileName(originalFilePath);

            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return;

            // 백업 파일 패턴: {원본파일명}.bak_*
            string pattern = fileName + ".bak_*";
            var backupFiles = Directory.GetFiles(directory, pattern)
                .OrderBy(f => f) // 파일명에 날짜가 포함되어 있으므로 이름순 정렬 = 시간순
                .ToList();

            // maxBackups 개수를 초과하는 오래된 파일 삭제
            int deleteCount = backupFiles.Count - maxBackups;
            for (int i = 0; i < deleteCount; i++)
            {
                File.Delete(backupFiles[i]);
                Debug.Log($"[LocalizationEnumGenerator] 오래된 백업 삭제: {Path.GetFileName(backupFiles[i])}");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LocalizationEnumGenerator] 백업 정리 중 오류: {e.Message}");
        }
    }

    private static string ToMinimalSanitizedEnumName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;
        raw = raw.Trim();
        if (raw.StartsWith("\"") && raw.EndsWith("\"") && raw.Length >= 2)
            raw = raw.Substring(1, raw.Length - 2);

        var sb = new StringBuilder();
        foreach (char c in raw)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
                sb.Append(c);
            else if (c == ' ' || c == '-') // 공백이나 하이픈은 언더스코어로
                sb.Append('_');
            // 그 외 특수문자는 무시
        }
        
        var candidate = sb.ToString();
        // 연속된 언더스코어 하나로
        candidate = Regex.Replace(candidate, @"_+", "_");
        
        if (candidate.Length == 0) return null;
        if (char.IsDigit(candidate[0]))
            candidate = "_" + candidate;

        var csharpKeywords = new HashSet<string> { "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override", "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while" };
        if (csharpKeywords.Contains(candidate))
            candidate = "@" + candidate;

        return candidate;
    }
}
#endif