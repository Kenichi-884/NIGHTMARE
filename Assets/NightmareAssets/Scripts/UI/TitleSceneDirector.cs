using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// タイトル画面専用の3D管理人室を生成し、RenderTexture経由で背景表示する。
/// ホラー演出（ヴィネット・静電気・影・タイトルグリッチ・蛍光灯フリッカー）も管理。
/// </summary>
public class TitleSceneDirector : MonoBehaviour
{
    [Header("UI References (AutoFind可)")]
    [SerializeField] Image    menuBG;
    [SerializeField] Image    vignetteOverlay;
    [SerializeField] Image    noiseOverlay;
    [SerializeField] Image    shadowFigure;
    [SerializeField] Text     titleText;
    [SerializeField] Text     titleSubText;

    [Header("タイトル中に隠すゲーム用Canvas")]
    [Tooltip("タイトル表示中は非表示にするゲームHUD/パネルのCanvas（複数可）")]
    [SerializeField] Canvas[] gameCanvases;

    Camera        _bgCam;
    Camera        _mainCam;             // タイトル中は描画を封じるゲームカメラ
    CameraClearFlags _mainCamOrigFlags;
    Color            _mainCamOrigBG;
    int              _mainCamOrigMask;
    RenderTexture _rt;
    RawImage      _bgDisplay;
    Material      _bgMat;        // NIGHTMARE/TitleBG
    Material      _crtOverlayMat;// NIGHTMARE/TitleCRTOverlay
    GameObject    _roomRoot;
    Light         _overheadLight;
    bool          _running;
    float         _camAngle;
    float         _swingT;

    // タイトルテキスト初期値（StopEffects で復元）
    Vector2 _titleOrigPos;
    Vector3 _titleOrigScale;
    string  _titleOrigText;

    const string SCRAMBLE_CHARS = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789@#$%&!?";

    [Header("タイトルカメラ位置")]
    [Tooltip("カメラが部屋のどのZ位置を中心に見るか。小さい(マイナス)ほど奥の壁寄り。")]
    [SerializeField] private float camLookAtZ     = -4.5f;
    [Tooltip("注視点からのカメラ距離。小さいほど前（注視点に近い）。")]
    [SerializeField] private float camOrbitRadius = 3.0f;
    [Tooltip("カメラの高さ。")]
    [SerializeField] private float camHeight      = 1.55f;

    [Header("タイトルカメラスイング")]
    [Tooltip("中心から左右に振れる最大角度（度）。30で±30度スイング。")]
    [SerializeField] private float swingAngleDeg   = 30f;
    [Tooltip("スイングの速さ。大きいほど速く往復する。0.03 = 約210秒で1往復。")]
    [SerializeField] private float swingSpeed      = 0.03f;
    [Tooltip("スイング開始位置。0=中央(右へ), 0.25=右端, 0.5=中央(左へ), 0.75=左端")]
    [SerializeField, Range(0f, 1f)] private float swingPhaseOffset = 0f;
    // ゲームステージと被らないよう大きくオフセット
    static readonly Vector3 ROOM_OFFSET = new Vector3(5000f, 0f, 0f);
    static readonly Vector3 LOOK_AT     = new Vector3(5000f, 1.0f, -4.5f);

    // ─── 初期化 ──────────────────────────────────────────────────

    void Start()
    {
        _rt    = new RenderTexture(1920, 1080, 24);
        _bgCam = CreateBGCamera();
        CreateBGDisplay();

        // 事前生成済みの部屋を探す。なければ動的生成（フォールバック）
        _roomRoot = GameObject.Find("[TitleRoom]");
        if (_roomRoot == null) BuildRoom();
        else _roomRoot.SetActive(true);

        // MenuBG（ベタ塗り）は非表示にしてRawImageに置き換え
        if (menuBG != null) menuBG.gameObject.SetActive(false);

        // 蛍光灯ライトを取得（既存プレハブ内から）
        var lightGO = _roomRoot.transform.Find("OverheadLight");
        if (lightGO != null) _overheadLight = lightGO.GetComponent<Light>();

        // タイトルの初期値を保存
        if (titleText != null)
        {
            _titleOrigPos   = titleText.rectTransform.anchoredPosition;
            _titleOrigScale = titleText.rectTransform.localScale;
            _titleOrigText  = titleText.text;
        }

        // ゲーム用Canvasをタイトル中は非表示にする
        if (gameCanvases != null)
            foreach (var c in gameCanvases)
                if (c != null) c.gameObject.SetActive(false);

        // タイトル中は MainCamera を「真っ黒・何も描かない」状態にする
        // （無効化すると画面に映すカメラがゼロになり "No Camera" が出る）
        _mainCam = Camera.main;
        if (_mainCam != null)
        {
            _mainCamOrigFlags = _mainCam.clearFlags;
            _mainCamOrigBG    = _mainCam.backgroundColor;
            _mainCamOrigMask  = _mainCam.cullingMask;
            _mainCam.clearFlags      = CameraClearFlags.SolidColor;
            _mainCam.backgroundColor = Color.black;
            _mainCam.cullingMask     = 0; // 何も描画しない
        }

        _swingT  = swingPhaseOffset * Mathf.PI * 2f;
        _running = true;
        StartCoroutine(HorrorLoop());
        // StartCoroutine(TitleBreathing());
        StartCoroutine(LightFlicker());
        if (titleSubText != null) StartCoroutine(SubTextTypewriter());
    }

    void Update()
    {
        if (!_running || _bgCam == null) return;
        _swingT  += Time.deltaTime * swingSpeed;
        _camAngle  = Mathf.Sin(_swingT) * swingAngleDeg * Mathf.Deg2Rad;
        float s = Mathf.Sin(_camAngle);
        float c = Mathf.Cos(_camAngle);
        float h = camHeight + Mathf.Sin(Time.time * 0.38f) * 0.05f;
        _bgCam.transform.position = new Vector3(ROOM_OFFSET.x + s * camOrbitRadius, h, camLookAtZ + c * camOrbitRadius);
        var lookAt = new Vector3(ROOM_OFFSET.x, 1.0f, camLookAtZ);
        _bgCam.transform.LookAt(lookAt + new Vector3(0, Mathf.Sin(Time.time * 0.18f) * 0.04f, 0));
    }

    // ─── カメラ & RenderTexture ───────────────────────────────────

    Camera CreateBGCamera()
    {
        var go  = new GameObject("TitleBGCamera");
        var cam = go.AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.008f, 0.007f, 0.012f);
        cam.fieldOfView     = 60f;
        cam.nearClipPlane   = 0.1f;
        cam.farClipPlane    = 35f;
        cam.targetTexture   = _rt;
        cam.depth           = -10;
        cam.allowHDR        = false; // タイトルRTにHDR不要
        cam.allowMSAA       = false; // RTのMSAA不要（アンチエイリアスはなくてよい）
        go.transform.position = new Vector3(ROOM_OFFSET.x, camHeight, camLookAtZ + camOrbitRadius);
        go.transform.LookAt(new Vector3(ROOM_OFFSET.x, 1.0f, camLookAtZ));
        return cam;
    }

    void CreateBGDisplay()
    {
        // MainMenuRoot 直下の最背面に配置 → 全パネルの後ろに表示される
        var mmRoot = FindDeepTransform("MainMenuRoot");
        if (mmRoot == null)
        {
            Debug.LogWarning("[TitleSceneDirector] MainMenuRoot が見つかりません");
            return;
        }
        var go  = new GameObject("TitleBGDisplay", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(mmRoot, false);
        go.transform.SetAsFirstSibling();
        var rt  = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        _bgDisplay = go.GetComponent<RawImage>();
        _bgDisplay.texture = _rt;

        // クロマティックアベレーション + スキャンライン + グレイン シェーダーを適用
        var bgShader = Shader.Find("NIGHTMARE/TitleBG");
        if (bgShader != null)
        {
            _bgMat = new Material(bgShader);
            _bgDisplay.material = _bgMat;
        }

        // UIテキスト上のスキャンライン + グレイン オーバーレイ
        var overlayShader = Shader.Find("NIGHTMARE/TitleCRTOverlay");
        if (overlayShader != null)
        {
            _crtOverlayMat = new Material(overlayShader);
            var overlayGO = new GameObject("TitleCRTOverlay", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            overlayGO.transform.SetParent(mmRoot, false);
            var ort = overlayGO.GetComponent<RectTransform>();
            ort.anchorMin = Vector2.zero; ort.anchorMax = Vector2.one;
            ort.offsetMin = ort.offsetMax = Vector2.zero;
            var img = overlayGO.GetComponent<UnityEngine.UI.Image>();
            img.material = _crtOverlayMat;
            img.color = Color.white;
            img.raycastTarget = false;
        }
    }

    // ─── 3D 管理人室の生成 ────────────────────────────────────────

    void BuildRoom()
    {
        _roomRoot = new GameObject("[TitleRoom]");

        var darkMat  = StdMat(0.05f, 0.04f, 0.04f, 0f,   0.04f);
        var wallMat  = StdMat(0.07f, 0.065f, 0.065f, 0f, 0.02f);
        var metalMat = StdMat(0.12f, 0.12f, 0.14f, 0.6f, 0.25f);
        var floorMat = StdMat(0.06f, 0.055f, 0.052f, 0f, 0.03f);

        // 床・天井・壁
        MkBox("Floor",     new Vector3(0,     0.0f, -4f), new Vector3(7f, 0.12f, 9f),  floorMat);
        MkBox("Ceiling",   new Vector3(0,     4.0f, -4f), new Vector3(7f, 0.12f, 9f),  darkMat);
        MkBox("WallBack",  new Vector3(0,     2.0f, -8.5f), new Vector3(7f, 4.3f, 0.15f), wallMat);
        MkBox("WallLeft",  new Vector3(-3.5f, 2.0f, -4f), new Vector3(0.15f, 4.3f, 9f), wallMat);
        MkBox("WallRight", new Vector3( 3.5f, 2.0f, -4f), new Vector3(0.15f, 4.3f, 9f), wallMat);

        // ドア枠（左壁）
        MkBox("DoorFrame", new Vector3(-3.42f, 1.15f, 0.2f), new Vector3(0.08f, 2.3f, 1.1f), darkMat);

        // 机
        MkBox("DeskTop",  new Vector3(0,     0.78f, -6.5f), new Vector3(3.0f, 0.07f, 1.1f), metalMat);
        MkBox("DeskLegFL",new Vector3(-1.3f, 0.39f, -6.0f), new Vector3(0.07f, 0.78f, 0.07f), metalMat);
        MkBox("DeskLegFR",new Vector3( 1.3f, 0.39f, -6.0f), new Vector3(0.07f, 0.78f, 0.07f), metalMat);
        MkBox("DeskLegBL",new Vector3(-1.3f, 0.39f, -7.0f), new Vector3(0.07f, 0.78f, 0.07f), metalMat);
        MkBox("DeskLegBR",new Vector3( 1.3f, 0.39f, -7.0f), new Vector3(0.07f, 0.78f, 0.07f), metalMat);

        // モニター（青白く発光）
        var monScreenMat = new Material(ResolveShader());
        monScreenMat.color = new Color(0.02f, 0.04f, 0.08f);
        monScreenMat.EnableKeyword("_EMISSION");
        monScreenMat.SetColor("_EmissionColor", new Color(0.02f, 0.06f, 0.18f));
        MkBox("MonitorScreen", new Vector3(0,     1.26f, -7.42f), new Vector3(1.4f, 0.85f, 0.05f), monScreenMat);
        MkBox("MonitorBase",   new Vector3(0,     0.88f, -7.32f), new Vector3(0.2f, 0.2f, 0.2f),   metalMat);

        // キーボード
        MkBox("Keyboard", new Vector3(0, 0.82f, -6.15f), new Vector3(0.9f, 0.02f, 0.35f), metalMat);

        // 椅子
        MkBox("ChairSeat", new Vector3(0, 0.46f, -5.1f), new Vector3(0.72f, 0.07f, 0.72f), darkMat);
        MkBox("ChairBack", new Vector3(0, 0.92f, -4.76f), new Vector3(0.72f, 0.72f, 0.07f), darkMat);
        MkBox("ChairLegFL", new Vector3(-0.3f, 0.23f, -4.9f),  new Vector3(0.05f, 0.46f, 0.05f), metalMat);
        MkBox("ChairLegFR", new Vector3( 0.3f, 0.23f, -4.9f),  new Vector3(0.05f, 0.46f, 0.05f), metalMat);
        MkBox("ChairLegBL", new Vector3(-0.3f, 0.23f, -5.4f),  new Vector3(0.05f, 0.46f, 0.05f), metalMat);
        MkBox("ChairLegBR", new Vector3( 0.3f, 0.23f, -5.4f),  new Vector3(0.05f, 0.46f, 0.05f), metalMat);

        // スチール棚（右壁沿い）
        MkBox("Shelf",      new Vector3(3.0f, 0.9f,  -7.5f), new Vector3(0.65f, 1.8f, 0.7f), metalMat);
        MkBox("ShelfItem0", new Vector3(3.0f, 1.4f,  -7.2f), new Vector3(0.3f, 0.25f, 0.2f), darkMat);
        MkBox("ShelfItem1", new Vector3(2.8f, 1.75f, -7.3f), new Vector3(0.15f, 0.35f, 0.15f), darkMat);

        // 電話機
        MkBox("Phone", new Vector3(-1.0f, 0.84f, -7.0f), new Vector3(0.22f, 0.06f, 0.32f), darkMat);

        // 配管（天井）
        MkBox("Pipe0", new Vector3(-1f, 3.85f, -4f), new Vector3(0.1f, 0.1f, 9f), metalMat);
        MkBox("Pipe1", new Vector3( 1f, 3.85f, -4f), new Vector3(0.1f, 0.1f, 9f), metalMat);

        // ─── ライト設定 ───────────────────────────────────────────

        // 天井蛍光灯（薄く青白い、フリッカー対象）
        _overheadLight = AddLight("OverheadLight",
            new Vector3(0, 3.8f, -4.5f),
            new Color(0.68f, 0.72f, 0.80f), 0.65f, 14f, LightType.Point);

        // モニターの青い反射光
        AddLight("MonitorBlue",
            new Vector3(0, 1.1f, -6.8f),
            new Color(0.12f, 0.28f, 0.75f), 0.55f, 4f, LightType.Point);

        // 不気味な赤環境光（右上奥）
        AddLight("RedAmbient",
            new Vector3(3f, 3.2f, -7f),
            new Color(0.75f, 0.05f, 0.03f), 0.22f, 9f, LightType.Point);

        // ドア側の薄暗い光（廊下からの漏れ光）
        AddLight("CorridorLight",
            new Vector3(-3f, 2.5f, 1.0f),
            new Color(0.5f, 0.45f, 0.35f), 0.15f, 6f, LightType.Point);

        // アンビエント（ほぼ真っ暗）
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.012f, 0.01f, 0.016f);
    }

    // ─── ホラーエフェクト ────────────────────────────────────────

    IEnumerator HorrorLoop()
    {
        yield return new WaitForSeconds(3f);
        while (_running)
        {
            yield return new WaitForSeconds(Random.Range(5f, 13f));
            if (!_running) yield break;
            switch (Random.Range(0, 6))
            {
                case 0: yield return StartCoroutine(StaticBurst());       break;
                case 1: yield return StartCoroutine(VignettePulse());     break;
                case 2: yield return StartCoroutine(ShadowAppear());      break;
                case 3: yield return StartCoroutine(TitleGlitch());       break;
                case 4: yield return StartCoroutine(TitleScramble());     break;
                case 5: yield return StartCoroutine(ChromaticPulse());    break;
            }
        }
    }

    // 静電気ノイズフラッシュ
    IEnumerator StaticBurst()
    {
        if (noiseOverlay == null) yield break;
        AudioManager.Instance?.Play("camera_static");
        float t = 0f, dur = Random.Range(0.2f, 0.65f);
        while (t < dur)
        {
            t += Time.deltaTime;
            SetAlpha(noiseOverlay, Mathf.PingPong(t * 11f, 1f) * 0.38f);
            yield return null;
        }
        SetAlpha(noiseOverlay, 0f);
    }

    // 赤ヴィネット脈動
    IEnumerator VignettePulse()
    {
        if (vignetteOverlay == null) yield break;
        float t = 0f, dur = 3.0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            SetAlpha(vignetteOverlay, Mathf.Sin(t / dur * Mathf.PI) * 0.52f);
            yield return null;
        }
        SetAlpha(vignetteOverlay, 0f);
    }

    // ドア際に影が現れる
    IEnumerator ShadowAppear()
    {
        if (shadowFigure == null) yield break;
        shadowFigure.gameObject.SetActive(true);
        // フェードイン
        float t = 0f;
        while (t < 0.7f) { t += Time.deltaTime; SetAlpha(shadowFigure, t / 0.7f * 0.8f); yield return null; }
        yield return new WaitForSeconds(Random.Range(0.5f, 2.5f));
        // フェードアウト
        t = 0f;
        while (t < 0.3f) { t += Time.deltaTime; SetAlpha(shadowFigure, (1f - t / 0.3f) * 0.8f); yield return null; }
        shadowFigure.gameObject.SetActive(false);
    }

    // タイトル文字グリッチ（色変化 + 位置シェイク）
    IEnumerator TitleGlitch()
    {
        if (titleText == null) yield break;
        Color  origCol = titleText.color;
        var    rt      = titleText.rectTransform;
        float elapsed = 0f, dur = Random.Range(0.3f, 0.85f);
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            // 色グリッチ
            titleText.color = Random.value > 0.45f
                ? new Color(origCol.r * Random.Range(0.2f, 1f),
                            origCol.g * Random.Range(0.05f, 0.4f),
                            origCol.b * Random.Range(0.2f, 1f))
                : origCol;
            // 位置シェイク（呼吸アニメ基準からのオフセット）
            // rt.anchoredPosition = _titleOrigPos + Random.insideUnitCircle * 9f;
            yield return new WaitForSeconds(0.033f);
        }
        titleText.color = origCol;
        // rt.anchoredPosition = _titleOrigPos;
    }

    // クロマティックアベレーション急増 + 静電ノイズ同時発動
    IEnumerator ChromaticPulse()
    {
        if (_bgMat == null) yield break;
        const float BASE = 0.005f;
        AudioManager.Instance?.Play("camera_static");

        // 急激に強くなる
        float t = 0f, rampUp = 0.15f;
        while (t < rampUp)
        {
            t += Time.deltaTime;
            _bgMat.SetFloat("_ChromaStr", Mathf.Lerp(BASE, 0.022f, t / rampUp));
            if (noiseOverlay) SetAlpha(noiseOverlay, (t / rampUp) * 0.3f);
            yield return null;
        }

        // しばらく維持（ランダム揺らぎ）
        float hold = Random.Range(0.4f, 1.2f);
        t = 0f;
        while (t < hold)
        {
            t += Time.deltaTime;
            float jitter = 0.018f + Mathf.Sin(t * 30f) * 0.004f;
            _bgMat.SetFloat("_ChromaStr", jitter);
            if (noiseOverlay) SetAlpha(noiseOverlay, Mathf.PingPong(t * 8f, 1f) * 0.25f);
            yield return null;
        }

        // ゆっくり戻る
        t = 0f;
        float rampDown = 0.5f;
        while (t < rampDown)
        {
            t += Time.deltaTime;
            _bgMat.SetFloat("_ChromaStr", Mathf.Lerp(0.018f, BASE, t / rampDown));
            if (noiseOverlay) SetAlpha(noiseOverlay, (1f - t / rampDown) * 0.2f);
            yield return null;
        }

        _bgMat.SetFloat("_ChromaStr", BASE);
        if (noiseOverlay) SetAlpha(noiseOverlay, 0f);
    }

    // タイトル文字スクランブル（ランダム文字に化ける）
    IEnumerator TitleScramble()
    {
        if (titleText == null) yield break;
        string orig    = titleText.text;
        var    sb      = new System.Text.StringBuilder(orig.Length);
        float elapsed  = 0f, dur = Random.Range(0.5f, 1.1f);
        AudioManager.Instance?.Play("camera_static");
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            sb.Clear();
            for (int i = 0; i < orig.Length; i++)
                sb.Append(orig[i] == ' ' ? ' '
                    : SCRAMBLE_CHARS[Random.Range(0, SCRAMBLE_CHARS.Length)]);
            titleText.text = sb.ToString();
            yield return new WaitForSeconds(0.042f);
        }
        titleText.text = orig;
    }

    // 蛍光灯フリッカー（常時、ランダム間隔）
    IEnumerator LightFlicker()
    {
        if (_overheadLight == null) yield break;
        float baseIntensity = _overheadLight.intensity;
        while (_running)
        {
            yield return new WaitForSeconds(Random.Range(3f, 18f));
            int flicks = Random.Range(1, 5);
            for (int i = 0; i < flicks; i++)
            {
                _overheadLight.intensity = Random.Range(0f, 0.1f);
                yield return new WaitForSeconds(Random.Range(0.03f, 0.12f));
                _overheadLight.intensity = baseIntensity * Random.Range(0.4f, 1f);
                yield return new WaitForSeconds(Random.Range(0.04f, 0.1f));
            }
            _overheadLight.intensity = baseIntensity;
        }
    }

    // タイトル文字アニメーション（スケール脈動 + 縦浮遊 + 横ドリフト）
    IEnumerator TitleBreathing()
    {
        if (titleText == null) yield break;
        var rt = titleText.rectTransform;
        while (_running)
        {
            float t = Time.time;
            // スケール脈動（少し大きめ）
            rt.localScale = _titleOrigScale * (1f + Mathf.Sin(t * 0.55f) * 0.008f);
            // 縦浮遊
            float floatY = Mathf.Sin(t * 0.28f) * 5f;
            // 横ドリフト（ごく微細）
            float driftX = Mathf.Sin(t * 0.17f + 1.3f) * 1.8f;
            rt.anchoredPosition = _titleOrigPos + new Vector2(driftX, floatY);
            yield return null;
        }
        rt.localScale        = _titleOrigScale;
        rt.anchoredPosition  = _titleOrigPos;
    }

    // サブテキストのタイプライター効果（起動時1回）
    IEnumerator SubTextTypewriter()
    {
        if (titleSubText == null) yield break;
        string full = titleSubText.text;
        titleSubText.text = "";
        yield return new WaitForSeconds(1.2f); // タイトルフリッカー後に開始
        foreach (char c in full)
        {
            titleSubText.text += c;
            yield return new WaitForSeconds(c == ' ' ? 0.06f : 0.045f);
        }
    }

    // ─── 停止（ゲーム開始時に呼ぶ）────────────────────────────────

    public void StopEffects()
    {
        _running = false;
        StopAllCoroutines();
        // ゲーム用Canvasを復元
        if (gameCanvases != null)
            foreach (var c in gameCanvases)
                if (c != null) c.gameObject.SetActive(true);

        // ゲーム開始時に MainCamera の設定を復元してから TitleBGCamera を止める
        if (_mainCam != null)
        {
            _mainCam.clearFlags      = _mainCamOrigFlags;
            _mainCam.backgroundColor = _mainCamOrigBG;
            _mainCam.cullingMask     = _mainCamOrigMask;
        }
        if (_bgCam  != null) _bgCam.gameObject.SetActive(false);
        if (_roomRoot!= null) _roomRoot.SetActive(false);
        if (_bgDisplay != null) _bgDisplay.gameObject.SetActive(false);
        if (menuBG   != null) menuBG.gameObject.SetActive(true);
        if (vignetteOverlay != null) SetAlpha(vignetteOverlay, 0f);
        if (noiseOverlay    != null) SetAlpha(noiseOverlay, 0f);
        if (shadowFigure    != null) shadowFigure.gameObject.SetActive(false);
        // タイトルテキストを元の状態に戻す
        if (titleText != null)
        {
            // titleText.rectTransform.localScale       = _titleOrigScale != Vector3.zero ? _titleOrigScale : Vector3.one;
            // titleText.rectTransform.anchoredPosition = _titleOrigPos;
            if (!string.IsNullOrEmpty(_titleOrigText)) titleText.text = _titleOrigText;
        }
        if (titleSubText != null && !string.IsNullOrEmpty(titleSubText.text))
            titleSubText.text = titleSubText.text; // そのまま維持
        // マテリアル・RenderTexture 解放
        if (_bgMat         != null) { Destroy(_bgMat);         _bgMat = null; }
        if (_crtOverlayMat != null) { Destroy(_crtOverlayMat); _crtOverlayMat = null; }
        if (_rt != null) { _rt.Release(); _rt = null; }
    }

    // ─── エディタセットアップから呼ぶ ────────────────────────────

    public void AutoFindChildren()
    {
        menuBG          = FindDeep<Image>("MenuBG");
        vignetteOverlay = FindDeep<Image>("TitleVignette");
        noiseOverlay    = FindDeep<Image>("TitleNoise");
        shadowFigure    = FindDeep<Image>("TitleShadow");
        titleText       = FindDeep<Text>("TitleMain");
        titleSubText    = FindDeep<Text>("TitleSub");
    }

    // ─── ユーティリティ ──────────────────────────────────────────

    void SetAlpha(Graphic g, float a)
    {
        var c = g.color; c.a = a; g.color = c;
    }

    static Shader ResolveShader()
    {
        var s = Shader.Find("Universal Render Pipeline/Lit");
        if (s != null) return s;
        s = Shader.Find("Standard");
        if (s != null) return s;
        Debug.LogError("[TitleSceneDirector] Lit シェーダーが見つかりません。");
        return Shader.Find("Diffuse");
    }

    Material StdMat(float r, float g, float b, float metallic, float gloss)
    {
        var m = new Material(ResolveShader());
        m.color = new Color(r, g, b);
        m.SetFloat("_Metallic", metallic);
        // URP は _Smoothness、Standard は _Glossiness — 両方セットしておく
        m.SetFloat("_Glossiness", gloss);
        m.SetFloat("_Smoothness", gloss);
        return m;
    }

    void MkBox(string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(_roomRoot.transform);
        go.transform.localPosition = ROOM_OFFSET + pos;
        go.transform.localScale    = scale;

        // マテリアルのインスタンスを作りテクスチャタイリングをスケールに合わせて補正
        var r   = go.GetComponent<Renderer>();
        var m   = new Material(mat);
        // 最も薄い軸を法線方向とみなし、残り 2 軸でタイリングを決める
        Vector2 tiling;
        if (scale.x <= scale.y && scale.x <= scale.z)
            tiling = new Vector2(scale.z, scale.y);
        else if (scale.y <= scale.x && scale.y <= scale.z)
            tiling = new Vector2(scale.x, scale.z);
        else
            tiling = new Vector2(scale.x, scale.y);
        m.SetTextureScale("_BaseMap",  tiling);
        m.SetTextureScale("_MainTex",  tiling); // Standard シェーダー互換
        r.material = m;

        Object.Destroy(go.GetComponent<Collider>());
    }

    Light AddLight(string name, Vector3 pos, Color col, float intensity, float range, LightType type)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_roomRoot.transform);
        go.transform.localPosition = ROOM_OFFSET + pos;
        var l = go.AddComponent<Light>();
        l.type      = type;
        l.color     = col;
        l.intensity = intensity;
        l.range     = range;
        l.shadows   = LightShadows.None; // RTに映るだけなのでシャドウ不要
        return l;
    }

    T FindDeep<T>(string name) where T : Component
    {
        foreach (var c in GetComponentsInChildren<T>(true))
            if (c.gameObject.name == name) return c;
        return null;
    }

    Transform FindDeepTransform(string name)
    {
        foreach (Transform c in GetComponentsInChildren<Transform>(true))
            if (c.name == name) return c;
        return null;
    }
}
