using UnityEngine;
using System;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // 1ゲーム時間(1時間) = 150リアル秒(2分30秒)
    // 10時間(20:00-06:00) = 25分のゲームプレイ
    [Header("Time Settings")]
    [SerializeField] private float realSecondsPerGameHour = 150f;

    [Header("Day Settings")]
    [SerializeField] private int totalDays = 7;

    public int CurrentDay { get; private set; } = 1;
    public GamePhase CurrentPhase { get; private set; } = GamePhase.Silence;
    public GameState CurrentState { get; private set; } = GameState.MainMenu;
    public float GameTimeInHours { get; private set; } = 20f;

    // 20時スタートで30時(翌6時)まで = 内部的に20～30
    private float phaseTimer = 0f;
    private bool  isActive = false;
    private float _timeWarpMult = 1f;   // TimeWarp 現象で一時的に加速

    public MonsterType? LastKillerType { get; private set; }

    public event Action<GamePhase> OnPhaseChanged;
    public event Action<int> OnDayStarted;
    public event Action<int> OnNightCleared;
    public event Action OnGameOver;
    public event Action OnTrueEnding;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        // シングルシーン構成のため DontDestroyOnLoad は使わない
        // SceneManager.LoadScene でシーンリロード時に全マネージャーが一緒にリセットされる
    }

    private void Start()
    {
        // MainMenuManagerがある場合はそちらから StartNight() を呼ぶ
        if (FindFirstObjectByType<MainMenuManager>() == null)
            StartNight();
    }

    public void StartNight()
    {
        CurrentState = GameState.Night;
        GameTimeInHours = 20f;
        phaseTimer = 0f;
        CurrentPhase = GamePhase.Silence;
        isActive = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[GameManager] StartNight Day={CurrentDay} → State=Night");
#endif
        OnDayStarted?.Invoke(CurrentDay);
        OnPhaseChanged?.Invoke(CurrentPhase);
    }

    // ステージ選択から呼ぶ: 指定した日付から開始
    public void StartNight(int day)
    {
        CurrentDay = day;
        StartNight();
    }

    private void Update()
    {
        if (!isActive || CurrentState != GameState.Night) return;

        phaseTimer += Time.deltaTime * _timeWarpMult;

        if (phaseTimer >= realSecondsPerGameHour)
        {
            phaseTimer -= realSecondsPerGameHour;
            GameTimeInHours += 1f;

            if (GameTimeInHours >= 30f) // 翌6:00
            {
                TriggerNightClear();
                return;
            }

            RefreshPhase();
        }
    }

    private void RefreshPhase()
    {
        GamePhase newPhase = HourToPhase(GameTimeInHours);
        if (newPhase == CurrentPhase) return;
        CurrentPhase = newPhase;
        OnPhaseChanged?.Invoke(CurrentPhase);
    }

    private GamePhase HourToPhase(float hour)
    {
        if (hour < 21f) return GamePhase.Silence;
        if (hour < 22f) return GamePhase.Omen;
        if (hour < 23f) return GamePhase.Contact;
        if (hour < 24f) return GamePhase.Increase;
        if (hour < 25f) return GamePhase.Erosion;
        if (hour < 26f) return GamePhase.Infiltration;
        if (hour < 27f) return GamePhase.Siege;
        if (hour < 28f) return GamePhase.Collapse;
        if (hour < 29f) return GamePhase.Abyss;
        return GamePhase.BeforeDawn;
    }

    public string GetDisplayTime()
    {
        float hour = GameTimeInHours >= 24f ? GameTimeInHours - 24f : GameTimeInHours;
        int h = (int)hour;
        int m = (int)((phaseTimer / realSecondsPerGameHour) * 60f);
        return $"{h:D2}:{m:D2}";
    }

    // 現在のフェーズの進捗(0-1)
    public float PhaseProgress => phaseTimer / realSecondsPerGameHour;

    // TimeWarp 現象: 指定倍速で duration 秒間ゲーム時間を加速する
    public void TriggerTimeWarp(float multiplier = 3f, float duration = 10f)
        => StartCoroutine(TimeWarpRoutine(multiplier, duration));

    private System.Collections.IEnumerator TimeWarpRoutine(float mult, float dur)
    {
        _timeWarpMult = mult;
        yield return new WaitForSeconds(dur);
        _timeWarpMult = 1f;
    }

    public void TriggerGameOver(MonsterType? killerType = null)
    {
        if (CurrentState == GameState.GameOver) return;
        LastKillerType = killerType;
        isActive = false;
        CurrentState = GameState.GameOver;
        OnGameOver?.Invoke();
    }

    private void TriggerNightClear()
    {
        isActive = false;
        OnNightCleared?.Invoke(CurrentDay);

        if (CurrentDay >= totalDays)
        {
            CurrentState = GameState.TrueEnding;
            OnTrueEnding?.Invoke();
        }
        else
        {
            CurrentState = GameState.DayTransition;
        }
    }

    // UIの「次の夜へ」ボタンから呼ぶ
    public void ProceedToNextDay()
    {
        if (CurrentState != GameState.DayTransition) return;
        CurrentDay++;
        StartNight();
    }

    public void RetryCurrentDay()
    {
        CurrentState = GameState.Night;
        StartNight();
    }
}
