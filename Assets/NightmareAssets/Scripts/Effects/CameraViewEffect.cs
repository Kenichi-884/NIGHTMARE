using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 監視カメラ映像に重ねる CRT モニター風エフェクト。
///
/// ■ 追加・強化された要素
///   ・ホスファ残像（前フレームのノイズを 25〜30% 持続させ、CRT の余光を再現）
///   ・RGB カラーノイズ（白一色ではなくR/G/B 微差つきの色付きドット）
///   ・水平同期エラー（ランダムなタイミングで走査線が横ずれ）
///   ・インターレース（偶数/奇数フレームで輝度をシフトし、映像のちらつきを再現）
///   ・色収差（青赤 UV オフセット。グリッチ時に強調）
///   ・ビネット（コーナー強化・CRT 湾曲感）
///   ・カラーバーのグリッチ（R/G/B 個別に色づけされた横線ノイズ）
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class CameraViewEffect : MonoBehaviour
{
    [Header("CRT Parameters")]
    [SerializeField] private float scanlineScrollSpeed  = 0.04f;
    [SerializeField] private float noiseUpdateInterval  = 0.08f;
    [SerializeField] private float baseNoiseAlpha       = 0.025f;
    [SerializeField, Range(0f, 0.6f)] private float phosphorPersistence = 0.28f;

    [Header("Security Camera Mode")]
    [SerializeField] private bool  securityCameraMode = false;
    [SerializeField, Range(0f, 1f)] private float noiseMultiplier = 1.0f;  // ノイズ量を下げる
    [SerializeField] private Color securityNoiseColor  = new Color(0.35f, 1.0f, 0.35f, 1f);  // 緑系ノイズ

    // ── UI レイヤー ───────────────────────────────────────────────
    private RawImage scanlineImage;
    private RawImage noiseImage;
    private RawImage glitchBarImage;
    private RawImage vignetteImage;
    private RawImage rgbShiftImage;
    private Image    colorFringeImage;

    // ── テクスチャ ────────────────────────────────────────────────
    private Texture2D scanlineTex;
    private Texture2D noiseTex;
    private Texture2D glitchBarTex;
    private Texture2D vignetteTex;
    private Texture2D rgbShiftTex;

    private Color32[] noisePixels;
    private Color32[] noisePrev;         // ホスファ残像バッファ
    private Color32[] glitchPixels;
    private bool      _glitchBarCleared = true; // テクスチャが既にクリア済みかどうか

    // ── 状態 ─────────────────────────────────────────────────────
    private float uvY             = 0f;
    private float noiseClock      = 0f;
    private float glitchIntensity = 0f;
    private float glitchTarget    = 0f;
    private float glitchBarTimer  = 0f;
    private float flickerClock    = 0f;
    private bool  flickerDim      = false;

    // 水平同期エラー
    private float syncTimer       = 0f;
    private float syncOffset      = 0f;

    // インターレース
    private int   interlaceFrame  = 0;

    // RGB シフト
    private float rgbPhase        = 0f;

    private void Awake()
    {
        BuildScanlineLayer();
        BuildNoiseLayer();
        BuildGlitchBarLayer();
        BuildVignetteLayer();
        BuildRgbShiftLayer();
        BuildColorFringeLayer();
    }

    // ── レイヤー構築 ─────────────────────────────────────────────

    private void BuildScanlineLayer()
    {
        scanlineTex = new Texture2D(4, 8, TextureFormat.RGBA32, false);
        scanlineTex.wrapMode   = TextureWrapMode.Repeat;
        scanlineTex.filterMode = FilterMode.Point;
        for (int y = 0; y < 8; y++)
        {
            // 上半分: 明るめ（ホスファ発光感、僅かに暖色）
            // 下半分: 暗め（走査線の影）
            Color32 c = y < 4
                ? new Color32(255, 252, 245, 0)
                : new Color32(0, 0, 0, 48);
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
        noisePrev   = new Color32[64 * 64];
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
        const int SZ = 64;
        vignetteTex = new Texture2D(SZ, SZ, TextureFormat.RGBA32, false);
        vignetteTex.filterMode = FilterMode.Bilinear;
        vignetteTex.wrapMode   = TextureWrapMode.Clamp;
        var vp = new Color32[SZ * SZ];
        for (int y = 0; y < SZ; y++)
        for (int x = 0; x < SZ; x++)
        {
            float nx = (x / (float)(SZ - 1)) * 2f - 1f;
            float ny = (y / (float)(SZ - 1)) * 2f - 1f;
            float d  = Mathf.Sqrt(nx * nx + ny * ny);
            // 端ほど強い（CRT 湾曲感）
            float a = Mathf.Clamp01((d - 0.52f) * 2.4f);
            vp[y * SZ + x] = new Color32(0, 0, 0, (byte)(a * 175));
        }
        vignetteTex.SetPixels32(vp);
        vignetteTex.Apply();
        vignetteImage = MakeRaw("_Vignette");
        vignetteImage.texture = vignetteTex;
    }

    private void BuildRgbShiftLayer()
    {
        // 1x1 の赤テクスチャ（UV オフセットで色収差を表現）
        rgbShiftTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        rgbShiftTex.wrapMode   = TextureWrapMode.Clamp;
        rgbShiftTex.filterMode = FilterMode.Bilinear;
        for (int i = 0; i < 4; i++) rgbShiftTex.SetPixel(i % 2, i / 2, new Color32(255, 0, 0, 0));
        rgbShiftTex.Apply();
        rgbShiftImage = MakeRaw("_RgbShift");
        rgbShiftImage.texture = rgbShiftTex;
        rgbShiftImage.color   = new Color(1f, 0.05f, 0.0f, 0f);
    }

    private void BuildColorFringeLayer()
    {
        var go = new GameObject("_ColorFringe", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        StretchRT(go.GetComponent<RectTransform>());
        colorFringeImage = go.GetComponent<Image>();
        colorFringeImage.raycastTarget = false;
        colorFringeImage.color = new Color(0.9f, 0.05f, 0.05f, 0f);
    }

    private RawImage MakeRaw(string goName)
    {
        var go = new GameObject(goName, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(transform, false);
        StretchRT(go.GetComponent<RectTransform>());
        var ri = go.GetComponent<RawImage>();
        ri.raycastTarget = false;
        ri.color = Color.white;
        return ri;
    }

    private static void StretchRT(RectTransform rt)
    {
        rt.anchorMin = rt.offsetMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMax = Vector2.zero;
    }

    // ── Update ────────────────────────────────────────────────────

    private void Update()
    {
        float dt = Time.deltaTime;

        // ── グリッチ強度の平滑補間 ───────────────────────────────
        glitchIntensity = Mathf.Lerp(glitchIntensity, glitchTarget, dt * 6f);

        // ── 水平同期エラー ────────────────────────────────────────
        syncTimer -= dt;
        if (syncTimer <= 0f)
        {
            // 強グリッチ時ほど頻繁・大きくずれる
            syncTimer  = Mathf.Lerp(9f, 0.7f, glitchIntensity) + Random.Range(-0.4f, 0.4f);
            syncOffset = glitchIntensity > 0.08f
                ? Random.Range(-0.40f, 0.40f) * glitchIntensity
                : 0f;
        }
        syncOffset = Mathf.Lerp(syncOffset, 0f, dt * 14f); // 素早く中央に戻る

        // ── スキャンライン スクロール + インターレース ────────────
        uvY += dt * scanlineScrollSpeed;
        interlaceFrame = Time.frameCount % 2;
        float interlaceShift = interlaceFrame == 0 ? 0f : 0.5f / 8f;

        float xTear = syncOffset
            + (glitchIntensity > 0.15f
               ? Random.Range(-0.05f, 0.05f) * glitchIntensity
               : 0f);
        if (scanlineImage)
            scanlineImage.uvRect = new Rect(xTear, uvY + interlaceShift, 1f, 1f);

        // ── ノイズ更新（ホスファ残像つき）────────────────────────
        noiseClock += dt;
        float interval = Mathf.Lerp(noiseUpdateInterval, 0.020f, glitchIntensity);
        if (noiseClock >= interval)
        {
            noiseClock = 0f;
            WriteNoise(
                Mathf.Lerp(0.025f, 0.72f, glitchIntensity) * noiseMultiplier,
                Mathf.Lerp(baseNoiseAlpha, 0.92f, glitchIntensity) * noiseMultiplier
            );
        }

        // ── グリッチバー ─────────────────────────────────────────
        glitchBarTimer += dt;
        if (glitchBarTimer >= Mathf.Lerp(0.40f, 0.032f, glitchIntensity))
        {
            glitchBarTimer = 0f;
            WriteGlitchBars();
        }

        // ── フリッカー（輝度落ち）────────────────────────────────
        flickerClock += dt;
        if (flickerClock >= Mathf.Lerp(0.18f, 0.025f, glitchIntensity))
        {
            flickerClock = 0f;
            flickerDim = glitchIntensity > 0.08f && Random.value < glitchIntensity * 0.55f;
        }
        if (noiseImage)
        {
            if (securityCameraMode)
            {
                // 監視カメラ: 緑系ノイズ、フリッカーなし
                noiseImage.color = securityNoiseColor;
            }
            else
            {
                // CRT: 暖色ホスファグロー
                noiseImage.color = flickerDim
                    ? new Color(1f, 0.94f, 0.85f, Random.Range(0.25f, 0.80f))
                    : new Color(1f, 0.96f, 0.90f, 1f);
            }
        }

        // ── RGB シフト（色収差）──────────────────────────────────
        rgbPhase += dt * (1f + glitchIntensity * 6f);
        if (rgbShiftImage)
        {
            float aberr = glitchIntensity * 0.05f + Mathf.Sin(rgbPhase * 0.6f) * 0.003f;
            var c = rgbShiftImage.color; c.a = Mathf.Clamp01(aberr * 7f); rgbShiftImage.color = c;
            rgbShiftImage.uvRect = new Rect(aberr, 0f, 1f, 1f);
        }

        // ── カラーフリンジ ────────────────────────────────────────
        if (colorFringeImage)
        {
            float a = glitchIntensity > 0.50f
                ? Mathf.Abs(Mathf.Sin(Time.time * 24f)) * (glitchIntensity - 0.50f) * 0.55f
                : 0f;
            // 監視カメラモードは緑フリンジ、通常は赤フリンジ
            Color fringeCol = securityCameraMode
                ? new Color(0.1f, 0.9f, 0.1f, a)
                : new Color(0.9f, 0.05f, 0.05f, a);
            colorFringeImage.color = fringeCol;
        }

        // ── ビネット パルス ───────────────────────────────────────
        if (vignetteImage)
        {
            float pulse = 1f + glitchIntensity * 0.20f * Mathf.Sin(Time.time * 3.8f);
            vignetteImage.color = new Color(1f, 1f, 1f, pulse);
        }
    }

    // ── テクスチャライター ────────────────────────────────────────

    private void WriteGlitchBars()
    {
        if (glitchIntensity < 0.04f)
        {
            // テクスチャが既に全透明なら再アップロード不要
            if (_glitchBarCleared) return;
            ClearGlitchPixels();
            glitchBarTex.SetPixels32(glitchPixels);
            glitchBarTex.Apply(false);
            _glitchBarCleared = true;
            return;
        }

        _glitchBarCleared = false;
        ClearGlitchPixels();
        int h       = glitchBarTex.height;
        int numBars = Mathf.Max(1, (int)(glitchIntensity * 14));
        for (int b = 0; b < numBars; b++)
        {
            int  barY  = Random.Range(0, h);
            int  barH  = Random.Range(1, 7);
            // R/G/B それぞれ独立した値でカラーグリッチ感を出す
            byte r    = (byte)Random.Range(60, 240);
            byte g    = (byte)Random.Range(60, 240);
            byte bv   = (byte)Random.Range(60, 240);
            byte alpha = (byte)Mathf.Clamp(glitchIntensity * 230f, 20f, 215f);
            for (int dy = 0; dy < barH; dy++)
            {
                int y = Mathf.Clamp(barY + dy, 0, h - 1);
                for (int x = 0; x < 4; x++)
                    glitchPixels[y * 4 + x] = new Color32(r, g, bv, alpha);
            }
        }
        glitchBarTex.SetPixels32(glitchPixels);
        glitchBarTex.Apply(false);
    }

    private void ClearGlitchPixels()
    {
        for (int i = 0; i < glitchPixels.Length; i++) glitchPixels[i] = new Color32(0, 0, 0, 0);
    }

    /// <summary>
    /// ホスファ残像付きノイズ書き換え。
    /// 前フレームのピクセルを phosphorPersistence の割合で残すことで
    /// CRT 特有の「余光」を再現する。
    /// </summary>
    private void WriteNoise(float density, float maxAlpha = -1f)
    {
        if (maxAlpha < 0f) maxAlpha = baseNoiseAlpha;
        byte maxA = (byte)(maxAlpha * 255f);

        for (int i = 0; i < noisePixels.Length; i++)
        {
            Color32 prev = noisePrev[i];

            if (Random.value < density)
            {
                // RGB 微差つきのカラーノイズ（ホスファドット感）
                byte v  = (byte)Random.Range(130, 255);
                byte r  = (byte)Mathf.Clamp(v + Random.Range(-18, 18), 0, 255);
                byte g  = (byte)Mathf.Clamp(v + Random.Range(-18, 18), 0, 255);
                byte bv = (byte)Mathf.Clamp(v + Random.Range(-18, 18), 0, 255);
                byte a  = (byte)Random.Range(40, maxA);

                // 前フレームとブレンド（ホスファ残像）
                byte pr = (byte)Mathf.Lerp(r,  prev.r, phosphorPersistence);
                byte pg = (byte)Mathf.Lerp(g,  prev.g, phosphorPersistence);
                byte pb = (byte)Mathf.Lerp(bv, prev.b, phosphorPersistence);
                byte pa = (byte)Mathf.Clamp(a + prev.a * phosphorPersistence * 0.5f, 0, 255);

                noisePixels[i] = new Color32(pr, pg, pb, pa);
            }
            else
            {
                // ノイズがない画素も前フレームを余光として薄く残す
                byte fa = (byte)(prev.a * phosphorPersistence * 0.65f);
                noisePixels[i] = fa > 4
                    ? new Color32(prev.r, prev.g, prev.b, fa)
                    : new Color32(0, 0, 0, 0);
            }

            noisePrev[i] = noisePixels[i];
        }

        noiseTex.SetPixels32(noisePixels);
        noiseTex.Apply(false);
    }

    // ── Public API ─────────────────────────────────────────────────

    /// <summary>0 = 通常, 1 = フルグリッチ（カメラ死亡 / ジャンプスケア直前）。</summary>
    public void SetGlitchIntensity(float t) => glitchTarget = Mathf.Clamp01(t);

    /// <summary>
    /// 監視カメラプリセットを適用する。
    /// noiseScale: ノイズ量の倍率（0.1 = 通常の10%）
    /// </summary>
    public void ApplySecurityCameraPreset(float noiseScale = 0.12f)
    {
        securityCameraMode = true;
        noiseMultiplier    = noiseScale;
        // シェーダー側でビネットを処理するのでCPU生成ビネットは無効化
        if (vignetteImage != null) vignetteImage.gameObject.SetActive(false);
    }
}
