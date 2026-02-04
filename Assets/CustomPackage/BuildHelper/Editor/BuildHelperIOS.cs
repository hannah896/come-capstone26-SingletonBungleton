using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;
using UnityEditor.iOS.Xcode.Extensions;

public class FBFrameworkPostProcess
{
    // 해당 프로젝트가 StormBorn일 경우 변경.
    private const ProjectCompanyType Company =  ProjectCompanyType.ActionFit;
    
    private static readonly string[] FBXCFrameworks =
    {
        // 해당 부분에 IOS빌드 시 자동으로 추가하고 싶은 프래임 워크 추가
        // "FBAEMKit.xcframework",
        // "FBSDKCoreKit.xcframework",
        // "FBSDKCoreKit_Basics.xcframework",
        // "FBSDKLoginKit.xcframework",
        // "FBSDKShareKit.xcframework",
        // "FBSDKGamingServicesKit.xcframework",
    };

    [PostProcessBuild(999)]
    public static void OnPostProcessBuild(
        BuildTarget target,
        string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS) return;

        string pbxPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
        var pbx = new PBXProject();
        pbx.ReadFromFile(pbxPath);

        // 1. 프로젝트 내의 모든 타겟 GUID를 가져옵니다.
        // 여기에는 Unity-iPhone, UnityFramework, GameAssembly, Tests 등이 모두 포함됩니다.
        var allTargets = new System.Collections.Generic.List<string>();
        allTargets.Add(pbx.GetUnityMainTargetGuid());
        allTargets.Add(pbx.GetUnityFrameworkTargetGuid());
    
        // 추가적인 타겟들(GameAssembly 등)을 모두 찾아서 리스트에 추가
        string gameAssemblyGuid = pbx.TargetGuidByName("GameAssembly");
        if (!string.IsNullOrEmpty(gameAssemblyGuid)) allTargets.Add(gameAssemblyGuid);

        string testTargetGuid = pbx.TargetGuidByName("Unity-iPhone Tests");
        if (!string.IsNullOrEmpty(testTargetGuid)) allTargets.Add(testTargetGuid);

        string teamID = "";
        switch (Company)
        {
            case ProjectCompanyType.ActionFit:
                teamID = "49W7A8489P";
                break;
            case ProjectCompanyType.StormBorn:
                teamID = "MCTHBCST32";
                break;
        }
        
        // 2. 모든 타겟을 순회하며 설정 적용
        foreach (var targetGuid in allTargets)
        {
            pbx.SetBuildProperty(targetGuid, "TARGETED_DEVICE_FAMILY", "1,2");
            pbx.SetBuildProperty(targetGuid, "SUPPORTS_MAC_DESIGNED_FOR_IPHONE_IPAD", "NO");
            pbx.SetBuildProperty(targetGuid, "SUPPORTS_XR_DESIGNED_FOR_IPHONE_IPAD", "NO");
            pbx.SetBuildProperty(targetGuid, "SUPPORTS_MACCATALYST", "NO");
            pbx.SetBuildProperty(targetGuid, "GCC_ENABLE_OBJC_EXCEPTIONS", "YES");
            pbx.SetBuildProperty(targetGuid, "CODE_SIGN_STYLE", "Automatic");
            pbx.SetBuildProperty(targetGuid, "DEVELOPMENT_TEAM", teamID);
            pbx.SetBuildProperty(targetGuid, "CODE_SIGN_IDENTITY", "Apple Development");
        }
        
        // 3. 위에 설정한 프래임 워크들을 추가.
        string mainTargetGuid = pbx.GetUnityMainTargetGuid(); 
        foreach (var framework in FBXCFrameworks)
        {
            AddXCFramework(
                pbx,
                pathToBuiltProject,
                framework,
                mainTargetGuid // 여기서 Main Target Guid를 넘겨줍니다.
            );
        }
        pbx.WriteToFile(pbxPath);
        
        // 4. Info.plist에서 AppUsesNonExemptEncryption키가 없을 경우 추가.
        string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
        PlistDocument plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        // Key: App Uses Non-Exempt Encryption (내부적으론 ITSAppUsesNonExemptEncryption)
        // Value: NO (false)
        plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
    
        plist.WriteToFile(plistPath);
    }

    private static void AddXCFramework(
        PBXProject pbx,
        string buildPath,
        string xcframeworkName,
        string targetGuid)
    {
        // 1. Xcode 프로젝트 폴더 전체에서 해당 xcframework의 실제 위치를 검색합니다.
        // Directory.GetDirectories를 사용하여 하위 폴더까지 모두 뒤집니다.
        string[] foundDirectories = Directory.GetDirectories(buildPath, xcframeworkName, SearchOption.AllDirectories);

        if (foundDirectories.Length == 0)
        {
            UnityEngine.Debug.LogError($"[FBFramework] Xcode 프로젝트 내에서 {xcframeworkName}을(를) 찾을 수 없습니다. Unity 인스펙터 설정을 확인하세요.");
            return;
        }

        string fullPath = foundDirectories[0];
        string relativePath = fullPath.Replace(buildPath + Path.DirectorySeparatorChar, "");
    
        relativePath = relativePath.Replace("\\", "/");

        UnityEngine.Debug.Log($"[FBFramework] 파일 발견: {relativePath}");

        string fileGuid = pbx.AddFile(relativePath, relativePath, PBXSourceTree.Source);

        if (!string.IsNullOrEmpty(fileGuid))
        {
            pbx.AddFileToBuild(targetGuid, fileGuid);
        
            pbx.AddFileToEmbedFrameworks(targetGuid, fileGuid);

            string directoryPath = Path.GetDirectoryName(relativePath).Replace("\\", "/");
            pbx.AddBuildProperty(targetGuid, "FRAMEWORK_SEARCH_PATHS", $"$(PROJECT_DIR)/{directoryPath}");
        }
    }
}

public enum ProjectCompanyType
{
    ActionFit,
    StormBorn,
}