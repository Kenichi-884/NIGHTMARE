using UnityEngine;
using UnityEngine.UI;
using System.Collections;

// モンスターが管理人室に近づいたとき、ハートビート音と赤いビネットで警告する
// B1_DoorFront = 高危険、B1_Corridor = 中危険
public class ProximityAlertSystem : MonoBehaviour
{
    public static ProximityAlertSystem Instance { get; private set; }

    [SerializeField] private Image dangerVignette;
    [SerializeField] private float highDangerHeartbeatInterval = 1.0f;
    [SerializeField] private float midDangerHeartbeatInterval  = 2.5f;

    private DangerLevel currentLevel = DangerLevel.None;
    private float heartbeatTimer = 0f;
    private Coroutine vignetteRoutine;

    private enum DangerLevel { None, Mid, High }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (dangerVignette) dangerVignette.gameObject.SetActive(false);
        GameManager.Instance.OnDayStarted += _ => ResetAlert();
        GameManager.Instance.OnGameOver   += () => ResetAlert();
    }

    private void Update()
    {
        if (GameManager.Instance?.CurrentState != GameState.Night) return;
        if (MonsterManager.Instance == null) return;

        var newLevel = EvaluateDanger();
        if (newLevel != currentLevel)
        {
            currentLevel = newLevel;
            ApplyDangerLevel();
        }

        if (currentLevel == DangerLevel.None) return;

        float interval = currentLevel == DangerLevel.High
            ? highDangerHeartbeatInterval
            : midDangerHeartbeatInterval;
        heartbeatTimer += Time.deltaTime;
        if (heartbeatTimer >= interval)
        {
            heartbeatTimer = 0f;
            AudioManager.Instance?.Play("heartbeat");
        }
    }

    private DangerLevel EvaluateDanger()
    {
        bool atDoor = false, atCorridor = false;
        foreach (var m in MonsterManager.Instance.ActiveMonsters)
        {
            if (m.CurrentLocation == FacilityLocation.B1_DoorFront) atDoor = true;
            if (m.CurrentLocation == FacilityLocation.B1_Corridor)  atCorridor = true;
        }
        if (atDoor)     return DangerLevel.High;
        if (atCorridor) return DangerLevel.Mid;
        return DangerLevel.None;
    }

    private void ApplyDangerLevel()
    {
        heartbeatTimer = 0f;
        if (vignetteRoutine != null) StopCoroutine(vignetteRoutine);

        if (currentLevel == DangerLevel.None)
        {
            if (dangerVignette) dangerVignette.gameObject.SetActive(false);
        }
        else
        {
            if (dangerVignette) vignetteRoutine = StartCoroutine(PulseVignette());
        }
    }

    private IEnumerator PulseVignette()
    {
        if (dangerVignette == null) yield break;
        dangerVignette.gameObject.SetActive(true);

        while (currentLevel != DangerLevel.None)
        {
            float baseA  = currentLevel == DangerLevel.High ? 0.38f : 0.18f;
            float speed  = currentLevel == DangerLevel.High ? 3.0f  : 1.4f;
            float a = baseA + Mathf.Sin(Time.time * speed) * (baseA * 0.45f);
            var c = dangerVignette.color;
            c.a = a;
            dangerVignette.color = c;
            yield return null;
        }

        // フェードアウト
        float fade = dangerVignette.color.a;
        while (fade > 0f)
        {
            fade -= Time.deltaTime * 3f;
            var c = dangerVignette.color;
            c.a = Mathf.Max(0f, fade);
            dangerVignette.color = c;
            yield return null;
        }

        dangerVignette.gameObject.SetActive(false);
    }

    private void ResetAlert()
    {
        currentLevel = DangerLevel.None;
        heartbeatTimer = 0f;
        if (vignetteRoutine != null) StopCoroutine(vignetteRoutine);
        if (dangerVignette) dangerVignette.gameObject.SetActive(false);
    }

    // エディタセットアップから呼ぶ
    public void AutoFindReferences()
    {
        dangerVignette = FindDeep<Image>("DangerVignette");
    }

    private T FindDeep<T>(string name) where T : Component
        => FindIn<T>(transform, name);

    private T FindIn<T>(Transform root, string name) where T : Component
    {
        foreach (Transform c in root)
        {
            if (c.name == name) { var r = c.GetComponent<T>(); if (r) return r; }
            var f = FindIn<T>(c, name);
            if (f) return f;
        }
        return null;
    }
}
