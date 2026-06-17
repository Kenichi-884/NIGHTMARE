using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class CameraViewEffect : MonoBehaviour
{
    [SerializeField] private float scanlineScrollSpeed = 0.04f;
    [SerializeField] private float noiseUpdateInterval = 0.08f;
    [SerializeField] private float baseNoiseAlpha      = 0.025f;

    // ── Layers ────────────────────────────────────────────────
    private RawImage scanlineImage;
    private RawImage noiseImage;
    private RawImage glitchBarImage;
    private RawImage vignetteImage;
    private Image    colorFringeImage;

    // ── Textures ──────────────────────────────────────────────
    private Texture2D scanlineTex;
    private Texture2D noiseTex;
    private Texture2D glitchBarTex;
    private Texture2D vignetteTex;
    private Color32[] noisePixels;
    private Color32[] glitchPixels;

    // ── State ─────────────────────────────────────────────────
    private float uvY             = 0f;
    private float noiseClock      = 0f;
    private float glitchIntensity = 0f;
    private float glitchTarget    = 0f;
    private float glitchBarTimer  = 0f;
    private float flickerClock    = 0f;
    private bool  flickerDim      = false;

    private void Awake()
    {
        BuildScanlineLayer();
        BuildNoiseLayer();
        BuildGlitchBarLayer();
        BuildVignetteLayer();
        BuildColorFringeLayer();
    }

    // ── Layer builders ────────────────────────────────────────

    private void BuildScanlineLayer()
    {
        scanlineTex = new Texture2D(4, 8, TextureFormat.RGBA32, false);
        scanlineTex.wrapMode   = TextureWrapMode.Repeat;
        scanlineTex.filterMode = FilterMode.Point;
        for (int y = 0; y < 8; y++)
        {
            Color32 c = (y % 4 < 2) ? new Color32(0, 0, 0, 45) : new Color32(0, 0, 0, 0);
            for (int x = 0; x < 4; x++) scanlineTex.SetPixel(x, y, c);
        }
        scanlineTex.Apply();
        scanlineImage = MakeRaw("_Scanlines");
        scanlineImage.texture = scanlineTex;
    }

    private void BuildNoiseLayer()
    {
        noiseTex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        noiseTex.filterMode = FilterMode.Point;
        noiseTex.wrapMode   = TextureWrapMode.Repeat;
        noisePixels = new Color32[64 * 64];
        WriteNoise(baseNoiseAlpha);
        noiseImage = MakeRaw("_Noise");
        noiseImage.texture = noiseTex;
    }

    private void BuildGlitchBarLayer()
    {
        glitchBarTex = new Texture2D(4, 256, TextureFormat.RGBA32, false);
        glitchBarTex.wrapMode   = TextureWrapMode.Clamp;
        glitchBarTex.filterMode = FilterMode.Point;
        glitchPixels = new Color32[4 * 256];
        ClearGlitchPixels();
        glitchBarImage = MakeRaw("_GlitchBars");
        glitchBarImage.texture = glitchBarTex;
    }

    private void BuildVignetteLayer()
    {
        const int sz = 64;
        vignetteTex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
        vignetteTex.filterMode = FilterMode.Bilinear;
        vignetteTex.wrapMode   = TextureWrapMode.Clamp;
        var vpix = new Color32[sz * sz];
        for (int y = 0; y < sz; y++)
        for (int x = 0; x < sz; x++)
        {
            float nx   = (x / (float)(sz - 1)) * 2f - 1f;
            float ny   = (y / (float)(sz - 1)) * 2f - 1f;
            float dist = Mathf.Sqrt(nx * nx + ny * ny);
            float a    = Mathf.Clamp01((dist - 0.55f) * 2.2f);
            vpix[y * sz + x] = new Color32(0, 0, 0, (byte)(a * 160));
        }
        vignetteTex.SetPixels32(vpix);
        vignetteTex.Apply();
        vignetteImage = MakeRaw("_Vignette");
        vignetteImage.texture = vignetteTex;
    }

    private void BuildColorFringeLayer()
    {
        var go = new GameObject("_ColorFringe", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        Stretch(go.GetComponent<RectTransform>());
        colorFringeImage = go.GetComponent<Image>();
        colorFringeImage.raycastTarget = false;
        colorFringeImage.color = new Color(1f, 0.08f, 0.08f, 0f);
    }

    private RawImage MakeRaw(string goName)
    {
        var go = new GameObject(goName, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(transform, false);
        Stretch(go.GetComponent<RectTransform>());
        var ri = go.GetComponent<RawImage>();
        ri.raycastTarget = false;
        ri.color = Color.white;
        return ri;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = rt.offsetMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMax = Vector2.zero;
    }

    // ── Update ────────────────────────────────────────────────

    private void Update()
    {
        // Scanline scroll + horizontal tear during glitch
        uvY += Time.deltaTime * scanlineScrollSpeed;
        float xTear = glitchIntensity > 0.25f
            ? Random.Range(-0.12f, 0.12f) * glitchIntensity
            : 0f;
        if (scanlineImage) scanlineImage.uvRect = new Rect(xTear, uvY, 1f, 1f);

        // Smooth glitch intensity toward target
        glitchIntensity = Mathf.Lerp(glitchIntensity, glitchTarget, Time.deltaTime * 6f);

        // Noise — refresh faster under glitch
        noiseClock += Time.deltaTime;
        float noiseInterval = Mathf.Lerp(noiseUpdateInterval, 0.022f, glitchIntensity);
        if (noiseClock >= noiseInterval)
        {
            noiseClock = 0f;
            WriteNoise(
                Mathf.Lerp(0.025f, 0.65f, glitchIntensity),
                Mathf.Lerp(baseNoiseAlpha, 0.88f, glitchIntensity)
            );
        }

        // Glitch bars — horizontal bright streaks
        glitchBarTimer += Time.deltaTime;
        if (glitchBarTimer >= Mathf.Lerp(0.4f, 0.04f, glitchIntensity))
        {
            glitchBarTimer = 0f;
            WriteGlitchBars();
        }

        // Flicker — random brightness drop
        flickerClock += Time.deltaTime;
        if (flickerClock >= Mathf.Lerp(0.18f, 0.035f, glitchIntensity))
        {
            flickerClock = 0f;
            flickerDim   = glitchIntensity > 0.12f && Random.value < glitchIntensity * 0.45f;
        }
        if (noiseImage)
            noiseImage.color = flickerDim
                ? new Color(1f, 1f, 1f, Random.Range(0.35f, 0.75f))
                : Color.white;

        // Color fringe — red flash at intense glitch
        if (colorFringeImage)
        {
            float a = glitchIntensity > 0.6f
                ? Mathf.Abs(Mathf.Sin(Time.time * 20f)) * (glitchIntensity - 0.6f) * 0.45f
                : 0f;
            var c = colorFringeImage.color; c.a = a; colorFringeImage.color = c;
        }

        // Vignette — pulses subtly with danger
        if (vignetteImage)
        {
            float pulse = 1f + glitchIntensity * 0.22f * Mathf.Sin(Time.time * 4.5f);
            vignetteImage.color = new Color(1f, 1f, 1f, pulse);
        }
    }

    // ── Texture writers ───────────────────────────────────────

    private void WriteGlitchBars()
    {
        ClearGlitchPixels();

        if (glitchIntensity >= 0.05f)
        {
            int h       = glitchBarTex.height;
            int numBars = Mathf.Max(1, (int)(glitchIntensity * 10));
            for (int b = 0; b < numBars; b++)
            {
                int  barY   = Random.Range(0, h);
                int  barH   = Random.Range(1, 5);
                byte bright = (byte)Random.Range(80, 220);
                byte alpha  = (byte)Mathf.Clamp(glitchIntensity * 200f, 30f, 200f);
                for (int dy = 0; dy < barH; dy++)
                {
                    int y = Mathf.Clamp(barY + dy, 0, h - 1);
                    for (int x = 0; x < 4; x++)
                        glitchPixels[y * 4 + x] = new Color32(bright, bright, bright, alpha);
                }
            }
        }
        glitchBarTex.SetPixels32(glitchPixels);
        glitchBarTex.Apply();
    }

    private void ClearGlitchPixels()
    {
        for (int i = 0; i < glitchPixels.Length; i++) glitchPixels[i] = new Color32(0, 0, 0, 0);
    }

    private void WriteNoise(float density, float maxAlpha = -1f)
    {
        if (maxAlpha < 0f) maxAlpha = baseNoiseAlpha;
        byte maxA = (byte)(maxAlpha * 255f);
        for (int i = 0; i < noisePixels.Length; i++)
        {
            if (Random.value < density)
            {
                byte v = (byte)Random.Range(140, 255);
                noisePixels[i] = new Color32(v, v, v, (byte)Random.Range(55, maxA));
            }
            else noisePixels[i] = new Color32(0, 0, 0, 0);
        }
        noiseTex.SetPixels32(noisePixels);
        noiseTex.Apply();
    }

    // ── Public API ────────────────────────────────────────────

    // 0 = 通常, 1 = フルグリッチ (カメラ死亡・ジャンプスケア直前)
    public void SetGlitchIntensity(float t) => glitchTarget = Mathf.Clamp01(t);
}
