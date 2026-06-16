using UnityEngine;
using UnityEngine.UI;

// 監視カメラモニターにスキャンラインとノイズエフェクトを追加する
// ExtBG / IntBG の GameObject にアタッチし、子として各エフェクトレイヤーを生成する
[RequireComponent(typeof(RectTransform))]
public class CameraViewEffect : MonoBehaviour
{
    [SerializeField] private float scanlineScrollSpeed = 0.04f;
    [SerializeField] private float noiseUpdateInterval = 0.08f;
    [SerializeField] private float baseNoiseAlpha = 0.025f;

    private RawImage scanlineImage;
    private RawImage noiseImage;
    private Texture2D scanlineTex;
    private Texture2D noiseTex;
    private Color32[] noisePixels;

    private float uvY = 0f;
    private float noiseClock = 0f;
    private float glitchIntensity = 0f;
    private float glitchTarget = 0f;

    private void Awake()
    {
        BuildScanlineLayer();
        BuildNoiseLayer();
    }

    private void BuildScanlineLayer()
    {
        scanlineTex = new Texture2D(4, 8, TextureFormat.RGBA32, false);
        scanlineTex.wrapMode = TextureWrapMode.Repeat;
        scanlineTex.filterMode = FilterMode.Point;
        for (int y = 0; y < 8; y++)
        {
            Color32 c = (y % 4 < 2)
                ? new Color32(0, 0, 0, 45)
                : new Color32(0, 0, 0, 0);
            for (int x = 0; x < 4; x++) scanlineTex.SetPixel(x, y, c);
        }
        scanlineTex.Apply();

        scanlineImage = MakeOverlay("_Scanlines");
        scanlineImage.texture = scanlineTex;
        scanlineImage.color = Color.white;
    }

    private void BuildNoiseLayer()
    {
        noiseTex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        noiseTex.filterMode = FilterMode.Point;
        noiseTex.wrapMode = TextureWrapMode.Repeat;
        noisePixels = new Color32[64 * 64];
        WriteNoise(baseNoiseAlpha);

        noiseImage = MakeOverlay("_Noise");
        noiseImage.texture = noiseTex;
        noiseImage.color = Color.white;
    }

    private RawImage MakeOverlay(string goName)
    {
        var go = new GameObject(goName, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var ri = go.GetComponent<RawImage>();
        ri.raycastTarget = false;
        return ri;
    }

    private void Update()
    {
        // スキャンラインスクロール
        uvY += Time.deltaTime * scanlineScrollSpeed;
        if (scanlineImage) scanlineImage.uvRect = new Rect(0, uvY, 1, 1);

        // グリッチ強度スムージング
        glitchIntensity = Mathf.Lerp(glitchIntensity, glitchTarget, Time.deltaTime * 6f);

        // ノイズ更新
        noiseClock += Time.deltaTime;
        if (noiseClock >= noiseUpdateInterval)
        {
            noiseClock = 0f;
            float density = Mathf.Lerp(0.025f, 0.6f, glitchIntensity);
            float maxA    = Mathf.Lerp(baseNoiseAlpha, 0.85f, glitchIntensity);
            WriteNoise(density, maxA);
        }
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
                noisePixels[i] = new Color32(v, v, v, (byte)Random.Range(60, maxA));
            }
            else
            {
                noisePixels[i] = new Color32(0, 0, 0, 0);
            }
        }
        noiseTex.SetPixels32(noisePixels);
        noiseTex.Apply();
    }

    // 0 = 通常, 1 = ヘビースタティック (カメラ死亡/ミミック時)
    public void SetGlitchIntensity(float t)
    {
        glitchTarget = Mathf.Clamp01(t);
    }
}
