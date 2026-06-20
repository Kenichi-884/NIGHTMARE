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
    [Tooltip("各監視カメラの設定リスト（ID・表示名・カバーエリア・内外判定）。空欄だとデフォルト8台構成が自動生成される。")]
    [SerializeField] private List<CameraConfig> cameraConfigs;

    [Header("Monitor (1台)")]
    [Tooltip("カメラ映像を表示するRawImage。RenderTextureが割り当てられる。")]
    [SerializeField] private RawImage monitorDisplay;
    [Tooltip("現在選択中のカメラ名を表示するTextコンポーネント。")]
    [SerializeField] private Text     cameraNameText;
    [Tooltip("監視中のエリア名（日本語）を表示するTextコンポーネント。")]
    [SerializeField] private Text     cameraLocationText;
    [Tooltip("Jammerが近くにいるときに表示するノイズオーバーレイImage。")]
    [SerializeField] private Image    noiseOverlay;
    [Tooltip("カメラが死亡またはMimicに乗っ取られたときに表示するスタティックオーバーレイImage。")]
    [SerializeField] private Image    staticOverlay;
    [Tooltip("モンスターのカメラスプライトを表示するオーバーレイImage。")]
    [SerializeField] private Image    monsterOverlay;

    [Header("Ghost Signal")]
    [Tooltip("GhostSignal現象が発生したときに一時的にカメラに映るスプライト。")]
    [SerializeField] private Sprite   ghostSignalSprite;

    [Header("Camera Ambient Beeps")]
    [Tooltip("カメラ使用中にランダム再生する電子音クリップ。複数登録するとランダムに選ばれる。")]
    [SerializeField] private AudioClip[] beepSounds;
    [Tooltip("電子音が鳴る間隔の最小秒数。")]
    [SerializeField] private float beepIntervalMin = 3f;
    [Tooltip("電子音が鳴る間隔の最大秒数。")]
    [SerializeField] private float beepIntervalMax = 10f;
    [Tooltip("電子音の音量。0=無音、1=最大。")]
    [SerializeField, Range(0f, 1f)] private float beepVolume = 0.5f;

    [Header("Camera FOV / Resolution")]
    [Tooltip("各監視カメラの視野角。大きいほど広角になる。監視カメラらしくするなら 75〜90 推奨。")]
    [SerializeField, Range(30f, 120f)] private float cameraFOV = 80f;
    [Tooltip("RenderTextureの幅(px)。TVが16:9なら 512、4:3なら 512 のまま。")]
    [SerializeField] private int rtWidth  = 512;
    [Tooltip("RenderTextureの高さ(px)。16:9なら 288、4:3なら 384。")]
    [SerializeField] private int rtHeight = 288;

    [Header("Monitor CRT Effect")]
    [Tooltip("ONにするとGraphics.BlitでCRTシェーダーを適用する。OFFだとカメラ映像そのまま。")]
    [SerializeField] private bool   enableCRTEffect   = true;
    [Tooltip("NIGHTMARE/SecurityCamera シェーダーをここにアサインする。空欄だとShader.Findで自動検索。")]
    [SerializeField] private Shader securityCamShader;
    [Tooltip("色収差の強さ。端に向かうほどRGBがズレる。大きいほど端がにじむ。")]
    [SerializeField, Range(0f, 0.02f)] private float chromaStr      = 0.004f;
    [Tooltip("走査線の濃さ。大きいほど横線が目立つ。0で無効。")]
    [SerializeField, Range(0f, 1f)]    private float scanlineAlpha  = 0.28f;
    [Tooltip("走査線の本数。多いほど線が細かくなる。")]
    [SerializeField, Range(80f,300f)]  private float scanlineCount  = 180f;
    [Tooltip("フィルムグレイン（砂粒ノイズ）の強さ。大きいほどザラザラ感が増す。")]
    [SerializeField, Range(0f, 0.3f)]  private float grainStr       = 0.038f;
    [Tooltip("画面周辺の暗さ。大きいほど端が暗くなる。1.0が標準的な監視カメラ感。")]
    [SerializeField, Range(0f, 2f)]    private float vignetteStr    = 1.0f;
    [Tooltip("色の鮮やかさ。0=完全白黒、1=元の色のまま。監視カメラらしくするには0に近い値を推奨。")]
    [SerializeField, Range(0f, 1f)]    private float saturation     = 0.0f;
    [Tooltip("映像全体の明るさ倍率。1.0が等倍。高くすると白飛び気味になる。")]
    [SerializeField, Range(0.5f,2f)]   private float brightness     = 1.10f;
    [Tooltip("映像を寒色系の白に近づける強さ。0=効果なし、高いほど青白い監視カメラらしい色温度になる。")]
    [SerializeField, Range(0f, 1f)]    private float whiteTint      = 0.15f;
    [Tooltip("画質の粗さ。小さいほどブロック状にピクセル化されて低解像度カメラっぽくなる。512=劣化なし。")]
    [SerializeField, Range(32f,512f)]  private float pixelScale     = 160f;
    [Tooltip("常時かかる軽いグリッチの強さ。0で無効、高いほど常にちらつく。")]
    [SerializeField, Range(0f, 1f)]    private float glitchBase     = 0.08f;
    [Tooltip("ランダムなCRTパルス（色収差急増）が起きる間隔の最小秒数。")]
    [SerializeField] private float crtPulseIntervalMin = 6f;
    [Tooltip("ランダムなCRTパルスが起きる間隔の最大秒数。")]
    [SerializeField] private float crtPulseIntervalMax = 20f;

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

    // TVMonitorDisplay が TV メッシュに貼り付けるための RenderTexture
    public RenderTexture MonitorRenderTexture
    {
        get
        {
            if (enableCRTEffect && _processedRT != null) return _processedRT;
            renderTextures.TryGetValue(_activeCamera, out var rt);
            return rt;
        }
    }

    private readonly Dictionary<CameraID, CameraConfig> configs      = new();
    private readonly Dictionary<CameraID, Camera>       sceneCams    = new();
    private readonly Dictionary<CameraID, RenderTexture> renderTextures = new();
    private readonly HashSet<CameraID>                   deadCameras  = new();
    private CameraID? mimicTarget  = null;
    private bool      flickerActive  = false;
    private bool      lightsOut      = false;
    private bool      _transitioning = false;
    private Coroutine _transRoutine;
    private Coroutine _beepRoutine;

    private CameraViewEffect _glitchEffect;
    private Material         _secCamMat;
    private RenderTexture    _processedRT;
    private Coroutine        _chromaPulseRoutine;

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
            var rt = new RenderTexture(rtWidth, rtHeight, 16, RenderTextureFormat.Default);
            rt.name = $"RT_{cfg.id}";
            cam.targetTexture  = rt;
            cam.fieldOfView    = cameraFOV;
            cam.enabled        = false;  // 表示中のカメラだけ有効化
            sceneCams[cfg.id]    = cam;
            renderTextures[cfg.id] = rt;
        }

        if (monitorDisplay == null) AutoFindMonitors();
    }

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnDayStarted  += OnDayStarted;
            GameManager.Instance.OnNightCleared += _ => StopBeepLoop();
            GameManager.Instance.OnGameOver     += StopBeepLoop;
        }

        ApplyActiveCamera();
        RefreshMonitorState();  // 初期テクスチャをセット

        if (enableCRTEffect) ApplyMonitorCRTEffect();

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
        if (_transitioning) return; // カメラ切替トランジション中は更新しない
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
                // CRTエフェクトON時は _processedRT (CRTBlitLoop が毎フレーム更新)
                if (enableCRTEffect && _processedRT != null)
                    monitorDisplay.texture = _processedRT;
                else
                {
                    renderTextures.TryGetValue(_activeCamera, out var rt);
                    monitorDisplay.texture = rt;
                }
            }
        }
    }

    // ===== カメラ切替（共通） =====
    public void SwitchCamera(CameraID id)
    {
        if (!configs.ContainsKey(id)) { Debug.LogWarning($"[CameraSystem] SwitchCamera 失敗: {id}"); return; }
        _activeCamera = id;
        for (int i = 0; i < CycleOrder.Length; i++)
            if (CycleOrder[i] == id) { _cycleIndex = i; break; }

        // 夜間のみトランジション（初期化時はスキップ）
        bool isNight = GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Night;
        if (isNight)
        {
            if (_transRoutine != null) StopCoroutine(_transRoutine);
            _transRoutine = StartCoroutine(SwitchTransitionRoutine());
        }
        else
        {
            ApplyActiveCamera();
        }
    }

    private IEnumerator SwitchTransitionRoutine()
    {
        _transitioning = true;

        // 瞬間暗転 + ノイズ音
        if (monitorDisplay) { monitorDisplay.texture = null; monitorDisplay.color = Color.black; }
        AudioManager.Instance?.Play("camera_static");

        // ポーズ中でも動作するよう unscaled time を使用
        yield return new WaitForSecondsRealtime(0.07f);

        ApplyActiveCamera();
        _transitioning = false;
    }

    // 後方互換ラッパー（UIManager が SwitchExternal/SwitchInternal を呼ぶため）
    public void SwitchExternal(CameraID id) => SwitchCamera(id);
    public void SwitchInternal(CameraID id) => SwitchCamera(id);

    // ===== キーボード操作 =====
    public void CycleCamera(int dir)
    {
        int next = (_cycleIndex + dir + CycleOrder.Length) % CycleOrder.Length;
        SwitchCamera(CycleOrder[next]);
    }

    public void SetCameraByIndex(int idx)
    {
        if (idx < 0 || idx >= CycleOrder.Length) return;
        SwitchCamera(CycleOrder[idx]);
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

    // GhostSignal 現象: 現在のカメラに幽霊スプライトを duration 秒表示する
    public void ShowGhostSignal(float duration)
        => StartCoroutine(GhostSignalRoutine(duration));

    private IEnumerator GhostSignalRoutine(float duration)
    {
        if (monsterOverlay == null || ghostSignalSprite == null) yield break;
        var prevSprite   = monsterOverlay.sprite;
        var prevEnabled  = monsterOverlay.enabled;
        monsterOverlay.sprite  = ghostSignalSprite;
        monsterOverlay.enabled = true;
        yield return new WaitForSeconds(duration);
        monsterOverlay.sprite  = prevSprite;
        monsterOverlay.enabled = prevEnabled;
    }

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

    // ===== Monitor CRT Effect =====
    private void ApplyMonitorCRTEffect()
    {
        if (monitorDisplay == null) return;

        var shader = securityCamShader != null
            ? securityCamShader
            : Shader.Find("NIGHTMARE/SecurityCamera");

        if (shader == null)
        {
            Debug.LogWarning("[CameraSystem] NIGHTMARE/SecurityCamera シェーダーが見つかりません。Inspector でアサインしてください。");
            return;
        }

        // シェーダーマテリアル & 加工先 RT を作成
        _secCamMat  = new Material(shader);
        _processedRT = new RenderTexture(rtWidth, rtHeight, 0, RenderTextureFormat.Default);
        _processedRT.name = "RT_CRT_Processed";
        ApplyShaderParams();

        // CameraViewEffect: ダイナミックグリッチ専用（ノイズは最小限）
        _glitchEffect = monitorDisplay.GetComponent<CameraViewEffect>();
        if (_glitchEffect == null)
            _glitchEffect = monitorDisplay.gameObject.AddComponent<CameraViewEffect>();
        _glitchEffect.ApplySecurityCameraPreset(0.05f); // ノイズほぼゼロ
        _glitchEffect.SetGlitchIntensity(glitchBase);

        // monitorDisplay に加工済み RT を表示（RefreshMonitorState で毎フレーム更新される）
        monitorDisplay.texture = _processedRT;

        StartCoroutine(CRTBlitLoop());

        if (_chromaPulseRoutine != null) StopCoroutine(_chromaPulseRoutine);
        _chromaPulseRoutine = StartCoroutine(MonitorCRTPulseLoop());
    }

    private void ApplyShaderParams()
    {
        if (_secCamMat == null) return;
        _secCamMat.SetFloat("_ChromaStr",     chromaStr);
        _secCamMat.SetFloat("_ScanlineAlpha", scanlineAlpha);
        _secCamMat.SetFloat("_ScanlineCount", scanlineCount);
        _secCamMat.SetFloat("_GrainStr",      grainStr);
        _secCamMat.SetFloat("_VignetteStr",   vignetteStr);
        _secCamMat.SetFloat("_Saturation",    saturation);
        _secCamMat.SetFloat("_Brightness",    brightness);
        _secCamMat.SetFloat("_WhiteTint",     whiteTint);
        _secCamMat.SetFloat("_PixelScale",    pixelScale);
    }

    // フレーム末尾にアクティブカメラの RT をシェーダー処理して _processedRT に書き込む
    private IEnumerator CRTBlitLoop()
    {
        var wait = new WaitForEndOfFrame();
        while (true)
        {
            yield return wait;
            if (_secCamMat == null || _processedRT == null) yield break;
            if (renderTextures.TryGetValue(_activeCamera, out var src) && src != null)
                Graphics.Blit(src, _processedRT, _secCamMat);
        }
    }

    private IEnumerator MonitorCRTPulseLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(
                UnityEngine.Random.Range(crtPulseIntervalMin, crtPulseIntervalMax));

            if (_secCamMat == null) yield break;

            float peakChroma = chromaStr * 4.0f;

            // ランプアップ
            float t = 0f;
            while (t < 0.10f)
            {
                t += Time.deltaTime;
                float n = t / 0.10f;
                _secCamMat.SetFloat("_ChromaStr", Mathf.Lerp(chromaStr, peakChroma, n));
                _glitchEffect?.SetGlitchIntensity(Mathf.Lerp(glitchBase, 0.45f, n));
                yield return null;
            }

            // ホールド
            float hold = UnityEngine.Random.Range(0.1f, 0.5f);
            t = 0f;
            while (t < hold) { t += Time.deltaTime; yield return null; }

            // ランプダウン
            t = 0f;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                float n = t / 0.35f;
                _secCamMat.SetFloat("_ChromaStr", Mathf.Lerp(peakChroma, chromaStr, n));
                _glitchEffect?.SetGlitchIntensity(Mathf.Lerp(0.45f, glitchBase, n));
                yield return null;
            }

            _secCamMat.SetFloat("_ChromaStr", chromaStr);
            _glitchEffect?.SetGlitchIntensity(glitchBase);
        }
    }

    private void OnDestroy()
    {
        if (_secCamMat   != null) Destroy(_secCamMat);
        if (_processedRT != null) { _processedRT.Release(); Destroy(_processedRT); }
    }

    // ===== 電子音ループ =====
    private void OnDayStarted(int day)
    {
        ResetAllCameras();
        StartBeepLoop();
    }

    private void StartBeepLoop()
    {
        if (_beepRoutine != null) StopCoroutine(_beepRoutine);
        _beepRoutine = StartCoroutine(BeepLoopRoutine());
    }

    private void StopBeepLoop()
    {
        if (_beepRoutine == null) return;
        StopCoroutine(_beepRoutine);
        _beepRoutine = null;
    }

    private IEnumerator BeepLoopRoutine()
    {
        if (beepSounds == null || beepSounds.Length == 0) yield break;

        while (true)
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(beepIntervalMin, beepIntervalMax));

            var clip = beepSounds[UnityEngine.Random.Range(0, beepSounds.Length)];
            if (clip != null)
                AudioPoolManager.Instance?.Play2D(clip, beepVolume);
        }
    }

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
