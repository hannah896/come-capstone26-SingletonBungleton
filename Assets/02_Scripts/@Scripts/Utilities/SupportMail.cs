
using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using Application = UnityEngine.Device.Application;
using SystemInfo = UnityEngine.Device.SystemInfo;

public class SupportMail : MonoBehaviour
{
    #region Fields

    private static readonly StringBuilder StringBuilder = new();

    #endregion

    public static void Contact()
    {
        StringBuilder.Clear();
        StringBuilder.Append("mailto:").Append(Def.SupportMailAddress);
        StringBuilder.Append("?subject=").Append(Uri.EscapeDataString(GetSubject()));
        StringBuilder.Append("&body=").Append(Uri.EscapeDataString(GetEmailBody()));
        
        Application.OpenURL(StringBuilder.ToString());
    }

    private static string GetSubject()
    {
        var subjectBuilder = new StringBuilder();
        var lang = Application.systemLanguage.ToString();
        subjectBuilder.Append("[Game Support][ActionFitGame] ");//TODO:: GameName!
        subjectBuilder.Append($"({lang})");
        
        return subjectBuilder.ToString();
    }

    private static string GetEmailBody()
    {
        var bodyBuilder = new StringBuilder();
        bodyBuilder.Append("Device Model : ").AppendLine(SystemInfo.deviceModel);
        bodyBuilder.Append("Device OS : ").AppendLine(SystemInfo.operatingSystem);
        bodyBuilder.Append("Device Battery Level : ").AppendLine(SystemInfo.batteryLevel.ToString(CultureInfo.InvariantCulture));
        bodyBuilder.Append("Device Memory Size : ").AppendLine(SystemInfo.systemMemorySize.ToString(CultureInfo.InvariantCulture));
        bodyBuilder.Append("Device GPU Memory Size : ").AppendLine(SystemInfo.graphicsMemorySize.ToString(CultureInfo.InvariantCulture));
        bodyBuilder.AppendLine();
        bodyBuilder.Append("Game Name : ").AppendLine(Application.productName);
        bodyBuilder.Append("Game Version : ").AppendLine(Application.version);
        bodyBuilder.Append("Game Platform : ").AppendLine(Application.platform.ToString());
        bodyBuilder.AppendLine("--------------------------------------------------");
        bodyBuilder.AppendLine();
        bodyBuilder.AppendLine();
        bodyBuilder.AppendLine("Please describe your issue or question here.");
        return bodyBuilder.ToString();
    }
}