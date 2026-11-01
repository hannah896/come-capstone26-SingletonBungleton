using UnityEngine;

namespace SingletonBungleton.BossTraining
{
    /// <summary>
    /// 학습 화면 왼쪽 위에 누적 통계를 띄운다 (최근 50판 승률, 평균 시간/피해, 패턴별 사용·적중).
    /// 학습이 진행되며 숫자가 변하는 모습을 발표용 스크린샷/영상에 담기 위한 것이다.
    /// </summary>
    public class DragonTrainingHud : MonoBehaviour
    {
        [SerializeField] private string title = "Dragon Boss RL (PPO)";

        private static readonly string[] SkillNames = { "Bite", "Breath", "Charge", "Spit" };
        private GUIStyle style;
        private GUIStyle titleStyle;

        private void OnGUI()
        {
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = 18, richText = true };
                style.normal.textColor = Color.white;
                titleStyle = new GUIStyle(style) { fontSize = 22, fontStyle = FontStyle.Bold };
            }

            int n = DragonTrainingStats.Episodes;
            float avgSec = n > 0 ? DragonTrainingStats.TotalSeconds / n : 0f;
            float avgDealt = n > 0 ? DragonTrainingStats.TotalDamageDealt / n : 0f;

            GUILayout.BeginArea(new Rect(16f, 12f, 520f, 400f));
            GUILayout.Label(title, titleStyle);
            GUILayout.Label($"Episodes: {n}   (Win {DragonTrainingStats.Wins} / Lose {DragonTrainingStats.Losses} / Timeout {DragonTrainingStats.Timeouts})", style);
            GUILayout.Label($"<b>Win rate (last {DragonTrainingStats.RecentCount}): {DragonTrainingStats.RecentWinRate * 100f:F0}%</b>", style);
            GUILayout.Label($"Avg episode: {avgSec:F1}s   Avg damage dealt: {avgDealt:F0}", style);

            for (int i = 0; i < SkillNames.Length; i++)
            {
                int uses = DragonTrainingStats.Uses[i];
                float rate = uses > 0 ? 100f * DragonTrainingStats.Hits[i] / uses : 0f;
                GUILayout.Label($"{SkillNames[i],-7} used {uses,6}   hit {rate:F0}%", style);
            }

            GUILayout.EndArea();
        }
    }
}
