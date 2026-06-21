using UnityEngine;
using UnityEngine.UI;
using System.Collections;

// モンスターが管理人室に近づいたとき、段階的な警告を行う
// Lobby → Corridor → B1_DoorFront と近づくほど演出が激しくなる
public class ProximityAlertSystem : MonoBehaviour
{
    public static ProximityAlertSystem Instance { get; private set; }

    [SerializeField] private Image dangerVignette;
    [SerializeField] private float lobbyHeartbeatInterval    = 4.5f;
    [SerializeField] private float midDangerHeartbeatInterval  = 2.5f;
    [SerializeField] private float highDangerHeartbeatInterval = 1.0f;

    private DangerLevel currentLevel = DangerLevel.None;
    private float       heartbeatTimer = 0f;
    private float       _evalTimer = 0f;
    private const float EvalInterval = 0.2f; // 5fps — モンスター移動は30-60秒ごとなのでこれで十分
    private Coroutine   vignetteRoutine;

    private enum DangerLevel { None, Lobby, Mid, High }

    // 危険レベルごとのビネット色 (RGB)
    private static readonly Color ColLobby  = new Color(0.5f, 0.5f, 0.0f); // 黄色
    private static readonly Color ColMid    = new Color(0.8f, 0.3f, 0.0f); // オレンジ
    private static readonly Color ColHigh   = new Color(0.9f, 0.0f, 0.0f); // 赤

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (dangerVignette == null) AutoFindReferences();
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

        // 危険レベル評価は5fps（モンスター移動は30-60秒ごとなので十分）
        _evalTimer += Time.deltaTime;
        if (_evalTimer >= EvalInterval)
        {
            _evalTimer = 0f;
            var newLevel = EvaluateDanger();
            if (newLevel != currentLevel)
            {
                var prev = currentLevel;
                currentLevel = newLevel;
                OnLevelChanged(prev, newLevel);
            }
        }

        if (currentLevel == DangerLevel.None) return;

        // ハートビートは毎フレーム計測（タイミング精度が必要）
        float interval = currentLevel switch
        {
            DangerLevel.High  => highDangerHeartbeatInterval,
            DangerLevel.Mid   => midDangerHeartbeatInterval,
            _                 => lobbyHeartbeatInterval
        };
        heartbeatTimer += Time.deltaTime;
        if (heartbeatTimer >= interval)
        {
            heartbeatTimer = 0f;
            AudioManager.Instance?.Play("heartbeat");
        }
    }

    private DangerLevel EvaluateDanger()
    {
        bool atDoor = false, atCorridor = false, atLobby = false;
        foreach (var m in MonsterManager.Instance.ActiveMonsters)
        {
            var loc = m.CurrentLocation;
            if (loc == FacilityLocation.B1_DoorFront)                      atDoor     = true;
            if (loc == FacilityLocation.B1_Corridor)                       atCorridor = true;
            if (loc == FacilityLocation.Lobby_Main || loc == FacilityLocation.Lobby_Stairs) atLobby = true;
        }
        if (atDoor)     return DangerLevel.High;
        if (atCorridor) return DangerLevel.Mid;
        if (atLobby)    return DangerLevel.Lobby;
        return DangerLevel.None;
    }

    private void OnLevelChanged(DangerLevel prev, DangerLevel next)
    {
        heartbeatTimer = 0f;
        if (vignetteRoutine != null) StopCoroutine(vignetteRoutine);

        // 危険度が上がったとき: カメラグリッチ音
        if (next > prev && next != DangerLevel.None)
            AudioManager.Instance?.Play("camera_static");

        if (next == DangerLevel.None)
        {
            if (dangerVignette) vignetteRoutine = StartCoroutine(FadeOutVignette());
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
            Color targetColor = currentLevel switch
            {
                DangerLevel.High  => ColHigh,
                DangerLevel.Mid   => ColMid,
                _                 => ColLobby
            };
            float baseA = currentLevel switch
            {
                DangerLevel.High  => 0.42f,
                DangerLevel.Mid   => 0.24f,
                _                 => 0.12f
            };
            float speed = currentLevel switch
            {
                DangerLevel.High  => 3.2f,
                DangerLevel.Mid   => 1.6f,
                _                 => 0.8f
            };

            float a = baseA + Mathf.Sin(Time.time * speed) * (baseA * 0.4f);
            dangerVignette.color = new Color(targetColor.r, targetColor.g, targetColor.b, a);
            yield return null;
        }

        yield return FadeOutVignette();
    }

    private IEnumerator FadeOutVignette()
    {
        if (dangerVignette == null) yield break;
        float startA = dangerVignette.color.a;
        float t      = 0f;
        float dur    = 0.5f;
        while (t < dur)
        {
            t += Time.deltaTime;
            var c = dangerVignette.color;
            c.a = Mathf.Lerp(startA, 0f, t / dur);
            dangerVignette.color = c;
            yield return null;
        }
        dangerVignette.gameObject.SetActive(false);
    }

    private void ResetAlert()
    {
        currentLevel   = DangerLevel.None;
        heartbeatTimer = 0f;
        if (vignetteRoutine != null) StopCoroutine(vignetteRoutine);
        if (dangerVignette) dangerVignette.gameObject.SetActive(false);
    }

    public void AutoFindReferences()
    {
        dangerVignette = FindDeep<Image>("DangerVignette");
    }

    private T FindDeep<T>(string name) where T : Component => FindIn<T>(transform, name);

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
