using UnityEngine;
using System;
using System.Collections;

// 天気システム: Day1-3=晴れ、Day4-6=雨、Day7=嵐
// 雨: モンスター移動15%速く、現象発生率1.3倍
// 嵐: モンスター移動30%速く、現象発生率1.6倍 + 定期的な砂嵐ノイズ
public class WeatherManager : MonoBehaviour
{
    public static WeatherManager Instance { get; private set; }

    // 低いほど速い（MonsterBase.moveInterval に掛ける）
    public float MoveIntervalMultiplier { get; private set; } = 1f;
    public float PhenomenaChanceMultiplier { get; private set; } = 1f;
    public WeatherType CurrentWeather { get; private set; } = WeatherType.Sunny;

    public event Action<WeatherType> OnWeatherChanged;

    private const float STORM_STATIC_INTERVAL_MIN = 30f;
    private const float STORM_STATIC_INTERVAL_MAX = 90f;
    private Coroutine stormRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        GameManager.Instance.OnDayStarted += ApplyWeather;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnDayStarted -= ApplyWeather;
    }

    private void ApplyWeather(int day)
    {
        WeatherType weather = GetWeather(day);
        CurrentWeather = weather;

        switch (weather)
        {
            case WeatherType.Sunny:
                MoveIntervalMultiplier    = 1f;
                PhenomenaChanceMultiplier = 1f;
                break;
            case WeatherType.Rain:
                MoveIntervalMultiplier    = 0.85f;  // 15%速く
                PhenomenaChanceMultiplier = 1.3f;
                break;
            case WeatherType.Storm:
                MoveIntervalMultiplier    = 0.70f;  // 30%速く
                PhenomenaChanceMultiplier = 1.6f;
                break;
        }

        if (stormRoutine != null) StopCoroutine(stormRoutine);
        stormRoutine = null;
        if (weather == WeatherType.Storm)
            stormRoutine = StartCoroutine(StormStaticRoutine());

        OnWeatherChanged?.Invoke(weather);
    }

    // Day に対応する天気を返す（static で MainMenuManager からも参照可能）
    public static WeatherType GetWeather(int day)
    {
        if (day <= 3) return WeatherType.Sunny;
        if (day <= 6) return WeatherType.Rain;
        return WeatherType.Storm;
    }

    // 嵐: 30〜90秒ごとにランダムな時間だけ砂嵐を発生させる
    private IEnumerator StormStaticRoutine()
    {
        while (true)
        {
            float wait = UnityEngine.Random.Range(STORM_STATIC_INTERVAL_MIN, STORM_STATIC_INTERVAL_MAX);
            yield return new WaitForSeconds(wait);

            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Night)
                continue;
            if (SecurityCameraSystem.Instance == null) continue;

            float dur = UnityEngine.Random.Range(0.5f, 2f);
            SecurityCameraSystem.Instance.TriggerFlicker(true,  dur);
            SecurityCameraSystem.Instance.TriggerFlicker(false, dur);
            AudioManager.Instance?.Play("camera_static");
        }
    }
}
