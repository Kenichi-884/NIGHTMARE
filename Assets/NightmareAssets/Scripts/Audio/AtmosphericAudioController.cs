using UnityEngine;
using System.Collections;

/// <summary>
/// プロシージャル環境音コントローラー。
///
/// 以下の環境音を動的に生成・制御する:
///   - 電気系ハム音（ピッチ揺れでリアルな電磁ノイズ感）
///   - HVAC 換気ファン（フェーズ後半で停止）
///   - 水滴音（ランダムタイミング）
///   - 構造体きしみ音（ランダムタイミング）
///   - 天候ごとの風/嵐ノイズ
///   - 停電時は HVAC 停止・ハム音変質
/// </summary>
public class AtmosphericAudioController : MonoBehaviour
{
    public static AtmosphericAudioController Instance { get; private set; }

    [Header("Ambient Clips (Inspector でアサイン)")]
    [SerializeField] private AudioClip clipHum;
    [SerializeField] private AudioClip clipHvac;
    [SerializeField] private AudioClip clipDrip;
    [SerializeField] private AudioClip clipCreak;
    [SerializeField] private AudioClip clipWind;
    [SerializeField] private AudioClip clipStorm;

    [Header("Base Volumes")]
    [SerializeField, Range(0f, 1f)] private float humVolume  = 0.18f;
    [SerializeField, Range(0f, 1f)] private float hvacVolume = 0.14f;
    [SerializeField, Range(0f, 1f)] private float windVolume = 0.28f;

    [Header("Random Event Timing")]
    [SerializeField] private Vector2 creakInterval = new Vector2(18f, 55f);
    [SerializeField] private Vector2 dripInterval  = new Vector2(6f,  24f);

    private AudioSource         _humSrc;
    private AudioSource         _hvacSrc;
    private AudioSource         _windSrc;
    private AudioLowPassFilter  _humLpf;
    private AudioHighPassFilter _hvacHpf;

    private float _humPhase;
    private float _pitchTimer;
    private const float PitchInterval = 0.05f; // 20fps — ±0.01 のピッチ変化は耳で区別不可能
    private float _creakTimer;
    private float _dripTimer;
    private int   _currentDay;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _humSrc  = CreateLoop("Hum",  clipHum,  humVolume);
        _hvacSrc = CreateLoop("HVAC", clipHvac, 0f);      // Start() でフェードイン
        _windSrc = CreateLoop("Wind", clipWind, 0f);

        // ハム音に低域通過フィルタ（こもった電磁ノイズ感）
        if (_humSrc != null)
        {
            _humLpf = _humSrc.gameObject.AddComponent<AudioLowPassFilter>();
            _humLpf.cutoffFrequency   = 2800f;
            _humLpf.lowpassResonanceQ = 0.75f;
        }

        // HVAC に高域通過フィルタ（ファン特有の質感）
        if (_hvacSrc != null)
        {
            _hvacHpf = _hvacSrc.gameObject.AddComponent<AudioHighPassFilter>();
            _hvacHpf.cutoffFrequency    = 200f;
            _hvacHpf.highpassResonanceQ = 1.0f;
        }
    }

    private void Start()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        gm.OnDayStarted   += d =>
        {
            _currentDay = d;
            StartCoroutine(FadeTo(_hvacSrc, hvacVolume, 4f));
            UpdateWeather();
        };
        gm.OnPhaseChanged += OnPhaseChanged;

        var pm = PowerManager.Instance;
        if (pm != null)
        {
            pm.OnPowerOut      += OnPowerOut;
            pm.OnPowerRestored += OnPowerRestored;
        }

        _creakTimer = Random.Range(creakInterval.x, creakInterval.y);
        _dripTimer  = Random.Range(dripInterval.x,  dripInterval.y);
    }

    private void Update()
    {
        if (GameManager.Instance?.CurrentState != GameState.Night) return;

        // 電気系ハム音の有機的ピッチ揺れ（20fpsで更新 — ±0.01の変化は耳で区別不可能）
        _humPhase += Time.deltaTime;
        _pitchTimer += Time.deltaTime;
        if (_pitchTimer >= PitchInterval && _humSrc)
        {
            _pitchTimer = 0f;
            _humSrc.pitch = 1f
                + Mathf.Sin(_humPhase * 2.17f) * 0.007f
                + Mathf.Sin(_humPhase * 7.63f) * 0.003f;
        }

        // 構造体きしみ音（ランダムタイミング）
        _creakTimer -= Time.deltaTime;
        if (_creakTimer <= 0f)
        {
            _creakTimer = Random.Range(creakInterval.x, creakInterval.y);
            PlayOneShot(clipCreak, Random.Range(0.10f, 0.35f), Random.Range(0.82f, 1.18f));
        }

        // 水滴音（B1 の環境感）
        _dripTimer -= Time.deltaTime;
        if (_dripTimer <= 0f)
        {
            _dripTimer = Random.Range(dripInterval.x, dripInterval.y);
            PlayOneShot(clipDrip, Random.Range(0.15f, 0.45f), Random.Range(0.88f, 1.12f));
        }
    }

    // ─── イベントハンドラ ──────────────────────────────────────────

    private void OnPhaseChanged(GamePhase phase)
    {
        // 電力が逼迫する後半フェーズで HVAC が止まる
        float targetHvac = phase >= GamePhase.Collapse ? 0f : hvacVolume;
        StartCoroutine(FadeTo(_hvacSrc, targetHvac, 4.5f));

        // ハム音はフェーズが深まるほど大きく・篭り気味に
        float targetHum = phase >= GamePhase.Erosion ? humVolume * 1.6f : humVolume;
        if (_humLpf) _humLpf.cutoffFrequency = phase >= GamePhase.Collapse ? 800f : 2800f;
        StartCoroutine(FadeTo(_humSrc, targetHum, 6f));
    }

    // 停電：HVAC 即停止・ハム音変質（低周波のみ残る）
    public void OnPowerOut()
    {
        StartCoroutine(FadeTo(_hvacSrc, 0f, 1.2f));
        if (_humLpf) _humLpf.cutoffFrequency = 450f;
        StartCoroutine(FadeTo(_humSrc, humVolume * 0.35f, 1.8f));
    }

    // 予備電源回復：ゆっくり復旧
    public void OnPowerRestored()
    {
        if (_humLpf) _humLpf.cutoffFrequency = 2800f;
        StartCoroutine(FadeTo(_humSrc,  humVolume, 3.5f));
        StartCoroutine(FadeTo(_hvacSrc, hvacVolume * 0.5f, 7f));
    }

    private void UpdateWeather()
    {
        if (WeatherManager.Instance == null) return;
        var w = WeatherManager.GetWeather(_currentDay);

        // 嵐なら wind → storm クリップに切替
        if (w == WeatherType.Storm && clipStorm && _windSrc)
        {
            _windSrc.Stop();
            _windSrc.clip = clipStorm;
            _windSrc.Play();
        }
        else if (_windSrc && clipWind && _windSrc.clip != clipWind)
        {
            _windSrc.Stop();
            _windSrc.clip = clipWind;
            _windSrc.Play();
        }

        float target = w switch
        {
            WeatherType.Rain  => windVolume * 0.55f,
            WeatherType.Storm => windVolume,
            _                 => 0f
        };
        StartCoroutine(FadeTo(_windSrc, target, 5f));
    }

    // ─── ユーティリティ ────────────────────────────────────────────

    private AudioSource CreateLoop(string goName, AudioClip clip, float vol)
    {
        var go = new GameObject($"Atmo_{goName}");
        go.transform.SetParent(transform);
        var src = go.AddComponent<AudioSource>();
        src.clip         = clip;
        src.loop         = true;
        src.volume       = vol;
        src.spatialBlend = 0f;
        src.playOnAwake  = false;
        if (clip != null) src.Play();
        return src;
    }

    private void PlayOneShot(AudioClip clip, float vol, float pitch)
    {
        if (!clip) return;
        if (AudioPoolManager.Instance != null)
            AudioPoolManager.Instance.Play2D(clip, vol, pitch);
    }

    private IEnumerator FadeTo(AudioSource src, float target, float dur)
    {
        if (!src) yield break;
        float start = src.volume, t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            src.volume = Mathf.Lerp(start, target, t / dur);
            yield return null;
        }
        src.volume = target;
    }
}
