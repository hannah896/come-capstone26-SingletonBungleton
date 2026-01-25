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
    private LocalizedEnumSo _searchDataAsset;
    private string searchDataPath = "Assets/@ScriptableObjects/LocalizedEnum/LocalizedEnumSo.asset";
    private string csvPath = "Assets/Localization/base.csv";
    private string outputRelativePath = "Assets/Localization/ELocalizedName.cs";
    private string enumName = "ELocalizedName";
    private Vector2 scroll;
    private List<string> previewNames = new List<string>();
    private string statusMessage = "";
    private Dictionary<string, string> memberToEnglish = new Dictionary<string, string>(StringComparer.Ordinal);

    [MenuItem("Tools/Localization/Generate ELocalizedName")]
    public static void OpenWindow()
    {
        var window = GetWindow<LocalizationEnumGenerator>("Localization Enum Generator");
        window.LoadSearchDataAsset();
    }
    
    private void LoadSearchDataAsset()
    {
        _searchDataAsset = AssetDatabase.LoadAssetAtPath<LocalizedEnumSo>(searchDataPath);
        if (_searchDataAsset == null)
        {
            statusMessage = "LocalizationSearchData 에셋을 찾을 수 없습니다. Generate를 눌러 새로 생성하세요.";
        }
    }

private void OnGUI()
    {
        GUILayout.Label("CSV → enum 변환기 (CSV에 없는 기존 키는 제거)", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");
        csvPath = EditorGUILayout.TextField("CSV 파일 경로", csvPath);
        outputRelativePath = EditorGUILayout.TextField("출력 파일 (프로젝트 경로)", outputRelativePath);
        enumName = EditorGUILayout.TextField("생성할 enum 이름", enumName);
        
        // ▼▼▼ 수정된 부분 ▼▼▼
        EditorGUILayout.LabelField("검색 데이터 에셋", EditorStyles.boldLabel);
        searchDataPath = EditorGUILayout.TextField("데이터 에셋 경로", searchDataPath);
        
        // 연결된 에셋을 표시 (수동 연결도 가능하게)
        _searchDataAsset = (LocalizedEnumSo)EditorGUILayout.ObjectField(
            "Search Data Asset", 
            _searchDataAsset, 
            typeof(LocalizedEnumSo), 
            false);
        
        if (_searchDataAsset == null)
        {
            EditorGUILayout.HelpBox("검색 데이터 에셋이 없습니다. Generate 시 지정된 경로에 새로 생성됩니다.", MessageType.Info);
        }
        // ▲▲▲ 수정된 부분 ▲▲▲

        if (GUILayout.Button("Preview (파싱해서 미리보기)"))
        {
            TryParseCsvAndPreview();
        }
        // ... (이하 OnGUI의 Preview/Generate 버튼 로직은 동일)
        if (previewNames.Count > 0)
        {
            EditorGUILayout.LabelField("Preview (" + previewNames.Count + ")", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(200));
            foreach (var p in previewNames)
                EditorGUILayout.LabelField(p, EditorStyles.label, GUILayout.MinWidth(10));
            EditorGUILayout.EndScrollView();
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Generate (생성)"))
        {
            // 안전장치 알림
            if (EditorUtility.DisplayDialog("Enum 생성/병합 확인",
                    "CSV에 없는 기존 enum 키들은 최종 파일에서 제거됩니다. 계속하시겠습니까?\n(권장: 기존 ELocalizedName.cs 백업)", "계속", "취소"))
            {
                GenerateEnumFile();
            }
        }

        if (!string.IsNullOrEmpty(statusMessage))
        {
            EditorGUILayout.HelpBox(statusMessage, MessageType.None);
        }

        EditorGUILayout.EndVertical();
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
    private void TryParseCsvAndPreview()
    {
        statusMessage = "";
        previewNames.Clear();
        memberToEnglish.Clear();

        string assetsPath = Directory.GetCurrentDirectory();
        
        string csvFullPath = Path.Combine(assetsPath, csvPath);

        if (!File.Exists(csvFullPath))
        {
            statusMessage = $"CSV 파일을 찾을 수 없습니다: {csvFullPath}";
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

        Dictionary<string, int> merged = new Dictionary<string, int>(StringComparer.Ordinal);
        
        try
        {
            string outPath;
            if (outputRelativePath.StartsWith("Assets"))
            {
                outPath = Path.Combine(Directory.GetCurrentDirectory(), outputRelativePath);
            }
            else
            {
                outPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", outputRelativePath); // Assets/ 추가
            }

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
    
    // ▼▼▼ 핵심 추가 함수 ▼▼▼
    private void UpdateSearchDataAsset(Dictionary<string, int> mergedEnumMap, Dictionary<string, string> englishMap)
    {
        // 1. 에셋 로드 또는 생성
        if (_searchDataAsset == null)
        {
            _searchDataAsset = AssetDatabase.LoadAssetAtPath<LocalizedEnumSo>(searchDataPath);
        }
        
        if (_searchDataAsset == null)
        {
            _searchDataAsset = ScriptableObject.CreateInstance<LocalizedEnumSo>();
            
            var dir = Path.GetDirectoryName(searchDataPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            
            AssetDatabase.CreateAsset(_searchDataAsset, searchDataPath);
            statusMessage += $"\n검색 에셋 생성: {searchDataPath}";
        }
        
        // 2. 데이터 업데이트
        _searchDataAsset.DictValueToString.Clear();
        
        // NONE = 0 추가
        _searchDataAsset.DictValueToString[0] = "None"; 
        
        foreach (var pair in mergedEnumMap) // pair.Key = Enum이름(string), pair.Value = Enum값(int)
        {
            if (pair.Value == 0) continue; // NONE은 이미 처리
            
            if (englishMap.TryGetValue(pair.Key, out string englishText))
            {
                _searchDataAsset.DictValueToString[pair.Value] = englishText;
            }
            else
            {
                // 영문 텍스트가 없는 경우
                _searchDataAsset.DictValueToString[pair.Value] = ""; 
            }
        }
        
        // 3. 변경사항 저장
        EditorUtility.SetDirty(_searchDataAsset);
        AssetDatabase.SaveAssets();
        statusMessage += "\n검색 데이터 에셋 업데이트 완료.";
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