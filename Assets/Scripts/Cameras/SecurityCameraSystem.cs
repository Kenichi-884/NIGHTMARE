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
    public bool isExternal;
    // シーン内の Unity Camera は名前（SceneCam_OUT_N 等）で自動検索する
}

public class SecurityCameraSystem : MonoBehaviour
{
    public static SecurityCameraSystem Instance { get; private set; }

    [Header("Camera Definitions")]
    [SerializeField] private List<CameraConfig> cameraConfigs;

    [Header("Monitor (1台)")]
    [SerializeField] private RawImage monitorDisplay;       // RenderTexture を表示する RawImage
    [SerializeField] private Text     cameraNameText;       // カメラ名ラベル
    [SerializeField] private Text     cameraLocationText;   // 監視中の場所テキスト
    [SerializeField] private Image    noiseOverlay;         // Jammer ノイズ
    [SerializeField] private Image    staticOverlay;        // 死亡/Mimic スタティック
    [SerializeField] private Image    monsterOverlay;       // モンスター表示オーバーレイ

    // 全 8 台のサイクル順序
    private static readonly CameraID[] CycleOrder =
    {
        CameraID.OUT_N, CameraID.OUT_E, CameraID.OUT_W, CameraID.OUT_TOP,
        CameraID.IN_1F_A, CameraID.IN_1F_B, CameraID.IN_B1_A, CameraID.IN_B1_B
    };
    private int _cycleIndex = 0;
    private CameraID _activeCamera = CameraID.OUT_N;

    public CameraID ActiveCamera       => _activeCamera;
    // 後方互換性（UIManager / FacilityMapUI が参照）
    public CameraID ActiveExternal     => _activeCamera;
    public CameraID ActiveInternal     => _activeCamera;
    public int      CameraCount        => CycleOrder.Length;
    public int      CurrentCameraIndex => _cycleIndex;

    private readonly Dictionary<CameraID, CameraConfig> configs      = new();
    private readonly Dictionary<CameraID, Camera>       sceneCams    = new();
    private readonly Dictionary<CameraID, RenderTexture> renderTextures = new();
    private readonly HashSet<CameraID>                   deadCameras  = new();
    private CameraID? mimicTarget  = null;
    private bool      flickerActive = false;
    private bool      lightsOut     = false;

    public event Action<CameraID> OnCameraKilled;

    // ===== 初期化 =====
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (cameraConfigs == null || cameraConfigs.Count == 0)
            BuildDefaultConfigs();

        foreach (var c in cameraConfigs) configs[c.id] = c;

        // シーン内の Security Camera を名前で検索して登録
        foreach (var cfg in cameraConfigs)
        {
            string goName = $"SceneCam_{cfg.id}";
            var go = GameObject.Find(goName);
            if (go == null) continue;
            var cam = go.GetComponent<Camera>();
            if (cam == null) continue;

            // RenderTexture を生成してカメラに割り当て
            var rt = new RenderTexture(512, 384, 16, RenderTextureFormat.Default);
            rt.name = $"RT_{cfg.id}";
            cam.targetTexture = rt;
            cam.enabled = false;  // 表示中のカメラだけ有効化
            sceneCams[cfg.id]    = cam;
            renderTextures[cfg.id] = rt;
        }

        if (monitorDisplay == null) AutoFindMonitors();
    }

    private void Start()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnDayStarted += _ => ResetAllCameras();

        ApplyActiveCamera();
        RefreshMonitorState();  // 初期テクスチャをセット

        Debug.Log($"[CameraSystem] 起動 monitor={monitorDisplay != null} " +
                  $"sceneCams={sceneCams.Count} configs={configs.Count}");
    }

    // ===== 毎フレーム更新 =====
    private float _dbgTimer = 0f;

    private void Update()
    {
        bool isNight = GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Night;
        if (!isNight) return;

        _dbgTimer += Time.deltaTime;
        if (_dbgTimer >= 5f)
        {
            _dbgTimer = 0f;
            Debug.Log($"[CameraSystem] Active={_activeCamera} " +
                      $"sceneCam={sceneCams.ContainsKey(_activeCamera)} monitor={monitorDisplay != null}");
        }

        RefreshMonitorState();
        RefreshMonsterOverlay();
    }

    private void RefreshMonsterOverlay()
    {
        if (monsterOverlay == null) return;
        if (!configs.TryGetValue(_activeCamera, out var cfg)) { monsterOverlay.enabled = false; return; }

        var monsters = MonsterManager.Instance?.ActiveMonsters;
        if (monsters == null || monsters.Count == 0) { monsterOverlay.enabled = false; return; }

        MonsterBase found = null;
        foreach (var m in monsters)
        {
            if (!m.IsVisible) continue;
            foreach (var loc in cfg.coveredLocations)
            {
                if (m.CurrentLocation == loc) { found = m; break; }
            }
            if (found != null) break;
        }

        if (found != null && found.CameraSprite != null)
        {
            monsterOverlay.sprite  = found.CameraSprite;
            monsterOverlay.enabled = true;
        }
        else
        {
            monsterOverlay.enabled = false;
        }
    }

    private void RefreshMonitorState()
    {
        if (!configs.TryGetValue(_activeCamera, out var cfg)) return;

        bool isDead     = deadCameras.Contains(_activeCamera);
        bool isMimicked = mimicTarget == _activeCamera;

        // カメラ名テキスト
        if (cameraNameText)
        {
            string suffix = isDead ? "  [消灯]" : isMimicked ? "  [!]" : "";
            cameraNameText.text = cfg.displayName + suffix;
        }

        // 監視中の場所テキスト
        if (cameraLocationText)
        {
            string loc = (cfg.coveredLocations != null && cfg.coveredLocations.Length > 0)
                ? LocationNameJP(cfg.coveredLocations[0]) : "";
            cameraLocationText.text = $"監視中:  {loc}";
        }

        // Jammer ノイズオーバーレイ
        if (noiseOverlay)  noiseOverlay.enabled  = !isDead && IsJammerNear(cfg);

        // スタティックオーバーレイ（死亡 or Mimic）
        if (staticOverlay) staticOverlay.enabled = isDead || isMimicked;

        // モニター表示
        if (monitorDisplay)
        {
            bool offline = isDead || flickerActive;
            if (offline)
            {
                monitorDisplay.texture = null;
                monitorDisplay.color   = Color.black;
            }
            else
            {
                bool dark = lightsOut && !cfg.isExternal;
                monitorDisplay.color = dark ? new Color(0.15f, 0.15f, 0.15f) : Color.white;
                renderTextures.TryGetValue(_activeCamera, out var rt);
                monitorDisplay.texture = rt;
            }
        }
    }

    // ===== カメラ切替（共通） =====
    public void SwitchCamera(CameraID id)
    {
        if (!configs.ContainsKey(id)) { Debug.LogWarning($"[CameraSystem] SwitchCamera 失敗: {id}"); return; }
        Debug.Log($"[CameraSystem] カメラ切替 → {id}");
        _activeCamera = id;
        for (int i = 0; i < CycleOrder.Length; i++)
            if (CycleOrder[i] == id) { _cycleIndex = i; break; }
        ApplyActiveCamera();
    }

    // 後方互換ラッパー（UIManager が SwitchExternal/SwitchInternal を呼ぶため）
    public void SwitchExternal(CameraID id) => SwitchCamera(id);
    public void SwitchInternal(CameraID id) => SwitchCamera(id);

    // ===== キーボード操作 =====
    public void CycleCamera(int dir)
    {
        _cycleIndex   = (_cycleIndex + dir + CycleOrder.Length) % CycleOrder.Length;
        _activeCamera = CycleOrder[_cycleIndex];
        ApplyActiveCamera();
    }

    public void SetCameraByIndex(int idx)
    {
        if (idx < 0 || idx >= CycleOrder.Length) return;
        _cycleIndex   = idx;
        _activeCamera = CycleOrder[_cycleIndex];
        ApplyActiveCamera();
    }

    // アクティブカメラだけ有効化し RenderTexture を表示する
    private void ApplyActiveCamera()
    {
        foreach (var kvp in sceneCams)
            kvp.Value.enabled = (kvp.Key == _activeCamera) && !deadCameras.Contains(kvp.Key);
    }

    // ===== ロケーション可視チェック =====
    public bool IsLocationVisible(FacilityLocation loc)
    {
        if (configs.TryGetValue(_activeCamera, out var cfg))
            foreach (var l in cfg.coveredLocations)
                if (l == loc) return true;
        return false;
    }

    // ===== カメラ状態 =====
    public void KillCamera(CameraID id)
    {
        deadCameras.Add(id);
        if (sceneCams.TryGetValue(id, out var cam)) cam.enabled = false;
        OnCameraKilled?.Invoke(id);
    }

    public bool TryResetCamera(CameraID id)
    {
        if (PowerManager.Instance == null || !PowerManager.Instance.Consume(3f)) return false;
        deadCameras.Remove(id);
        if (mimicTarget == id) mimicTarget = null;
        ApplyActiveCamera();
        return true;
    }

    public void SetMimicTarget(CameraID? id) => mimicTarget = id;

    // ===== 現象 =====
    public void TriggerFlicker(bool external, float duration)
        => StartCoroutine(FlickerRoutine(duration));

    private IEnumerator FlickerRoutine(float duration)
    {
        flickerActive = true;
        if (sceneCams.TryGetValue(_activeCamera, out var cam)) cam.enabled = false;
        yield return new WaitForSeconds(duration);
        flickerActive = false;
        ApplyActiveCamera();
    }

    public void SetLightsOut(bool value) => lightsOut = value;

    public void TriggerBlackout(float duration)
        => StartCoroutine(BlackoutRoutine(duration));

    private IEnumerator BlackoutRoutine(float duration)
    {
        flickerActive = true;
        foreach (var cam in sceneCams.Values) cam.enabled = false;
        yield return new WaitForSeconds(duration);
        flickerActive = false;
        ApplyActiveCamera();
    }

    public bool IsCameraDead(CameraID id)     => deadCameras.Contains(id);
    public bool IsCameraMimicked(CameraID id) => mimicTarget == id;
    public CameraConfig GetConfig(CameraID id) => configs.TryGetValue(id, out var c) ? c : null;

    // ===== ユーティリティ =====
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

    private string LocationNameJP(FacilityLocation loc) => loc switch
    {
        FacilityLocation.Outside_North => "北ゲート外部",
        FacilityLocation.Outside_East  => "東側外部",
        FacilityLocation.Outside_West  => "西側搬入口",
        FacilityLocation.Outside_Top   => "施設入口前",
        FacilityLocation.Lobby_Main    => "1F ロビー",
        FacilityLocation.Lobby_Stairs  => "1F 階段前",
        FacilityLocation.B1_Corridor   => "B1 廊下",
        FacilityLocation.B1_DoorFront  => "B1 管理人室前",
        _ => loc.ToString()
    };

    private void ResetAllCameras()
    {
        deadCameras.Clear();
        mimicTarget   = null;
        flickerActive = false;
        lightsOut     = false;
        _activeCamera = CameraID.OUT_N;
        _cycleIndex   = 0;
        ApplyActiveCamera();
    }

    // ===== デフォルト設定 =====
    private void BuildDefaultConfigs()
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

    // ===== ギズモ =====
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        var cfgs = (cameraConfigs != null && cameraConfigs.Count > 0)
            ? cameraConfigs : GizmoDefaultConfigs();

        foreach (var cfg in cfgs)
        {
            var go = GameObject.Find($"SceneCam_{cfg.id}");
            if (go == null) continue;

            Color col = cfg.isExternal ? new Color(0.2f, 0.85f, 1f) : new Color(0.25f, 1f, 0.5f);
            Gizmos.color = col;
            Gizmos.DrawWireSphere(go.transform.position, 0.25f);
            Gizmos.DrawRay(go.transform.position, go.transform.forward * 4f);

            var style = new GUIStyle { fontSize = 10 };
            style.normal.textColor = col;
            UnityEditor.Handles.Label(go.transform.position + Vector3.up * 0.45f, cfg.displayName, style);
        }
    }

    private void OnDrawGizmosSelected()
    {
        var cfgs = (cameraConfigs != null && cameraConfigs.Count > 0)
            ? cameraConfigs : GizmoDefaultConfigs();

        foreach (var cfg in cfgs)
        {
            var go = GameObject.Find($"SceneCam_{cfg.id}");
            if (go == null) continue;

            var cam = go.GetComponent<Camera>();
            if (cam == null) continue;

            Gizmos.color = new Color(1f, 1f, 0.3f, 0.25f);
            Matrix4x4 prev = Gizmos.matrix;
            Gizmos.matrix = go.transform.localToWorldMatrix;
            Gizmos.DrawFrustum(Vector3.zero, cam.fieldOfView, 10f, cam.nearClipPlane, cam.aspect);
            Gizmos.matrix = prev;
        }
    }

    private static List<CameraConfig> GizmoDefaultConfigs() => new List<CameraConfig>
    {
        new CameraConfig { id = CameraID.OUT_N,   displayName = "OUT-N  北ゲート",  isExternal = true  },
        new CameraConfig { id = CameraID.OUT_E,   displayName = "OUT-E  東側",      isExternal = true  },
        new CameraConfig { id = CameraID.OUT_W,   displayName = "OUT-W  西側",      isExternal = true  },
        new CameraConfig { id = CameraID.OUT_TOP, displayName = "OUT-TOP 入口真上", isExternal = true  },
        new CameraConfig { id = CameraID.IN_1F_A, displayName = "IN-1F-A ロビー",   isExternal = false },
        new CameraConfig { id = CameraID.IN_1F_B, displayName = "IN-1F-B 階段前",   isExternal = false },
        new CameraConfig { id = CameraID.IN_B1_A, displayName = "IN-B1-A B1廊下",  isExternal = false },
        new CameraConfig { id = CameraID.IN_B1_B, displayName = "IN-B1-B 管理室前",isExternal = false },
    };
#endif

    // ===== エディタセットアップ =====
    public void AutoFindMonitors()
    {
        monitorDisplay      = FindDeep<RawImage>("MonitorDisplay");
        cameraNameText      = FindDeep<Text>("CameraNameText");
        cameraLocationText  = FindDeep<Text>("CameraLocationText");
        noiseOverlay        = FindDeep<Image>("MonitorNoiseOverlay");
        staticOverlay       = FindDeep<Image>("MonitorStaticOverlay");
        monsterOverlay      = FindDeep<Image>("MonitorMonsterOverlay");
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
