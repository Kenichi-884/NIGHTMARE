using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// UI レイヤーによる疑似ポストプロセスチェーン。
///
/// シェーダー不使用で以下の AAA エフェクトをシミュレートする:
///   1. フィルムグレイン  — 事前生成フレームのサイクル（ランタイムのApply不要）
///   2. 色調補正          — フェーズごとに変化するカラーグレード Tint
///   3. 色収差            — 赤/青チャンネルを水平方向にオフセットした半透明 Image
///   4. レンズ歪み        — HUD Canvas を弾性スケールで変形
///
/// 使い方:
///   ・Screen Space - Overlay Canvas 上に空の GameObject を作り本コンポーネントを追加。
///   ・同じ Canvas の一番上（最前面）に配置すること。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class PostProcessChainUI : MonoBehaviour
{
    public static PostProcessChainUI Instance { get; private set; }

    // ── フィルムグレイン ──────────────────────────────────────────
    [Header("Film Grain")]
    [SerializeField, Range(0f, 0.25f)] private float grainIntensity    = 0.05f;
    [SerializeField, Range(0.02f, 0.2f)] private float grainUpdateRate = 0.05f;

    // ── 色調補正（フェーズごとのカラーグレード）─────────────────────
    [Header("Color Grading")]
    [SerializeField] private Color gradeCalm     = new Color(0.00f, 0.03f, 0.10f, 0.07f);
    [SerializeField] private Color gradeTense    = new Color(0.10f, 0.03f, 0.00f, 0.09f);
    [SerializeField] private Color gradeCritical = new Color(0.18f, 0.00f, 0.00f, 0.13f);
    [SerializeField, Range(0.2f, 4f)] private float gradeBlendSpeed = 1.5f;

    // ── 色収差 ────────────────────────────────────────────────────
    [Header("Chromatic Aberration")]
    [SerializeField, Range(0f, 30f)] private float chromaMaxOffset = 14f;

    // ── レンズ歪み ────────────────────────────────────────────────
    [Header("Lens Distortion")]
    [SerializeField, Range(0f, 0.12f)] private float lensDistortionMax = 0.065f;

    // ── 参照（自動生成）──────────────────────────────────────────
    private Image    _gradeOverlay;
    private Image    _chromaRed;
    private Image    _chromaBlue;
    private RawImage _grainOverlay;

    // 起動時に事前生成したグレインフレーム群（ランタイムの Apply 不要）
    private const int GRAIN_FRAME_COUNT = 12;
    private const int GRAIN_SZ = 128;
    private Texture2D[] _grainFrames;
    private int   _grainFrameIndex   = 0;
    private float _grainClock        = 0f;
    private float _bakedIntensity    = -1f;

    private Color     _currentGrade;
    private Color     _targetGrade;
    private Coroutine _distortRoutine;
    private Coroutine _chromaRoutine;

    // レンズ歪みを適用する RectTransform（HUD ルートなど）
    [Header("Lens Distortion Target")]
    [SerializeField] private RectTransform lensTarget;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        BuildColorGrade();
        BuildChromaLayers();
        BuildGrainLayer();

        _currentGrade = gradeCalm;
        _targetGrade  = gradeCalm;
    }

    private void Start()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        gm.OnPhaseChanged += p => _targetGrade = ResolveGrade(p);
        gm.OnGameOver     += OnGameOver;
        gm.OnDayStarted   += _ => { _targetGrade = gradeCalm; SetChromaticAberration(0f); };

        if (lensTarget == null)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas) lensTarget = canvas.GetComponent<RectTransform>();
        }
    }

    private void Update()
    {
        // カラーグレードを滑らかにブレンド
        _currentGrade = Color.Lerp(_currentGrade, _targetGrade, Time.deltaTime * gradeBlendSpeed);
        if (_gradeOverlay) _gradeOverlay.color = _currentGrade;

        // 事前生成グレインフレームをサイクル（Apply不要）
        _grainClock += Time.deltaTime;
        if (_grainClock >= grainUpdateRate)
        {
            _grainClock = 0f;
            _grainFrameIndex = (_grainFrameIndex + 1) % GRAIN_FRAME_COUNT;
            if (_grainOverlay) _grainOverlay.texture = _grainFrames[_grainFrameIndex];
        }
    }

    // ─── Public API ────────────────────────────────────────────────

    public void SetChromaticAberration(float t)
    {
        t = Mathf.Clamp01(t);
        float offset = t * chromaMaxOffset;
        if (_chromaRed)
        {
            _chromaRed.rectTransform.anchoredPosition = new Vector2(-offset, 0f);
            var c = _chromaRed.color; c.a = t * 0.22f; _chromaRed.color = c;
        }
        if (_chromaBlue)
        {
            _chromaBlue.rectTransform.anchoredPosition = new Vector2(offset, 0f);
            var c = _chromaBlue.color; c.a = t * 0.22f; _chromaBlue.color = c;
        }
    }

    public void FadeOutChroma(float duration = 0.5f)
    {
        if (_chromaRoutine != null) StopCoroutine(_chromaRoutine);
        _chromaRoutine = StartCoroutine(FadeChromaRoutine(duration));
    }

    public void PulseLens(float duration = 0.45f, float magnitude = -1f)
    {
        if (magnitude < 0f) magnitude = lensDistortionMax;
        if (_distortRoutine != null) StopCoroutine(_distortRoutine);
        _distortRoutine = StartCoroutine(LensRoutine(duration, magnitude));
    }

    /// <summary>グレイン強度を変更する。強度が大きく変化した場合はフレームを再ベイクする。</summary>
    public void SetGrainIntensity(float v)
    {
        v = Mathf.Clamp01(v);
        grainIntensity = v;
        if (Mathf.Abs(v - _bakedIntensity) > 0.02f)
            RebakeGrainFrames(v);
    }

    // ─── ゲームイベント ───────────────────────────────────────────

    private void OnGameOver()
    {
        _targetGrade = gradeCritical;
        SetChromaticAberration(1f);
        FadeOutChroma(1.2f);
        PulseLens(0.9f, lensDistortionMax * 0.8f);
    }

    // ─── レイヤー構築 ─────────────────────────────────────────────

    private void BuildColorGrade()
    {
        _gradeOverlay = MakeFullscreenImage("_ColorGrade", gradeCalm);
    }

    private void BuildChromaLayers()
    {
        _chromaRed  = MakeFullscreenImage("_ChromaRed",  new Color(1f, 0.06f, 0.0f, 0f));
        _chromaBlue = MakeFullscreenImage("_ChromaBlue", new Color(0.0f, 0.06f, 1.0f, 0f));
    }

    private void BuildGrainLayer()
    {
        _grainFrames = BakeGrainFrames(grainIntensity);
        _bakedIntensity = grainIntensity;

        var go = new GameObject("_FilmGrain", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(transform, false);
        StretchRT(go.GetComponent<RectTransform>());
        _grainOverlay = go.GetComponent<RawImage>();
        _grainOverlay.raycastTarget = false;
        _grainOverlay.texture = _grainFrames[0];
    }

    private void RebakeGrainFrames(float intensity)
    {
        if (_grainFrames != null)
            foreach (var t in _grainFrames) if (t) Destroy(t);

        _grainFrames     = BakeGrainFrames(intensity);
        _bakedIntensity  = intensity;
        _grainFrameIndex = 0;
        if (_grainOverlay) _grainOverlay.texture = _grainFrames[0];
    }

    private static Texture2D[] BakeGrainFrames(float intensity)
    {
        var frames   = new Texture2D[GRAIN_FRAME_COUNT];
        var pixels   = new Color32[GRAIN_SZ * GRAIN_SZ];
        byte maxAlpha = (byte)Mathf.Clamp(intensity * 80f, 3f, 80f);

        for (int f = 0; f < GRAIN_FRAME_COUNT; f++)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                if (Random.value < intensity)
                {
                    byte v = (byte)Random.Range(80, 255);
                    byte a = (byte)Random.Range(2, maxAlpha);
                    pixels[i] = new Color32(v, v, v, a);
                }
                else pixels[i] = new Color32(0, 0, 0, 0);
            }

            var tex         = new Texture2D(GRAIN_SZ, GRAIN_SZ, TextureFormat.RGBA32, false);
            tex.filterMode  = FilterMode.Point;
            tex.wrapMode    = TextureWrapMode.Repeat;
            tex.SetPixels32(pixels);
            tex.Apply(false);
            frames[f] = tex;
        }
        return frames;
    }

    private Image MakeFullscreenImage(string goName, Color col)
    {
        var go = new GameObject(goName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        StretchRT(go.GetComponent<RectTransform>());
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.color = col;
        return img;
    }

    private static void StretchRT(RectTransform rt)
    {
        rt.anchorMin = rt.offsetMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMax = Vector2.zero;
    }

    // ─── コルーチン ───────────────────────────────────────────────

    private IEnumerator LensRoutine(float dur, float mag)
    {
        if (lensTarget == null) yield break;
        Vector3 origin = lensTarget.localScale;
        float f1 = Mathf.PI / dur * 3.0f;
        float f2 = f1 * 2.37f;
        float f3 = f1 * 0.41f;
        float t  = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float decay = 1f - t / dur;
            float d = mag * decay * (Mathf.Sin(f1 * t) * 0.55f +
                                      Mathf.Sin(f2 * t) * 0.28f +
                                      Mathf.Sin(f3 * t) * 0.17f);
            lensTarget.localScale = origin * (1f + d);
            yield return null;
        }
        lensTarget.localScale = origin;
    }

    private IEnumerator FadeChromaRoutine(float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            SetChromaticAberration(1f - t / dur);
            yield return null;
        }
        SetChromaticAberration(0f);
    }

    // ─── ヘルパー ─────────────────────────────────────────────────

    private Color ResolveGrade(GamePhase phase) => phase switch
    {
        GamePhase.Silence or GamePhase.Omen                                     => gradeCalm,
        GamePhase.Contact or GamePhase.Increase or GamePhase.Erosion
            or GamePhase.Infiltration or GamePhase.Siege                         => gradeTense,
        GamePhase.Collapse or GamePhase.Abyss or GamePhase.BeforeDawn           => gradeCritical,
        _ => gradeCalm
    };

    private void OnDestroy()
    {
        if (_grainFrames != null)
            foreach (var t in _grainFrames) if (t) Destroy(t);
    }
}
