// 파일: Assets/Editor/SearchableEnumDrawer.cs
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SearchableEnumAttribute))]
public class SearchableEnumDrawer : PropertyDrawer
{
    // 각 프로퍼티의 상태(검색어, 스크롤 위치, 팝업 표시 여부)를 관리
    private static Dictionary<string, State> _propertyStates = new Dictionary<string, State>();
    private const float PADDING = 2f; // 여백

    // ScriptableObject 캐시
    private static LocalizedEnumSo _searchData;
    private static bool _searchDataLoaded = false;
    private static string _searchDataPath = "Assets/@ScriptableObjects/LocalizedEnum/LocalizedEnumSo.asset";

    // 검색 항목을 관리하기 위한 내부 클래스
    private class SearchItem
    {
        public string DisplayName;  // CamelCase 분리된 이름 (예: Player Attack)
        public int IndexInProperty; // property.enumValueIndex에 사용할 인덱스
        public int Value;           // enum의 실제 int 값 (예: 101)
    }
    
    private class State
    {
        public string SearchString = "";
        public Vector2 ScrollPosition;
        public bool IsDropdownVisible = false;
        
        // ▼▼▼ 추가된 부분 ▼▼▼
        // 텍스트 높이 계산을 캐싱하기 위한 변수
        public float EnglishTextHeight = 0;
        // ▲▲▲ 추가된 부분 ▲▲▲
    }

    // CamelCase 문자열을 "Camel Case" 처럼 공백을 넣어 변환
    private static string SplitCamelCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var sb = new StringBuilder();
        sb.Append(input[0]); // 첫 글자는 그대로 추가

        for (int i = 1; i < input.Length; i++)
        {
            char current = input[i];
            char prev = input[i - 1];

            // 현재 문자가 대문자이고, 바로 이전 문자가 소문자일 경우에만 공백을 추가
            if (char.IsUpper(current) && char.IsLower(prev))
            {
                sb.Append(' ');
            }

            sb.Append(current);
        }
        return sb.ToString();
    }
    
    // ScriptableObject 로드 함수
    private void LoadSearchData()
    {
        if (_searchDataLoaded) return;

        _searchData = AssetDatabase.LoadAssetAtPath<LocalizedEnumSo>(_searchDataPath);
        if (_searchData == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:LocalizedEnumSo");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _searchData = AssetDatabase.LoadAssetAtPath<LocalizedEnumSo>(path);
            }
        }
        _searchDataLoaded = true;
    }
    
    // ▼▼▼ 추가된 부분 ▼▼▼
    // 현재 선택된 값의 영문 텍스트를 가져오는 헬퍼 함수
    private string GetCurrentEnglishValue(SerializedProperty property)
    {
        LoadSearchData(); // 데이터 로드 보장
        
        if (_searchData == null || property.enumValueIndex < 0 || property.enumValueIndex >= property.enumNames.Length)
            return null;
        
        try
        {
            // property.enumNames[index]는 Enum의 '이름' (예: "Adspop_Remove_Ads")
            int value = (int)Enum.Parse(fieldInfo.FieldType, property.enumNames[property.enumValueIndex]);
            
            if (_searchData.DictValueToString.TryGetValue(value, out string englishText) && !string.IsNullOrEmpty(englishText))
            {
                return englishText;
            }
        }
        catch (Exception) { return null; } // Enum 파싱 실패 등
        return null;
    }
    // ▲▲▲ 추가된 부분 ▲▲▲

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.Enum)
        {
            EditorGUI.LabelField(position, label.text, "SearchableEnum은 Enum 타입에만 사용할 수 있습니다.");
            return;
        }
        
        bool isSerializable = fieldInfo.IsPublic || fieldInfo.GetCustomAttribute<SerializeField>() != null;
        if (!isSerializable)
        {
            EditorGUI.HelpBox(position, $"'{fieldInfo.Name}' 필드에 [SerializeField]가 없어 값이 저장되지 않습니다.", MessageType.Error);
            return;
        }
        
        LoadSearchData(); // 검색 데이터 로드

        string propertyPath = property.propertyPath;
        if (!_propertyStates.TryGetValue(propertyPath, out State state))
        {
            state = new State();
            _propertyStates[propertyPath] = state;
        }
        
        EditorGUI.BeginProperty(position, label, property);

        // 1. 현재 선택된 값을 보여주는 메인 버튼
        Rect buttonRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        string currentDisplayName = SplitCamelCase(property.enumDisplayNames[property.enumValueIndex]);
        
        if (GUI.Button(buttonRect, new GUIContent(label.text + ": " + currentDisplayName), EditorStyles.popup))
        {
            state.IsDropdownVisible = !state.IsDropdownVisible;
            if (state.IsDropdownVisible)
            {
                GUI.FocusControl(propertyPath + "_SearchField");
            }
        }

        // 2. 팝업이 열렸을 때 UI 그리기
        if (state.IsDropdownVisible)
        {
            string[] enumNames = property.enumDisplayNames;
            
            var allItems = new List<SearchItem>();
            for(int i = 0; i < enumNames.Length; i++)
            {
                int value = (int)Enum.Parse(fieldInfo.FieldType, property.enumNames[i]);
                allItems.Add(new SearchItem
                {
                    DisplayName = SplitCamelCase(enumNames[i]),
                    IndexInProperty = i,
                    Value = value
                });
            }

            // 검색어에 따라 필터링 (Enum 이름 + 영문 텍스트)
            var filteredItems = allItems
                .Where(item =>
                {
                    if (string.IsNullOrEmpty(state.SearchString)) return true;
                    if (item.DisplayName.IndexOf(state.SearchString, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                    if (_searchData != null && _searchData.DictValueToString.TryGetValue(item.Value, out string englishText))
                    {
                        if (!string.IsNullOrEmpty(englishText) && 
                            englishText.IndexOf(state.SearchString, StringComparison.OrdinalIgnoreCase) >= 0)
                            return true;
                    }
                    return false;
                })
                .ToList();
            
            SearchableEnumAttribute attr = (SearchableEnumAttribute)attribute;
            // 'MaxVisibleItems'로 수정
            float listHeight = Mathf.Min(filteredItems.Count, attr.maxVisible) * EditorGUIUtility.singleLineHeight;
            if (listHeight <= 0) listHeight = EditorGUIUtility.singleLineHeight;
            
            Rect popupRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + PADDING, position.width, 0);

            Rect backgroundRect = new Rect(popupRect.x, popupRect.y, popupRect.width, EditorGUIUtility.singleLineHeight + listHeight + PADDING * 2);
            GUI.Box(backgroundRect, "", EditorStyles.helpBox);

            Rect searchRect = new Rect(popupRect.x + PADDING, popupRect.y + PADDING, popupRect.width - PADDING * 2, EditorGUIUtility.singleLineHeight);
            GUI.SetNextControlName(propertyPath + "_SearchField");
            state.SearchString = EditorGUI.TextField(searchRect, state.SearchString);
            
            Rect scrollViewRect = new Rect(searchRect.x, searchRect.yMax, searchRect.width, listHeight);
            Rect viewRect = new Rect(0, 0, scrollViewRect.width - 20, filteredItems.Count * EditorGUIUtility.singleLineHeight);
            
            state.ScrollPosition = GUI.BeginScrollView(scrollViewRect, state.ScrollPosition, viewRect);

            for (int i = 0; i < filteredItems.Count; i++)
            {
                var item = filteredItems[i];
                Rect itemRect = new Rect(0, i * EditorGUIUtility.singleLineHeight, viewRect.width, EditorGUIUtility.singleLineHeight);
                
                bool isSelected = property.enumValueIndex == item.IndexInProperty;
                if (isSelected) EditorGUI.DrawRect(itemRect, new Color(0.2f, 0.5f, 0.9f, 0.3f));

                if (GUI.Button(itemRect, item.DisplayName, EditorStyles.label))
                {
                    property.enumValueIndex = item.IndexInProperty;
                    property.serializedObject.ApplyModifiedProperties();
                    state.IsDropdownVisible = false;
                    state.SearchString = "";
                    GUI.FocusControl(null);
                }
            }
            GUI.EndScrollView();
        }
        // ▼▼▼ 수정된 부분 (팝업이 닫혀있을 때) ▼▼▼
        else
        {
            // 현재 선택된 값의 영문 텍스트를 가져옴
            string englishValue = GetCurrentEnglishValue(property);
            if (englishValue != null)
            {
                // HelpBox 스타일을 복사해서 wordWrap을 활성화
                GUIStyle style = new GUIStyle(EditorStyles.helpBox);
                style.wordWrap = true;
                
                // OnGUI에서는 position.width로 정확한 높이 계산 가능
                float textHeight = style.CalcHeight(new GUIContent(englishValue), position.width);
                state.EnglishTextHeight = textHeight; // 높이 캐시

                Rect helpBoxRect = new Rect(position.x, buttonRect.yMax + PADDING, position.width, textHeight);
                EditorGUI.HelpBox(helpBoxRect, englishValue, MessageType.None);
            }
            else
            {
                state.EnglishTextHeight = 0; // 텍스트 없으면 높이 0
            }
        }
        // ▲▲▲ 수정된 부분 ▲▲▲

        EditorGUI.EndProperty();
    }

    // ▼▼▼ 수정된 부분 (GetPropertyHeight) ▼▼▼
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        bool isSerializable = fieldInfo.IsPublic || fieldInfo.GetCustomAttribute<SerializeField>() != null;
        if (!isSerializable)
        {
            return EditorGUIUtility.singleLineHeight * 2.5f; // 경고 메시지 높이
        }
        
        if (property.propertyType != SerializedPropertyType.Enum)
        {
            return base.GetPropertyHeight(property, label);
        }
        
        string propertyPath = property.propertyPath;
        if (!_propertyStates.TryGetValue(propertyPath, out State state))
        {
            state = new State();
            _propertyStates[propertyPath] = state;
        }

        // 팝업이 열려있을 때의 높이 계산
        if (state.IsDropdownVisible)
        {
            LoadSearchData(); // 높이 계산 시에도 데이터 로드

            SearchableEnumAttribute attr = (SearchableEnumAttribute)attribute;
            
            var allItems = new List<SearchItem>();
            for(int i = 0; i < property.enumDisplayNames.Length; i++)
            {
                int value = (int)Enum.Parse(fieldInfo.FieldType, property.enumNames[i]);
                allItems.Add(new SearchItem
                {
                    DisplayName = SplitCamelCase(property.enumDisplayNames[i]),
                    IndexInProperty = i,
                    Value = value
                });
            }

            var filteredCount = allItems
                .Count(item =>
                {
                    if (string.IsNullOrEmpty(state.SearchString)) return true;
                    if (item.DisplayName.IndexOf(state.SearchString, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    if (_searchData != null && _searchData.DictValueToString.TryGetValue(item.Value, out string englishText))
                    {
                         if (!string.IsNullOrEmpty(englishText) && 
                             englishText.IndexOf(state.SearchString, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    }
                    return false;
                });

            // 'MaxVisibleItems'로 수정
            float listHeight = Mathf.Min(filteredCount, attr.maxVisible) * EditorGUIUtility.singleLineHeight;
            if (listHeight <= 0) listHeight = EditorGUIUtility.singleLineHeight;
            
            return EditorGUIUtility.singleLineHeight * 2 + listHeight + PADDING * 3;
        }

        // 팝업이 닫혀있을 때의 높이 계산
        float buttonHeight = EditorGUIUtility.singleLineHeight;
        float englishTextHeight = 0;
        
        string englishValue = GetCurrentEnglishValue(property);
        
        if (englishValue != null)
        {
            GUIStyle style = new GUIStyle(EditorStyles.helpBox);
            style.wordWrap = true;
            
            // GetPropertyHeight에서는 position.width를 알 수 없으므로,
            // OnGUI에서 계산한 캐시 값을 사용하거나, 뷰 가로폭으로 추정합니다.
            if (state.EnglishTextHeight > 0)
            {
                englishTextHeight = state.EnglishTextHeight; // 캐시된 높이 사용
            }
            else
            {
                // 캐시가 없으면 추정
                float assumedWidth = EditorGUIUtility.currentViewWidth - 38f; // 인스펙터 폭 추정
                englishTextHeight = style.CalcHeight(new GUIContent(englishValue), assumedWidth);
            }
            
            return buttonHeight + englishTextHeight + PADDING;
        }

        return buttonHeight; // 텍스트가 없으면 버튼 높이만
    }
}
#endif