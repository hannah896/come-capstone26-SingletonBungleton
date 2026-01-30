using System;

public class GameEvents {
    public static Action OnGameReady;
    public static Action OnGameStart;
    public static Action OnGamePause;
    public static Action OnGameResume;
    public static Action OnGameContinue;
    public static Action OnGameClear;
    public static Action OnGameOver;

    public static Action<int> OnChangeHeart;
}