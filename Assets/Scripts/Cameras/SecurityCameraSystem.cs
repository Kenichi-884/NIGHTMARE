using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class CameraConfig
{
    public CameraID id;
    public string displayName;
    public FacilityLocation[] coveredLocations;
    public Sprite backgroundSprite;
    public bool isExternal;
}

public class SecurityCameraSystem : MonoBehaviour
{
    public static SecurityCameraSystem Instance { get; private set; }

    [Header("Camera Definitions")]
    [SerializeField] private List<CameraConfig> cameraConfigs;

    [Header("External Monitor")]
    [SerializeField] private Image extMonitorBG;
    [SerializeField] private Image extMonsterOverlay;
    [SerializeField] private Image extNoiseOverlay;
    [SerializeField] private Text extCameraLabel;
    [SerializeField] private Image extStaticOverlay;   // Mimic / dead時の砂嵐

    [Header("Internal Monitor")]
    [SerializeField] private Image intMonitorBG;
    [SerializeField] private Image intMonsterOverlay;
    [SerializeField] private Image intNoiseOverlay;
    [SerializeField] private Text intCameraLabel;
    [SerializeField] private Image intStaticOverlay;

    // 外部カメラは OUT_N、内部カメラは IN_1F_A がデフォルト
    public CameraID ActiveExternal { get; private set; } = CameraID.OUT_N;
    public CameraID ActiveInternal { get; private set; } = CameraID.IN_1F_A;

    private readonly Dictionary<CameraID, CameraConfig> configs = new Dictionary<CameraID, CameraConfig>();
    private readonly HashSet<CameraID> deadCameras = new HashSet<CameraID>();
    private CameraID? mimicTarget = null;

    // 現象用の一時的なフリッカーフラグ
    private bool extFlicker = false;
    private bool intFlicker = false;
    // ライト消灯（B1カメラが暗くなる）
    private bool lightsOut = false;

    public event Action<CameraID> OnCameraKilled;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // デフォルト設定（エディタで未設定の場合）
        if (cameraConfigs == null || cameraConfigs.Count == 0)
        {
            cameraConfigs = new List<CameraConfig>
            {
                new CameraConfig { id = CameraID.OUT_N,   displayName = "OUT-N  北ゲート",    coveredLocations = new[] { FacilityLocation.Outside_North }, isExternal = true },
                new CameraConfig { id = CameraID.OUT_E,   displayName = "OUT-E  東側",         coveredLocations = new[] { FacilityLocation.Outside_East  }, isExternal = true },
                new CameraConfig { id = CameraID.OUT_W,   displayName = "OUT-W  西側搬入口",   coveredLocations = new[] { FacilityLocation.Outside_West  }, isExternal = true },
                new CameraConfig { id = CameraID.OUT_TOP, displayName = "OUT-TOP 入口真上",    coveredLocations = new[] { FacilityLocation.Outside_Top   }, isExternal = true },
                new CameraConfig { id = CameraID.IN_1F_A, displayName = "IN-1F-A ロビー",      coveredLocations = new[] { FacilityLocation.Lobby_Main    }, isExternal = false },
                new CameraConfig { id = CameraID.IN_1F_B, displayName = "IN-1F-B 階段前",      coveredLocations = new[] { FacilityLocation.Lobby_Stairs  }, isExternal = false },
                new CameraConfig { id = CameraID.IN_B1_A, displayName = "IN-B1-A B1廊下",      coveredLocations = new[] { FacilityLocation.B1_Corridor   }, isExternal = false },
                new CameraConfig { id = CameraID.IN_B1_B, displayName = "IN-B1-B 管理人室前",  coveredLocations = new[] { FacilityLocation.B1_DoorFront  }, isExternal = false },
            };
        }

        foreach (var c in cameraConfigs) configs[c.id] = c;
    }

    private void Start()
    {
        GameManager.Instance.OnDayStarted += _ => ResetAllCameras();
    }

    private void Update()
    {
        if (GameManager.Instance.CurrentState != GameState.Night) return;
        RefreshMonitor(ActiveExternal, extMonitorBG, extMonsterOverlay, extNoiseOverlay, extCameraLabel, extStaticOverlay, extFlicker);
        RefreshMonitor(ActiveInternal, intMonitorBG, intMonsterOverlay, intNoiseOverlay, intCameraLabel, intStaticOverlay, intFlicker);
    }

    private void RefreshMonitor(CameraID id, Image bg, Image monsterImg, Image noise, Text label, Image staticImg, bool flicker)
    {
        if (!configs.TryGetValue(id, out var cfg)) return;

        bool isDead = deadCameras.Contains(id);
        bool isMimicked = mimicTarget == id;

        if (isDead || flicker)
        {
            bg.color = Color.black;
            bg.sprite = null;
            monsterImg.enabled = false;
            noise.enabled = false;
            staticImg.enabled = isDead;
            label.text = isDead ? $"{cfg.displayName}  [消灯]" : cfg.displayName;
            bg.GetComponent<CameraViewEffect>()?.SetGlitchIntensity(isDead ? 1f : 0.5f);
            return;
        }

        // 背景スプライト
        bg.sprite = cfg.backgroundSprite;
        bg.color = lightsOut && !cfg.isExternal ? new Color(0.15f, 0.15f, 0.15f) : Color.white;
        bg.GetComponent<CameraViewEffect>()?.SetGlitchIntensity(isMimicked ? 0.6f : 0f);
        label.text = isMimicked ? $"{cfg.displayName}  [!]" : cfg.displayName;

        // Mimic乗っ取り中は怪しい砂嵐をかける
        staticImg.enabled = isMimicked;

        // モンスターが映っているか
        var monster = isMimicked ? null : GetVisibleMonster(cfg);
        monsterImg.enabled = monster != null;
        if (monster != null) monsterImg.sprite = monster.CameraSprite;

        // Jammerが近くにいるとノイズ
        noise.enabled = IsJammerNear(cfg);
    }

    private MonsterBase GetVisibleMonster(CameraConfig cfg)
    {
        if (MonsterManager.Instance == null) return null;
        foreach (var m in MonsterManager.Instance.ActiveMonsters)
        {
            if (!m.IsVisible) continue;
            foreach (var loc in cfg.coveredLocations)
                if (m.CurrentLocation == loc) return m;
        }
        return null;
    }

    private bool IsJammerNear(CameraConfig cfg)
    {
        if (MonsterManager.Instance == null) return false;
        foreach (var m in MonsterManager.Instance.ActiveMonsters)
        {
            if (m is not JammerAI) continue;
            foreach (var loc in cfg.coveredLocations)
                if (m.CurrentLocation == loc) return true;
        }
        return false;
    }

    public void SwitchExternal(CameraID id)
    {
        if (!configs.TryGetValue(id, out var cfg) || !cfg.isExternal) return;
        ActiveExternal = id;
    }

    public void SwitchInternal(CameraID id)
    {
        if (!configs.TryGetValue(id, out var cfg) || cfg.isExternal) return;
        ActiveInternal = id;
    }

    // 指定ロケーションが現在のアクティブカメラに映っているか
    public bool IsLocationVisible(FacilityLocation loc)
    {
        if (configs.TryGetValue(ActiveExternal, out var ext))
            foreach (var l in ext.coveredLocations)
                if (l == loc) return true;

        if (configs.TryGetValue(ActiveInternal, out var intC))
            foreach (var l in intC.coveredLocations)
                if (l == loc) return true;

        return false;
    }

    public void KillCamera(CameraID id)
    {
        deadCameras.Add(id);
        OnCameraKilled?.Invoke(id);
    }

    // プレイヤーがカメラリセット操作 (-3% 電力消費)
    public bool TryResetCamera(CameraID id)
    {
        if (!PowerManager.Instance.Consume(3f)) return false;
        deadCameras.Remove(id);
        if (mimicTarget == id) mimicTarget = null;
        return true;
    }

    public void SetMimicTarget(CameraID? id) => mimicTarget = id;

    // 現象: フリッカー
    public void TriggerFlicker(bool external, float duration)
    {
        StartCoroutine(FlickerRoutine(external, duration));
    }

    private IEnumerator FlickerRoutine(bool external, float duration)
    {
        if (external) extFlicker = true; else intFlicker = true;
        yield return new WaitForSeconds(duration);
        if (external) extFlicker = false; else intFlicker = false;
    }

    // 現象: ライト消灯
    public void SetLightsOut(bool value) => lightsOut = value;

    // 現象: 全カメラブラックアウト
    public void TriggerBlackout(float duration)
    {
        StartCoroutine(BlackoutRoutine(duration));
    }

    private IEnumerator BlackoutRoutine(float duration)
    {
        extFlicker = true;
        intFlicker = true;
        yield return new WaitForSeconds(duration);
        extFlicker = false;
        intFlicker = false;
    }

    public bool IsCameraDead(CameraID id) => deadCameras.Contains(id);
    public bool IsCameraMimicked(CameraID id) => mimicTarget == id;
    public CameraConfig GetConfig(CameraID id) => configs.TryGetValue(id, out var c) ? c : null;

    // エディタセットアップから呼ぶ: 子オブジェクトを名前で自動接続
    public void AutoFindMonitors()
    {
        extMonitorBG      = FindDeep<Image>("ExtBG");
        extMonsterOverlay = FindDeep<Image>("ExtMonsterOverlay");
        extNoiseOverlay   = FindDeep<Image>("ExtNoiseOverlay");
        extStaticOverlay  = FindDeep<Image>("ExtStaticOverlay");
        extCameraLabel    = FindDeep<Text>("ExtCameraLabel");
        intMonitorBG      = FindDeep<Image>("IntBG");
        intMonsterOverlay = FindDeep<Image>("IntMonsterOverlay");
        intNoiseOverlay   = FindDeep<Image>("IntNoiseOverlay");
        intStaticOverlay  = FindDeep<Image>("IntStaticOverlay");
        intCameraLabel    = FindDeep<Text>("IntCameraLabel");
    }

    private T FindDeep<T>(string name) where T : Component
    {
        return FindDeepInTransform<T>(transform, name);
    }

    private T FindDeepInTransform<T>(Transform root, string name) where T : Component
    {
        foreach (Transform child in root)
        {
            if (child.name == name)
            {
                var c = child.GetComponent<T>();
                if (c != null) return c;
            }
            var found = FindDeepInTransform<T>(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private void ResetAllCameras()
    {
        deadCameras.Clear();
        mimicTarget = null;
        extFlicker = false;
        intFlicker = false;
        lightsOut = false;
        ActiveExternal = CameraID.OUT_N;
        ActiveInternal = CameraID.IN_1F_A;
    }
}
