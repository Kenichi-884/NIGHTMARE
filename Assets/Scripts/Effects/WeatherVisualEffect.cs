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

    private Texture2D _rainTex;
    private Color32[] _rainPx;

    private const int W = 64;
    private const int H = 256;

    // ─── 状態 ────────────────────────────────────────────────────
    private WeatherType _weather      = WeatherType.Sunny;
    private float       _scrollY      = 0f;
    private float       _scrollX      = 0f;
    private float       _rebuildTimer = 0f;
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
        _scrollX += Time.deltaTime * speed * 0.12f; // 微妙な斜め（右方向）
        if (_scrollY > 1f) _scrollY -= 1f;
        if (_scrollX > 1f) _scrollX -= 1f;

        if (_rainLayer)
        {
            // 内部カメラ表示中は雨を薄くする（屋内なので）
            bool isExt = IsExternalCameraActive();
            float alpha = isExt ? 1f : 0.15f;
            _rainLayer.color   = new Color(1f, 1f, 1f, alpha);
            _rainLayer.uvRect  = new Rect(_scrollX, _scrollY, 1.35f, 1.35f);
        }

        // ─ 雨テクスチャを定期再生成 ─
        _rebuildTimer += Time.deltaTime;
        float rebuildRate = _weather == WeatherType.Storm ? 0.045f : 0.080f;
        if (_rebuildTimer >= rebuildRate)
        {
            _rebuildTimer = 0f;
            RebuildRainTexture();
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

        RebuildRainTexture();
        _scrollY = Random.value;
        _scrollX = Random.value;
    }

    private void SetActive(bool on)
    {
        _active = on;
        if (_rainLayer)  _rainLayer.gameObject.SetActive(on);
        if (_tintLayer)  _tintLayer.gameObject.SetActive(on);
        // 雷レイヤーは常に存在（フラッシュ時のみ表示）
    }

    // ─── 雨テクスチャ生成 ─────────────────────────────────────────

    private void RebuildRainTexture()
    {
        for (int i = 0; i < _rainPx.Length; i++) _rainPx[i] = new Color32(0, 0, 0, 0);

        float density = _weather == WeatherType.Storm ? stormDensity : rainDensity;
        int streaks   = Mathf.RoundToInt(density * W * H * 0.6f);

        for (int s = 0; s < streaks; s++)
        {
            int x   = Random.Range(0, W);
            int y   = Random.Range(0, H);
            int len = Random.Range(4, _weather == WeatherType.Storm ? 24 : 14);

            // 青みがかった白（雨粒の色）
            byte r   = (byte)Random.Range(155, 210);
            byte g   = (byte)Random.Range(170, 225);
            byte b   = (byte)Random.Range(210, 255);
            byte alf = (byte)Random.Range(45, 140);

            for (int i = 0; i < len; i++)
            {
                int py = y - i;
                int px = x + Mathf.RoundToInt(i * 0.28f);
                if (py < 0 || py >= H || px < 0 || px >= W) continue;
                byte fade = (byte)(alf * (1f - (float)i / len));
                _rainPx[py * W + px] = new Color32(r, g, b, fade);
            }
        }
        _rainTex.SetPixels32(_rainPx);
        _rainTex.Apply();
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

        // 30% 確率でダブルフラッシュ
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
        _rainTex            = new Texture2D(W, H, TextureFormat.RGBA32, false);
        _rainTex.wrapMode   = TextureWrapMode.Repeat;
        _rainTex.filterMode = FilterMode.Bilinear;
        _rainPx             = new Color32[W * H];

        var go = new GameObject("_RainStreaks", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(transform, false);
        Stretch(go.GetComponent<RectTransform>());
        _rainLayer              = go.GetComponent<RawImage>();
        _rainLayer.raycastTarget = false;
        _rainLayer.texture      = _rainTex;
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
}
