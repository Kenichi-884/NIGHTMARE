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

    [Header("External Monitor (Left)")]
    [SerializeField] private Image extMonitorBG;
    [SerializeField] private Image extMonsterOverlay;
    [SerializeField] private Image extNoiseOverlay;
    [SerializeField] private Text  extCameraLabel;
    [SerializeField] private Image extStaticOverlay;

    [Header("Internal Monitor (Right)")]
    [SerializeField] private Image intMonitorBG;
    [SerializeField] private Image intMonsterOverlay;
    [SerializeField] private Image intNoiseOverlay;
    [SerializeField] private Text  intCameraLabel;
    [SerializeField] private Image intStaticOverlay;

    // カメラサイクル順（← → キー / 1〜8 キーで操作）
    // 0-3: External, 4-7: Internal
    private static readonly CameraID[] CycleOrder =
    {
        CameraID.OUT_N, CameraID.OUT_E, CameraID.OUT_W, CameraID.OUT_TOP,
        CameraID.IN_1F_A, CameraID.IN_1F_B, CameraID.IN_B1_A, CameraID.IN_B1_B
    };
    private int _cycleIndex = 0;

    // 各モニターの現在選択カメラ
    private CameraID _activeExternal = CameraID.OUT_N;
    private CameraID _activeInternal = CameraID.IN_1F_A;

    public CameraID ActiveExternal     => _activeExternal;
    public CameraID ActiveInternal     => _activeInternal;
    /// <summary>キーボード操作で最後に選んだカメラ (R キーでリセット対象)</summary>
    public CameraID ActiveCamera       => CycleOrder[_cycleIndex];
    public int      CameraCount        => CycleOrder.Length;
    public int      CurrentCameraIndex => _cycleIndex;

    private readonly Dictionary<CameraID, CameraConfig> configs     = new();
    private readonly HashSet<CameraID>                  deadCameras = new();
    private CameraID? mimicTarget = null;

    private bool extFlicker = false;
    private bool intFlicker = false;
    private bool lightsOut  = false;

    public event Action<CameraID> OnCameraKilled;

    // ===== 初期化 =====
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (cameraConfigs == null || cameraConfigs.Count == 0)
        {
            cameraConfigs = new List<CameraConfig>
            {
                new CameraConfig { id = CameraID.OUT_N,   displayName = "OUT-N  北ゲート",     coveredLocations = new[] { FacilityLocation.Outside_North }, isExternal = true  },
                new CameraConfig { id = CameraID.OUT_E,   displayName = "OUT-E  東側",          coveredLocations = new[] { FacilityLocation.Outside_East  }, isExternal = true  },
                new CameraConfig { id = CameraID.OUT_W,   displayName = "OUT-W  西側搬入口",    coveredLocations = new[] { FacilityLocation.Outside_West  }, isExternal = true  },
                new CameraConfig { id = CameraID.OUT_TOP, displayName = "OUT-TOP 入口真上",     coveredLocations = new[] { FacilityLocation.Outside_Top   }, isExternal = true  },
                new CameraConfig { id = CameraID.IN_1F_A, displayName = "IN-1F-A ロビー",       coveredLocations = new[] { FacilityLocation.Lobby_Main    }, isExternal = false },
                new CameraConfig { id = CameraID.IN_1F_B, displayName = "IN-1F-B 階段前",       coveredLocations = new[] { FacilityLocation.Lobby_Stairs  }, isExternal = false },
                new CameraConfig { id = CameraID.IN_B1_A, displayName = "IN-B1-A B1廊下",       coveredLocations = new[] { FacilityLocation.B1_Corridor   }, isExternal = false },
                new CameraConfig { id = CameraID.IN_B1_B, displayName = "IN-B1-B 管理人室前",   coveredLocations = new[] { FacilityLocation.B1_DoorFront  }, isExternal = false },
            };
        }

        foreach (var c in cameraConfigs) configs[c.id] = c;

        if (extMonitorBG == null) AutoFindMonitors();
    }

    private void Start()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnDayStarted += _ => ResetAllCameras();

        // 起動時の参照チェック
        Debug.Log($"[CameraSystem] 起動 extMonitorBG={extMonitorBG != null}, intMonitorBG={intMonitorBG != null}, configs={configs.Count}");
    }

    private float _dbgTimer = 0f;

    // ===== 毎フレーム更新 =====
    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Night) return;

        // 5秒ごとに現在のカメラ状態をログ出力
        _dbgTimer += Time.deltaTime;
        if (_dbgTimer >= 5f)
        {
            _dbgTimer = 0f;
            configs.TryGetValue(_activeExternal, out var extCfg);
            configs.TryGetValue(_activeInternal, out var intCfg);
            Debug.Log($"[CameraSystem] Ext={_activeExternal} sprite={extCfg?.backgroundSprite != null} bgEnabled={extMonitorBG?.enabled} bgGO={extMonitorBG?.gameObject.activeInHierarchy}");
            Debug.Log($"[CameraSystem] Int={_activeInternal} sprite={intCfg?.backgroundSprite != null} bgEnabled={intMonitorBG?.enabled} bgGO={intMonitorBG?.gameObject.activeInHierarchy}");
            Debug.Log($"[CameraSystem] GameScreen={extMonitorBG?.transform.root.name} / extMonitorBG.color={extMonitorBG?.color}");
        }

        RefreshMonitor(_activeExternal, extMonitorBG, extMonsterOverlay, extNoiseOverlay, extCameraLabel, extStaticOverlay, extFlicker);
        RefreshMonitor(_activeInternal, intMonitorBG, intMonsterOverlay, intNoiseOverlay, intCameraLabel, intStaticOverlay, intFlicker);
    }

    private void RefreshMonitor(CameraID id, Image bg, Image monsterImg, Image noise,
                                Text label, Image staticImg, bool flicker)
    {
        if (bg == null) return;
        if (!configs.TryGetValue(id, out var cfg)) return;

        bool isDead     = deadCameras.Contains(id);
        bool isMimicked = mimicTarget == id;

        if (isDead || flicker)
        {
            bg.color = Color.black;
            bg.sprite = null;
            if (monsterImg) monsterImg.enabled = false;
            if (noise)      noise.enabled = false;
            if (staticImg)  staticImg.enabled = isDead;
            if (label)      label.text = isDead ? $"{cfg.displayName}  [消灯]" : cfg.displayName;
            bg.GetComponent<CameraViewEffect>()?.SetGlitchIntensity(isDead ? 1f : 0.5f);
            return;
        }

        bg.sprite = cfg.backgroundSprite;
        bg.color  = lightsOut && !cfg.isExternal ? new Color(0.15f, 0.15f, 0.15f) : Color.white;
        bg.GetComponent<CameraViewEffect>()?.SetGlitchIntensity(isMimicked ? 0.6f : 0f);
        if (label)     label.text = isMimicked ? $"{cfg.displayName}  [!]" : cfg.displayName;
        if (staticImg) staticImg.enabled = isMimicked;

        var monster = isMimicked ? null : GetVisibleMonster(cfg);
        if (monsterImg)
        {
            monsterImg.enabled = monster != null;
            if (monster != null) monsterImg.sprite = monster.CameraSprite;
        }
        if (noise) noise.enabled = IsJammerNear(cfg);
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

    // ===== カメラ切替 (ボタン操作) =====
    public void SwitchExternal(CameraID id)
    {
        if (!configs.TryGetValue(id, out var cfg) || !cfg.isExternal) { Debug.LogWarning($"[CameraSystem] SwitchExternal 失敗: id={id}"); return; }
        Debug.Log($"[CameraSystem] 外部カメラ切替 → {id}");
        _activeExternal = id;
    }

    public void SwitchInternal(CameraID id)
    {
        if (!configs.TryGetValue(id, out var cfg) || cfg.isExternal) { Debug.LogWarning($"[CameraSystem] SwitchInternal 失敗: id={id}"); return; }
        Debug.Log($"[CameraSystem] 内部カメラ切替 → {id}");
        _activeInternal = id;
    }

    // ===== カメラ切替 (キーボード操作) =====
    /// <summary>← → キー: 全8台を順に切替。外部/内部を自動振り分け</summary>
    public void CycleCamera(int dir)
    {
        _cycleIndex = (_cycleIndex + dir + CycleOrder.Length) % CycleOrder.Length;
        ApplyCycleIndex();
    }

    /// <summary>1〜8 キー: インデックス直接指定</summary>
    public void SetCameraByIndex(int idx)
    {
        if (idx < 0 || idx >= CycleOrder.Length) return;
        _cycleIndex = idx;
        ApplyCycleIndex();
    }

    private void ApplyCycleIndex()
    {
        var id = CycleOrder[_cycleIndex];
        if (configs.TryGetValue(id, out var cfg))
        {
            if (cfg.isExternal) _activeExternal = id;
            else                _activeInternal = id;
        }
    }

    // ===== カメラ状態 =====
    public bool IsLocationVisible(FacilityLocation loc)
    {
        if (configs.TryGetValue(_activeExternal, out var ext))
            foreach (var l in ext.coveredLocations)
                if (l == loc) return true;

        if (configs.TryGetValue(_activeInternal, out var intC))
            foreach (var l in intC.coveredLocations)
                if (l == loc) return true;

        return false;
    }

    public void KillCamera(CameraID id)
    {
        deadCameras.Add(id);
        OnCameraKilled?.Invoke(id);
    }

    public bool TryResetCamera(CameraID id)
    {
        if (PowerManager.Instance == null || !PowerManager.Instance.Consume(3f)) return false;
        deadCameras.Remove(id);
        if (mimicTarget == id) mimicTarget = null;
        return true;
    }

    public void SetMimicTarget(CameraID? id) => mimicTarget = id;

    // ===== 現象 =====
    public void TriggerFlicker(bool external, float duration)
        => StartCoroutine(FlickerRoutine(external, duration));

    private IEnumerator FlickerRoutine(bool external, float duration)
    {
        if (external) extFlicker = true; else intFlicker = true;
        yield return new WaitForSeconds(duration);
        if (external) extFlicker = false; else intFlicker = false;
    }

    public void SetLightsOut(bool value) => lightsOut = value;

    public void TriggerBlackout(float duration)
        => StartCoroutine(BlackoutRoutine(duration));

    private IEnumerator BlackoutRoutine(float duration)
    {
        extFlicker = intFlicker = true;
        yield return new WaitForSeconds(duration);
        extFlicker = intFlicker = false;
    }

    public bool IsCameraDead(CameraID id)     => deadCameras.Contains(id);
    public bool IsCameraMimicked(CameraID id) => mimicTarget == id;
    public CameraConfig GetConfig(CameraID id) => configs.TryGetValue(id, out var c) ? c : null;

    private void ResetAllCameras()
    {
        deadCameras.Clear();
        mimicTarget   = null;
        extFlicker    = false;
        intFlicker    = false;
        lightsOut     = false;
        _activeExternal = CameraID.OUT_N;
        _activeInternal = CameraID.IN_1F_A;
        _cycleIndex   = 0;
    }

    // ===== エディタセットアップ =====
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
        => FindDeepIn<T>(transform, name);

    private T FindDeepIn<T>(Transform root, string name) where T : Component
    {
        foreach (Transform child in root)
        {
            if (child.name == name) { var c = child.GetComponent<T>(); if (c) return c; }
            var found = FindDeepIn<T>(child, name);
            if (found) return found;
        }
        return null;
    }
}
