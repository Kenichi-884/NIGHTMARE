using UnityEngine;
using System;
using System.Collections.Generic;

public class PowerManager : MonoBehaviour
{
    public static PowerManager Instance { get; private set; }

    [SerializeField] private float maxPower = 100f;
    [SerializeField] private float baseDrainPerSecond = 1f / 60f; // 1%/分

    private float currentPower = 100f;
    private readonly Dictionary<string, float> drains = new Dictionary<string, float>();
    private bool isPowerOut = false;
    private float restoreTimer = 0f;
    private const float RESTORE_DELAY = 300f;
    private const float RESTORE_AMOUNT = 10f;

    private float _cachedTotalDrain   = 0f;   // drains 変化時のみ再計算
    private float _lastBroadcastPower = -1f;  // OnPowerChanged の過剰発火を防ぐ
    private const float BroadcastThreshold = 0.5f; // 0.5% 変化で発火

    public float CurrentPower => currentPower;
    public float PowerPercent => currentPower / maxPower;
    public bool IsPowerOut => isPowerOut;

    public event Action<float> OnPowerChanged;
    public event Action OnPowerOut;
    public event Action OnPowerRestored;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        drains["natural"] = baseDrainPerSecond;
        RecalcDrainCache();
        GameManager.Instance.OnPhaseChanged += AdjustNaturalDrain;
        GameManager.Instance.OnDayStarted += ResetPower;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.OnPhaseChanged -= AdjustNaturalDrain;
        GameManager.Instance.OnDayStarted -= ResetPower;
    }

    private void Update()
    {
        if (GameManager.Instance.CurrentState != GameState.Night) return;

        if (isPowerOut)
        {
            restoreTimer += Time.deltaTime;
            if (restoreTimer >= RESTORE_DELAY) RestorePower();
            return;
        }

        currentPower = Mathf.Clamp(currentPower - _cachedTotalDrain * Time.deltaTime, 0f, maxPower);

        // 前回の通知から BroadcastThreshold 以上変化したときのみ発火
        if (Mathf.Abs(currentPower - _lastBroadcastPower) >= BroadcastThreshold)
        {
            _lastBroadcastPower = currentPower;
            OnPowerChanged?.Invoke(currentPower);
        }

        if (currentPower <= 0f) TriggerPowerOut();
    }

    private void AdjustNaturalDrain(GamePhase phase)
    {
        float perMin = phase switch
        {
            GamePhase.Collapse   => 2f,
            GamePhase.Abyss      => 2f,
            GamePhase.BeforeDawn => 3f,
            _ => 1f
        };
        float dayMultiplier = 1f + (GameManager.Instance.CurrentDay - 1) * 0.1f;
        drains["natural"] = (perMin / 60f) * dayMultiplier;
        RecalcDrainCache();
    }

    public void ApplyDayDifficulty(int day)
    {
        float multiplier = 1f + (day - 1) * 0.1f;
        drains["natural"] = (baseDrainPerSecond * multiplier);
        RecalcDrainCache();
    }

    private void ResetPower(int day)
    {
        currentPower = maxPower;
        isPowerOut = false;
        drains.Clear();
        drains["natural"] = baseDrainPerSecond;
        ApplyDayDifficulty(day);
        _lastBroadcastPower = currentPower;
        OnPowerChanged?.Invoke(currentPower);
    }

    public void AddDrain(string key, float perSecond) { drains[key] = perSecond; RecalcDrainCache(); }
    public void RemoveDrain(string key) { drains.Remove(key); RecalcDrainCache(); }

    public void AddPower(float amount)
    {
        currentPower = Mathf.Clamp(currentPower + amount, 0f, maxPower);
        _lastBroadcastPower = currentPower;
        OnPowerChanged?.Invoke(currentPower);
    }

    private void RecalcDrainCache()
    {
        _cachedTotalDrain = 0f;
        foreach (var v in drains.Values) _cachedTotalDrain += v;
    }

    // デバッグ用: 現在の合計ドレイン速度 (%/秒)
    public float TotalDrainPerSecond() => _cachedTotalDrain;
    public Dictionary<string, float> DrainSnapshot()
        => new Dictionary<string, float>(drains);

    // 即時消費（失敗したらfalseを返す）
    public bool Consume(float amount)
    {
        if (isPowerOut || currentPower < amount) return false;
        currentPower -= amount;
        OnPowerChanged?.Invoke(currentPower);
        return true;
    }

    // 現象による急激な電力消費
    public void ApplyFluctuation(float amount)
    {
        currentPower = Mathf.Max(0f, currentPower - amount);
        _lastBroadcastPower = currentPower;
        OnPowerChanged?.Invoke(currentPower);
        if (currentPower <= 0f) TriggerPowerOut();
    }

    private void TriggerPowerOut()
    {
        isPowerOut = true;
        restoreTimer = 0f;
        drains.Clear();
        RecalcDrainCache();
        OnPowerOut?.Invoke();
    }

    private void RestorePower()
    {
        isPowerOut = false;
        currentPower = RESTORE_AMOUNT;
        drains["natural"] = baseDrainPerSecond;
        RecalcDrainCache();
        _lastBroadcastPower = currentPower;
        OnPowerRestored?.Invoke();
    }
}
