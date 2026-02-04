#if UNITY_EDITOR

using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using System.IO;
using System.Linq;
using Project.Scripts.Build.Editor;
using Debug = UnityEngine.Debug;

#if UNITY_ANDROID

public class AOSBuildProcess

{
    [System.Serializable]
    public class FirebaseData

    {
        public FirebaseClient[] client;
    }

    [System.Serializable]
    public class FirebaseClient

    {
        public FirebaseClientInfo client_info;
    }

    [System.Serializable]
    public class FirebaseClientInfo

    {
        public FirebaseAndroidInfo android_client_info;
    }

    [System.Serializable]
    public class FirebaseAndroidInfo

    {
        public string package_name;
    }

    public static void AndroidBuildApk(
        BuildSettingsSO setting
    )

    {
#if DEV
Debug.LogError("[Build Fail] build process is not support in DEV define");

return;
#endif
        EditorUserBuildSettings.androidCreateSymbols = AndroidCreateSymbols.Public;
        EditorUserBuildSettings.buildAppBundle = false;
        AndroidBuildProcess(setting);
    }

    public static void AndroidBuildApkRun(
        BuildSettingsSO setting
    )

    {
        EditorUserBuildSettings.androidCreateSymbols = AndroidCreateSymbols.Public;
        EditorUserBuildSettings.buildAppBundle = false;
        AndroidBuildProcess(setting, false, "", BuildOptions.AutoRunPlayer);
    }

    public static void AndroidBuildAab(
        BuildSettingsSO setting
    )

    {
#if DEV
Debug.LogError("[Build Fail] build process is not support in DEV define");

return;
#endif
        EditorUserBuildSettings.androidCreateSymbols = AndroidCreateSymbols.Public;
        EditorUserBuildSettings.buildAppBundle = true;
        AndroidBuildProcess(setting, true);
    }

    public static void AndroidBuildAabRun(
        BuildSettingsSO setting
    )

    {
        EditorUserBuildSettings.androidCreateSymbols = AndroidCreateSymbols.Public;
        EditorUserBuildSettings.buildAppBundle = true;
        AndroidBuildProcess(setting, true, "", BuildOptions.AutoRunPlayer);
    }

    static void AndroidBuildProcess(
        BuildSettingsSO setting,
        bool aab = false,
        string buildPrefix = "",
        BuildOptions buildOptions = BuildOptions.None
    )

    {
        AosBuildSetting(setting);
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            UnityEngine.Debug.LogError("Current platform is not Android. Please switch to Android platform first.");
            return;
        }
        string buildName = setting.buildFileName;
        string version = setting.buildVersion;
        string bundleCode = setting.bundleNo;
        string buildPath = setting.androidBuildPath + $"/{buildName}_v{version}({bundleCode})/";
        if (setting.saveFileInProject)
        {
            string folderName = $"{buildName}_v{version}({bundleCode})";
            buildPath = $"Builds/{folderName}";
            string absolutePath = Path.GetFullPath(buildPath);
            if (!Directory.Exists(absolutePath))
            {
                Directory.CreateDirectory(absolutePath);
                AssetDatabase.Refresh();
            }
        }
        if (Path.IsPathRooted(buildPath))
        {
            string projectPath = Path.GetDirectoryName(Application.dataPath);
            buildPath = Path.GetRelativePath(projectPath, buildPath);
        }
        string fullBuildPath = Path.Combine(Application.dataPath, "..", buildPath);
        if (!Directory.Exists(fullBuildPath))
        {
            Directory.CreateDirectory(fullBuildPath);
        }
        string fileName = $"{buildName}_v{version}({bundleCode}).{(aab ? "aab" : "apk")}";
        string buildFilePath = Path.Combine(buildPath, fileName);
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray(),
            locationPathName = buildFilePath, target = BuildTarget.Android, options = buildOptions
        };
        EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
        UnityEngine.Debug.Log($"Starting build at: {buildFilePath}");
        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;
        switch (summary.result)
        {
            case BuildResult.Succeeded:
                UnityEngine.Debug.Log($"Build succeeded: {summary.totalSize} bytes");
                string absoluteBuildPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", buildPath));
                UnityEngine.Debug.Log($"Build saved to: {absoluteBuildPath}");
                EditorUtility.RevealInFinder(absoluteBuildPath);
                break;
            case BuildResult.Failed:
                UnityEngine.Debug.LogError($"Build failed with {summary.totalErrors} errors");
                foreach (var step in report.steps)
                {
                    foreach (var message in step.messages)
                    {
                        if (message.type == LogType.Error)
                        {
                            UnityEngine.Debug.LogError($"Build error: {message.content}");
                        }
                    }
                }
                break;
        }
    }

    private static void AosBuildSetting(
        BuildSettingsSO settings
    )

    {
        BuildTargetGroup buildTargetGroup = BuildTargetGroup.Android;
        string currentDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTargetGroup);
        var definesList = new List<string>(currentDefines.Split(';'));

// #1. 이름 설정.
        if (!string.IsNullOrEmpty(settings.companyName))
        {
            PlayerSettings.companyName = settings.companyName;
        }
        if (!string.IsNullOrEmpty(settings.productName))
        {
            PlayerSettings.productName = settings.productName;
        }

// #2. Dev 모드 처리.
        if (settings.isDevMode)
        {
            if (!definesList.Contains("DEV")) definesList.Add("DEV");
        }
        else
            definesList.RemoveAll(x => x == "DEV");

// #3. 심볼 처리.
        const string GpgsSymbol = "ENABLE_GPGS";
        if (settings.settingGpgs)
        {
            if (!definesList.Contains(GpgsSymbol)) definesList.Add(GpgsSymbol);
        }
        else
        {
            definesList.RemoveAll(x => x == GpgsSymbol);
        }
        if (settings.defineSymbol.Count > 0)
        {
            foreach (var symbol in settings.defineSymbol)
            {
                if (!string.IsNullOrEmpty(symbol) && !definesList.Contains(symbol)) definesList.Add(symbol);
            }
        }
        string newDefines = string.Join(";", definesList);
        PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, newDefines);
        PlayerSettings.bundleVersion = settings.buildVersion;
        if (File.Exists(settings.keyStorePath))
        {
            PlayerSettings.Android.keystoreName = settings.keyStorePath;
            PlayerSettings.Android.keystorePass = BuildSettingsSO.KeyStorePassword;
            PlayerSettings.Android.keyaliasName = settings.keyStoreAlias;
            PlayerSettings.Android.keyaliasPass = BuildSettingsSO.AliasPassword;
        }
        if (int.TryParse(settings.bundleNo, out int bundleCode))
        {
            PlayerSettings.Android.bundleVersionCode = bundleCode;
        }

// #4. GPGS 설정 (사용자가 입력한 데이터를 기반으로 수행)
        if (settings.settingGpgs)
        {
            DoGpgsSetup(settings.resourcesDefinition, settings.clientID);
        }

// #5. 키스토어 체크
        string finalKeystorePath = settings.keyStorePath;
        if (settings.autoSearchKeystore)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string[] files = Directory.GetFiles(projectRoot, "*.keystore", SearchOption.AllDirectories);
            string validKeystore = null;
            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);

// "._" 로 시작하는 맥 시스템 숨김 파일이 아닌 경우만 선택
                if (!fileName.StartsWith("._"))
                {
                    validKeystore = file;
                    break;
                }
            }
            if (!string.IsNullOrEmpty(validKeystore))
            {
                finalKeystorePath = validKeystore;
                UnityEngine.Debug.Log($"<color=green><b>[Build]</b></color> Keystore Auto-detected: {finalKeystorePath}");
                settings.keyStorePath = finalKeystorePath;
                EditorUtility.SetDirty(settings);
            }
            else
            {
                UnityEngine.Debug.LogWarning("[Build] 유효한 .keystore 파일을 찾지 못했습니다.");
            }
        }

// #6. Alias 체크
        if (settings.autoSearchAlias)
        {
            string detectedAlias =
                GetFirstAliasFromKeystore(finalKeystorePath, BuildSettingsSO.KeyStorePassword);
            if (!string.IsNullOrEmpty(detectedAlias))
            {
                settings.keyStoreAlias = detectedAlias;
                UnityEngine.Debug.Log($"<color=cyan><b>[Build]</b></color> Alias Auto-detected: {detectedAlias}");
            }
        }
        if (File.Exists(finalKeystorePath))
        {
            PlayerSettings.Android.keystoreName = finalKeystorePath;
            PlayerSettings.Android.keystorePass = BuildSettingsSO.KeyStorePassword;
            PlayerSettings.Android.keyaliasName = settings.keyStoreAlias;
            PlayerSettings.Android.keyaliasPass = BuildSettingsSO.AliasPassword;
        }
        else
        {
            UnityEngine.Debug.LogError($"[Build] Keystore file not found at: {finalKeystorePath}");
        }

// #7. 패키지 이름 적용
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, settings.androidPackageName);
    }

    private static string GetFirstAliasFromKeystore(
        string keystorePath,
        string password
    )

    {
        try
        {
// 유니티가 사용하는 JDK 내의 keytool 경로 찾기
            string jdkPath = UnityEditor.Android.AndroidExternalToolsSettings.jdkRootPath;
            string keytoolPath = Path.Combine(jdkPath, "bin", "keytool");

// 윈도우일 경우 .exe 확장자 추가
            if (Application.platform == RuntimePlatform.WindowsEditor) keytoolPath += ".exe";
            if (!File.Exists(keytoolPath))
            {
                UnityEngine.Debug.LogError("Keytool을 찾을 수 없습니다. JDK 경로를 확인하세요.");
                return string.Empty;
            }

// keytool -list 명령어 실행 (비밀번호 전달)
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = keytoolPath, Arguments = $"-list -keystore \"{keystorePath}\" -storepass {password}", RedirectStandardOutput = true, UseShellExecute = false,
                CreateNoWindow = true, StandardOutputEncoding = System.Text.Encoding.UTF8
            };
            using (Process process = Process.Start(startInfo))
            {
                using (StreamReader reader = process.StandardOutput)
                {
                    string result = reader.ReadToEnd();

// 결과 문자열에서 Alias 별칭 추출 (보통 "별칭 이름, 날짜, ..." 형식으로 나옴)

// 정규표현식이나 한 줄씩 읽어서 추출
                    string[] lines = result.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.None);
                    foreach (var line in lines)
                    {
// keytool 결과에서 별칭이 포함된 라인을 찾음 (보통 쉼표로 시작하거나 특정 패턴이 있음)
                        if (!string.IsNullOrEmpty(line) && line.Contains(","))
                        {
// 쉼표 앞부분이 Alias 이름임
                            return line.Split(',')[0].Trim();
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"Alias 추출 실패: {e.Message}");
        }
        return string.Empty;
    }

    private static void ResetGpgsState()

    {
#if ENABLE_GPGS
var settings = GPGSProjectSettings.Instance;

settings.Set("proj.AppId", null);

settings.Set("proj.classDir", null);

settings.Set("proj.ConstantsClassName", null);

settings.Set("and.ResourceData", null);

settings.Set("and.ClientId", null);

settings.Save();
#endif
    }

    private static void DoGpgsSetup(
        string resourceXml,
        string clientId
    )

    {
#if ENABLE_GPGS
if (string.IsNullOrEmpty(resourceXml))

{

Debug.LogError("[GPGS] Resource Definition이 비어있습니다.");

return;

}



// 1. 클라이언트 ID 강제 정규화 (패턴 보강)

if (!string.IsNullOrEmpty(clientId))

{

// \s(공백문자 전체)를 포함하여 허용되지 않는 모든 유니코드 문자를 제거합니다.

clientId = System.Text.RegularExpressions.Regex.Replace(clientId, @"[^a-zA-Z0-9\-\.]", "").Trim();


// 디버깅: 정제된 ID의 길이를 함께 출력하여 보이지 않는 문자가 있는지 최종 확인

Debug.Log($"[GPGS] 정제된 Client ID: '{clientId}' (Length: {clientId.Length})");

}



// 2. App ID 추출 (기존 로직 유지)

string appId = "";

var match = System.Text.RegularExpressions.Regex.Match(resourceXml, @"name=""app_id""[^>]*>(\d+)</string>");

if (match.Success)

{

appId = match.Groups[1].Value;

}

else

{

Debug.LogError("[GPGS] XML에서 app_id를 찾을 수 없습니다.");

return;

}



try

{

// 3. GPGS 내부 설정 데이터 선행 주입 (중요)

// PerformSetup 호출 전에 Instance에 직접 값을 넣어주면 플러그인이 더 안정적으로 반응합니다.

var settings = GooglePlayGames.Editor.GPGSProjectSettings.Instance;

settings.Set("proj.AppId", appId);

settings.Set("proj.classDir", BuildSettingsSO.SaveConstants);

settings.Set("proj.ConstantsClassName", BuildSettingsSO.ConstantsName);

settings.Set("and.ResourceData", resourceXml);

settings.Set("and.ClientId", clientId);

settings.Save();



// 4. GPGS Setup 실행

ResetGpgsState();

AssetDatabase.Refresh();

bool success = GPGSAndroidSetupUI.PerformSetup(clientId, BuildSettingsSO.SaveConstants, BuildSettingsSO.ConstantsName, resourceXml, null);



if (success)

{

AssetDatabase.Refresh();

Debug.Log($"<color=green><b>[GPGS]</b></color> Setup Complete! (AppId: {appId})");

}

}

catch (System.Exception e)

{

Debug.LogError($"[GPGS] Setup 도중 예외 발생: {e.Message}");

}
#endif
    }

    public static string GetPackageNameFromFirebaseJson()
    {
        // 1. 프로젝트 루트 경로 가져오기 (Assets 폴더의 부모 폴더)
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        // 2. System.IO를 사용하여 루트부터 모든 하위 폴더에서 파일 검색
        // SearchOption.AllDirectories를 쓰면 프로젝트 전체를 뒤집니다.
        string[] files = Directory.GetFiles(projectRoot, "google-services.json", SearchOption.AllDirectories);
        if (files.Length == 0)
        {
            EditorUtility.DisplayDialog("Error", "프로젝트 경로 내에서 google-services.json 파일을 찾을 수 없습니다.", "OK");
            return null;
        }

        // 첫 번째로 발견된 파일 경로 사용
        string jsonPath = files[0];
        try
        {
            // 3. 파일 읽기 및 파싱 (이후 로직은 동일)
            string jsonText = File.ReadAllText(jsonPath);
            FirebaseData data = JsonUtility.FromJson<FirebaseData>(jsonText);
            if (data != null && data.client != null && data.client.Length > 0)
            {
                string pName = data.client[0].client_info.android_client_info.package_name;
                if (!string.IsNullOrEmpty(pName))
                {
                    return pName;
                }
            }
            EditorUtility.DisplayDialog("Error", "JSON 내에 package_name 정보가 없습니다.", "OK");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"[Firebase Parsing Error] {e.Message}");
        }
        return null;
    }
}

#endif

#endif