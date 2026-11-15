using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SingletonBungleton.BossTraining.Editor
{
    /// <summary>
    /// 학습 · 평가를 사람이 Play를 다시 누르지 않고 이어서 돌려 주는 에디터 도구.
    ///
    ///  - 자동 학습 반복(3회): 파이썬 쪽에서 mlagents-learn을 시드만 바꿔 연달아 켜 두면, 한 번 끝날 때마다 잠깐 쉬었다가 다시 Play한다.
    ///    파이썬 서버가 먼저 켜져 있지 않은 채 Play하면 트레이너 없이 단독 실행이 되므로, 재시작 전에 넉넉히 기다린다.
    ///  - 시드 모델 평가: Assets/05_Datas/BossModel/Eval 의 .onnx 를 하나씩 학습 아레나에 꽂고 300판씩 평가한다(결과는 results/dragon_eval.csv, 라벨 = 파일명).
    ///
    /// 중지: Tools/보스 학습/자동 실행 중지. 실행이 너무 짧게 끝나면(오류 등) 자동으로 중단한다.
    /// </summary>
    [InitializeOnLoad]
    public static class DragonAutoRun
    {
        private const string ScenePath = "Assets/01_Scenes/Training/DragonTrainingScene.unity";
        private const string EvalFolder = "Assets/05_Datas/BossModel/Eval";

        private const string KeyMode = "DragonAuto.Mode";
        private const string KeyLeft = "DragonAuto.Left";
        private const string KeyQueue = "DragonAuto.Queue";
        private const string KeyStart = "DragonAuto.Start";
        private const string KeyLabel = "DragonEvalLabel";

        private const double TrainRestartDelay = 45.0;   // 파이썬 서버가 다시 뜰 시간
        private const double EvalRestartDelay = 3.0;
        private const double TrainMinRun = 120.0;
        private const double EvalMinRun = 20.0;

        private static bool waiting;
        private static double waitUntil;

        static DragonAutoRun()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += Tick;
        }

        #region 메뉴

        [MenuItem("Tools/보스 학습/자동 학습 반복 시작 (3회)")]
        public static void StartTrainLoop()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[용 자동] 이미 Play 중입니다. 멈춘 뒤 다시 실행하세요.");
                return;
            }

            // 학습 중에는 모델이 꽂혀 있으면 안 된다. 혹시 트레이너 없이 단독 실행되면 평가 기록이 섞이지 않게 별도 라벨을 단다.
            DragonTrainingSetup.SetTrainingArenaModelAtPath(null);
            EditorPrefs.SetString(KeyMode, "train");
            EditorPrefs.SetInt(KeyLeft, 3);
            EditorPrefs.SetString(KeyLabel, "aborted_standalone");
            Debug.Log("[용 자동] 학습 3회 자동 반복을 시작합니다. 파이썬 서버(mlagents-learn)가 이미 켜져 있어야 합니다.");
            BeginPlay();
        }

        [MenuItem("Tools/보스 학습/시드 모델 평가 자동 실행")]
        public static void StartEvalLoop()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[용 자동] 이미 Play 중입니다. 멈춘 뒤 다시 실행하세요.");
                return;
            }

            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", EvalFolder));
            if (!Directory.Exists(folder))
            {
                Debug.LogError($"[용 자동] 평가할 모델 폴더가 없습니다: {EvalFolder}");
                return;
            }

            var paths = new List<string>();
            foreach (string file in Directory.GetFiles(folder, "*.onnx"))
                paths.Add($"{EvalFolder}/{Path.GetFileName(file)}");
            paths.Sort(StringComparer.Ordinal);

            if (paths.Count == 0)
            {
                Debug.LogError($"[용 자동] {EvalFolder} 에 .onnx 가 없습니다.");
                return;
            }

            EditorPrefs.SetString(KeyMode, "eval");
            EditorPrefs.SetString(KeyQueue, string.Join("|", paths));
            Debug.Log($"[용 자동] 모델 {paths.Count}개를 차례로 300판씩 평가합니다.");
            AdvanceEval();
        }

        [MenuItem("Tools/보스 학습/자동 실행 중지")]
        public static void StopAuto()
        {
            Finish("사용자가 중지했습니다.");
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }

        #endregion

        #region 흐름

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            string mode = EditorPrefs.GetString(KeyMode, "");
            if (string.IsNullOrEmpty(mode)) return;

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                EditorPrefs.SetString(KeyStart, EditorApplication.timeSinceStartup.ToString("R"));
                return;
            }

            if (state != PlayModeStateChange.EnteredEditMode) return;

            double.TryParse(EditorPrefs.GetString(KeyStart, "0"), out double start);
            double ran = EditorApplication.timeSinceStartup - start;
            double minRun = mode == "train" ? TrainMinRun : EvalMinRun;
            if (ran < minRun)
            {
                Finish($"실행이 {ran:F0}초 만에 끝나 자동 반복을 중단했습니다. 콘솔 에러와 파이썬 로그를 확인하세요.");
                return;
            }

            if (mode == "train")
            {
                int left = EditorPrefs.GetInt(KeyLeft, 0) - 1;
                EditorPrefs.SetInt(KeyLeft, left);
                if (left <= 0)
                {
                    Finish("학습 반복을 모두 마쳤습니다.");
                    return;
                }

                Debug.Log($"[용 자동] 학습 한 회를 마쳤습니다. 남은 횟수 {left}. {TrainRestartDelay:F0}초 뒤 다음 학습을 시작합니다.");
                Schedule(TrainRestartDelay);
            }
            else
            {
                Schedule(EvalRestartDelay);
            }
        }

        private static void Schedule(double delaySeconds)
        {
            waiting = true;
            waitUntil = EditorApplication.timeSinceStartup + delaySeconds;
        }

        private static void Tick()
        {
            if (!waiting || EditorApplication.timeSinceStartup < waitUntil) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;

            waiting = false;
            string mode = EditorPrefs.GetString(KeyMode, "");
            if (mode == "train") BeginPlay();
            else if (mode == "eval") AdvanceEval();
        }

        private static void AdvanceEval()
        {
            string queue = EditorPrefs.GetString(KeyQueue, "");
            if (string.IsNullOrEmpty(queue))
            {
                Finish("평가를 모두 마쳤습니다. 결과: results/dragon_eval.csv");
                return;
            }

            int cut = queue.IndexOf('|');
            string next = cut < 0 ? queue : queue.Substring(0, cut);
            EditorPrefs.SetString(KeyQueue, cut < 0 ? "" : queue.Substring(cut + 1));

            DragonTrainingSetup.SetTrainingArenaModelAtPath(next);
            EditorPrefs.SetString(KeyLabel, Path.GetFileNameWithoutExtension(next));
            Debug.Log($"[용 자동] 평가 시작: {next}");
            BeginPlay();
        }

        private static void BeginPlay()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        private static void Finish(string message)
        {
            bool wasEval = EditorPrefs.GetString(KeyMode, "") == "eval";
            waiting = false;
            EditorPrefs.DeleteKey(KeyMode);
            EditorPrefs.DeleteKey(KeyLeft);
            EditorPrefs.DeleteKey(KeyQueue);
            EditorPrefs.DeleteKey(KeyStart);
            EditorPrefs.DeleteKey(KeyLabel);

            // 평가가 끝나면 학습 아레나를 규칙 기반/학습용 상태(모델 없음)로 되돌린다
            if (wasEval) DragonTrainingSetup.SetTrainingArenaModelAtPath(null);
            Debug.Log($"[용 자동] {message}");
        }

        #endregion
    }
}
