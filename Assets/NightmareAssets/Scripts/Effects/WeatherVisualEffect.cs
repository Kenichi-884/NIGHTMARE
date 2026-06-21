using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 天気ごとの視覚エフェクトを Canvas 上に重ねる。
/// Screen Space Overlay Canvas の子 GameObject に追加すること。
///
/// 雨  : 雨粒ストリーク + 青みトント (外部カメラのみ強表示)
/// 嵐  : 激しいストリーク + 強トント + 稲妻フラッシュ + PulseLens
/// 晴れ: 何も表示しない
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class WeatherVisualEffect : MonoBehaviour
{
    public static WeatherVisualEffect Instance { get; private set; }

    [Header("Rain")]
    [SerializeField] private Color rainTint          = new Color(0.05f, 0.10f, 0.25f, 0.07f);
    [SerializeField, Range(0f, 1f)] private float rainDensity  = 0.022f;
    [SerializeField] private float rainScrollSpeed   = 0.32f;   // UV/sec

    [Header("Storm")]
    [SerializeField] private Color stormTint         = new Color(0.02f, 0.06f, 0.20f, 0.14f);
    [SerializeField, Range(0f, 1f)] private float stormDensity = 0.065f;
    [SerializeField] private float stormScrollSpeed  = 0.72f;
    [SerializeField] private Vector2 thunderInterval = new Vector2(8f, 26f);

    // ─── UI レイヤー ─────────────────────────────────────────────
    private RawImage  _rainLayer;
    private Image     _tintLayer;
    private Image     _thunderLayer;

    // 起動時に事前生成した雨フレーム群（ゲームプレイ中はApply不要）
    private const int FRAME_COUNT = 16;
    private const int W = 64;
    private const int H = 256;
    private Texture2D[] _rainFrames;
    private Texture2D[] _stormFrames;
    private int   _frameIndex   = 0;
    private float _frameTimer   = 0f;

    // ─── 状態 ────────────────────────────────────────────────────
    private WeatherType _weather      = WeatherType.Sunny;
    private float       _scrollY      = 0f;
    private float       _scrollX      = 0f;
    private float       _thunderTimer = 0f;
    private bool        _active       = false;

    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        BuildRainLayer();
        BuildTintLayer();
        BuildThunderLayer();
        SetActive(false);
    }

    private void Start()
    {
        if (WeatherManager.Instance != null)
            WeatherManager.Instance.OnWeatherChanged += Apply;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnDayStarted   += d  => Apply(WeatherManager.GetWeather(d));
            GameManager.Instance.OnNightCleared += _  => SetActive(false);
            GameManager.Instance.OnGameOver     += () => SetActive(false);
        }

        _thunderTimer = Random.Range(thunderInterval.x, thunderInterval.y);
    }

    private void Update()
    {
        if (!_active || _weather == WeatherType.Sunny) return;
        if (GameManager.Instance?.CurrentState != GameState.Night) return;

        // ─ 雨ストリーク スクロール ─
        float speed = _weather == WeatherType.Storm ? stormScrollSpeed : rainScrollSpeed;
        _scrollY += Time.deltaTime * speed;
        _scrollX += Time.deltaTime * speed * 0.12f;
        if (_scrollY > 1f) _scrollY -= 1f;
        if (_scrollX > 1f) _scrollX -= 1f;

        if (_rainLayer)
        {
            bool isExt = IsExternalCameraActive();
            float alpha = isExt ? 1f : 0.15f;
            _rainLayer.color  = new Color(1f, 1f, 1f, alpha);
            _rainLayer.uvRect = new Rect(_scrollX, _scrollY, 1.35f, 1.35f);
        }

        // ─ 事前生成フレームをサイクル（Apply不要） ─
        float frameRate = _weather == WeatherType.Storm ? 0.045f : 0.080f;
        _frameTimer += Time.deltaTime;
        if (_frameTimer >= frameRate)
        {
            _frameTimer = 0f;
            _frameIndex = (_frameIndex + 1) % FRAME_COUNT;
            if (_rainLayer != null)
                _rainLayer.texture = _weather == WeatherType.Storm
                    ? _stormFrames[_frameIndex]
                    : _rainFrames[_frameIndex];
        }

        // ─ 嵐：稲妻フラッシュ ─
        if (_weather == WeatherType.Storm)
        {
            _thunderTimer -= Time.deltaTime;
            if (_thunderTimer <= 0f)
            {
                _thunderTimer = Random.Range(thunderInterval.x, thunderInterval.y);
                StartCoroutine(ThunderFlash());
            }
        }
    }

    // ─── 天気適用 ─────────────────────────────────────────────────

    private void Apply(WeatherType w)
    {
        _weather = w;
        bool rainy = w != WeatherType.Sunny;
        SetActive(rainy);
        if (!rainy) return;

        Color tint = w == WeatherType.Storm ? stormTint : rainTint;
        if (_tintLayer) _tintLayer.color = tint;

        _frameIndex = 0;
        _frameTimer = 0f;
        if (_rainLayer != null)
            _rainLayer.texture = w == WeatherType.Storm ? _stormFrames[0] : _rainFrames[0];

        _scrollY = Random.value;
        _scrollX = Random.value;
    }

    private void SetActive(bool on)
    {
        _active = on;
        if (_rainLayer)  _rainLayer.gameObject.SetActive(on);
        if (_tintLayer)  _tintLayer.gameObject.SetActive(on);
    }

    // ─── 稲妻フラッシュ ──────────────────────────────────────────

    private IEnumerator ThunderFlash()
    {
        if (_thunderLayer == null) yield break;

        float peak = Random.Range(0.10f, 0.30f);
        _thunderLayer.color = new Color(1f, 1f, 1f, peak);

        PostProcessChainUI.Instance?.PulseLens(0.28f, 0.038f);
        AudioManager.Instance?.Play("camera_static");

        yield return new WaitForSeconds(0.06f);
        _thunderLayer.color = new Color(1f, 1f, 1f, 0f);

        if (Random.value < 0.30f)
        {
            yield return new WaitForSeconds(Random.Range(0.07f, 0.20f));
            _thunderLayer.color = new Color(1f, 1f, 1f, peak * 0.50f);
            yield return new WaitForSeconds(0.04f);
            _thunderLayer.color = new Color(1f, 1f, 1f, 0f);
        }
    }

    // ─── ユーティリティ ───────────────────────────────────────────

    private static bool IsExternalCameraActive()
    {
        if (SecurityCameraSystem.Instance == null) return true;
        var cfg = SecurityCameraSystem.Instance.GetConfig(
                      SecurityCameraSystem.Instance.ActiveCamera);
        return cfg == null || cfg.isExternal;
    }

    // ─── UIレイヤー構築 ───────────────────────────────────────────

    private void BuildRainLayer()
    {
        // 雨・嵐それぞれのフレームを起動時に一括生成（ゲームプレイ中はApply不要）
        _rainFrames  = BakeFrames(rainDensity,  maxStreakLen: 14);
        _stormFrames = BakeFrames(stormDensity, maxStreakLen: 24);

        var go = new GameObject("_RainStreaks", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(transform, false);
        Stretch(go.GetComponent<RectTransform>());
        _rainLayer               = go.GetComponent<RawImage>();
        _rainLayer.raycastTarget = false;
        _rainLayer.texture       = _rainFrames[0];
    }

    private Texture2D[] BakeFrames(float density, int maxStreakLen)
    {
        var frames = new Texture2D[FRAME_COUNT];
        var buf    = new Color32[W * H];
        int streaks = Mathf.RoundToInt(density * W * H * 0.6f);

        for (int f = 0; f < FRAME_COUNT; f++)
        {
            for (int i = 0; i < buf.Length; i++) buf[i] = new Color32(0, 0, 0, 0);

            for (int s = 0; s < streaks; s++)
            {
                int sx  = Random.Range(0, W);
                int sy  = Random.Range(0, H);
                int len = Random.Range(4, maxStreakLen);
                byte r   = (byte)Random.Range(155, 210);
                byte g   = (byte)Random.Range(170, 225);
                byte b   = (byte)Random.Range(210, 255);
                byte alf = (byte)Random.Range(45, 140);

                for (int k = 0; k < len; k++)
                {
                    int py = sy - k;
                    int px = sx + Mathf.RoundToInt(k * 0.28f);
                    if (py < 0 || py >= H || px < 0 || px >= W) continue;
                    byte fade = (byte)(alf * (1f - (float)k / len));
                    buf[py * W + px] = new Color32(r, g, b, fade);
                }
            }

            var tex         = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.wrapMode    = TextureWrapMode.Repeat;
            tex.filterMode  = FilterMode.Bilinear;
            tex.SetPixels32(buf);
            tex.Apply(false);
            frames[f] = tex;
        }
        return frames;
    }

    private void BuildTintLayer()
    {
        var go = new GameObject("_WeatherTint", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        Stretch(go.GetComponent<RectTransform>());
        _tintLayer               = go.GetComponent<Image>();
        _tintLayer.raycastTarget = false;
        _tintLayer.color         = Color.clear;
    }

    private void BuildThunderLayer()
    {
        var go = new GameObject("_Thunder", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        Stretch(go.GetComponent<RectTransform>());
        _thunderLayer               = go.GetComponent<Image>();
        _thunderLayer.raycastTarget = false;
        _thunderLayer.color         = Color.clear;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = rt.offsetMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMax = Vector2.zero;
    }

    private void OnDestroy()
    {
        if (_rainFrames  != null) foreach (var t in _rainFrames)  if (t) Destroy(t);
        if (_stormFrames != null) foreach (var t in _stormFrames) if (t) Destroy(t);
    }
}
