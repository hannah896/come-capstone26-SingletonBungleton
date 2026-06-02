using System;

public class GameEvents {
    public static Action OnGameReady;
    public static Action OnGameStart;
    public static Action OnGamePause;
    public static Action OnGameResume;
    public static Action OnGameContinue;

    // 게임 오버 (사망)
    public static Action OnGameOver;
}