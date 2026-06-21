using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// UI レイヤーによる疑似ポストプロセスチェーン。
///
/// シェーダー不使用で以下の AAA エフェクトをシミュレートする:
///   1. フィルムグレイン  — 高頻度ノイズテクスチャ更新
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
    private Texture2D _grainTex;
    private Color32[] _grainPixels;

    private Color     _currentGrade;
    private Color     _targetGrade;
    private float     _grainClock;
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

        // lensTarget が未アサインなら親 Canvas を探す
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

        // フィルムグレイン更新
        _grainClock += Time.deltaTime;
        if (_grainClock >= grainUpdateRate)
        {
            _grainClock = 0f;
            RefreshGrain();
        }
    }

    // ─── Public API ────────────────────────────────────────────────

    /// <summary>
    /// 色収差強度を設定する（0=なし, 1=最大）。
    /// ジャンプスケア・グリッチ演出時に呼ぶ。
    /// </summary>
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

    /// <summary>
    /// 色収差を duration 秒かけてフェードアウト。
    /// SetChromaticAberration(1f) を呼んだあとに使う。
    /// </summary>
    public void FadeOutChroma(float duration = 0.5f)
    {
        if (_chromaRoutine != null) StopCoroutine(_chromaRoutine);
        _chromaRoutine = StartCoroutine(FadeChromaRoutine(duration));
    }

    /// <summary>
    /// レンズ歪みパルスを発生させる（衝撃・ジャンプスケア・ドア強打）。
    /// </summary>
    public void PulseLens(float duration = 0.45f, float magnitude = -1f)
    {
        if (magnitude < 0f) magnitude = lensDistortionMax;
        if (_distortRoutine != null) StopCoroutine(_distortRoutine);
        _distortRoutine = StartCoroutine(LensRoutine(duration, magnitude));
    }

    /// <summary>フィルムグレイン強度を変更する（停電・ジャンプスケア後など）。</summary>
    public void SetGrainIntensity(float v) => grainIntensity = Mathf.Clamp01(v);

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
        // 赤チャンネル（左にオフセット）
        _chromaRed  = MakeFullscreenImage("_ChromaRed",  new Color(1f, 0.06f, 0.0f, 0f));
        // 青チャンネル（右にオフセット）
        _chromaBlue = MakeFullscreenImage("_ChromaBlue", new Color(0.0f, 0.06f, 1.0f, 0f));
    }

    private void BuildGrainLayer()
    {
        const int SZ = 128;
        _grainTex = new Texture2D(SZ, SZ, TextureFormat.RGBA32, false);
        _grainTex.filterMode = FilterMode.Point;
        _grainTex.wrapMode   = TextureWrapMode.Repeat;
        _grainPixels = new Color32[SZ * SZ];

        var go = new GameObject("_FilmGrain", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(transform, false);
        StretchRT(go.GetComponent<RectTransform>());
        _grainOverlay = go.GetComponent<RawImage>();
        _grainOverlay.raycastTarget = false;
        _grainOverlay.texture = _grainTex;
        RefreshGrain();
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
            float n = 1f - t / dur;
            SetChromaticAberration(n);
            yield return null;
        }
        SetChromaticAberration(0f);
    }

    // ─── テクスチャ書き換え ───────────────────────────────────────

    private void RefreshGrain()
    {
        float intensity = grainIntensity;
        byte maxAlpha   = (byte)Mathf.Clamp(intensity * 80f, 3f, 80f);
        for (int i = 0; i < _grainPixels.Length; i++)
        {
            if (Random.value < intensity)
            {
                byte v = (byte)Random.Range(80, 255);
                byte a = (byte)Random.Range(2, maxAlpha);
                _grainPixels[i] = new Color32(v, v, v, a);
            }
            else _grainPixels[i] = new Color32(0, 0, 0, 0);
        }
        _grainTex.SetPixels32(_grainPixels);
        _grainTex.Apply();
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
}
