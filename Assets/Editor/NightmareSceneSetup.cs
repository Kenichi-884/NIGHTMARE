// NIGHTMARE > Setup Full Scene  でシーン全体を自動構築します
// Shift+F5 ショートカット対応

using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public static class NightmareSceneSetup
{
    private const float W = 1920f;
    private const float H = 1080f;

    [MenuItem("NIGHTMARE/Setup Full Scene %#s", priority = 1)]
    public static void SetupScene()
    {
        if (!EditorUtility.DisplayDialog("NIGHTMARE Setup",
            "シーン全体をセットアップします。\n既存の [NIGHTMARE Root] は削除されます。",
            "実行", "キャンセル")) return;

        // AudioManager を Root から切り離して設定を保持する
        GameObject savedAudioGO = null;
        var existingAudio = Object.FindObjectOfType<AudioManager>();
        if (existingAudio != null)
        {
            savedAudioGO = existingAudio.gameObject;
            savedAudioGO.transform.SetParent(null); // Root 削除から保護
        }

        var old = GameObject.Find("[NIGHTMARE Root]");
        if (old) Object.DestroyImmediate(old);

        var root = new GameObject("[NIGHTMARE Root]");

        CreateManagers(root, savedAudioGO);
        var canvas = CreateCanvas(root);
        CreateEventSystem();
        SetupCamera();

        // セキュリティカメラ GameObjects（3D空間にカメラを配置）
        CreateSecurityCameras(root);

        // 各コンポーネントの自動接続
        canvas.GetComponent<UIManager>()?.AutoFindChildren();
        canvas.GetComponent<SecurityCameraSystem>()?.AutoFindMonitors();
        canvas.GetComponent<MainMenuManager>()?.AutoFindChildren();
        canvas.GetComponent<JumpScareManager>()?.AutoFindChildren();
        canvas.GetComponent<TitleSceneDirector>()?.AutoFindChildren();

        // ProximityAlertSystem に DangerVignette を接続
        WireProximityAlert(root);

        MarkDirty(root);
        Debug.Log("[NIGHTMARE] セットアップ完了！ Ctrl+S で保存してください。");
    }

    // ===== マネージャー =====
    static void CreateManagers(GameObject root, GameObject existingAudioGO = null)
    {
        var m = Child(root, "Managers");
        m.AddComponent<GameManager>();
        m.AddComponent<PowerManager>();
        m.AddComponent<PhenomenaManager>();
        m.AddComponent<WeatherManager>();

        // DontDestroyOnLoad なので独立したオブジェクトに配置
        var progress = Child(root, "StageProgress");
        progress.AddComponent<StageProgressManager>();

        // AudioManager: 既存があれば設定を保持したまま再利用、なければ新規作成
        if (existingAudioGO != null)
        {
            existingAudioGO.name = "AudioManager";
            existingAudioGO.transform.SetParent(root.transform);
            // AudioSource が不足していれば補完
            var sources = existingAudioGO.GetComponents<AudioSource>();
            if (sources.Length < 1) existingAudioGO.AddComponent<AudioSource>().loop = true;
            if (sources.Length < 2) existingAudioGO.AddComponent<AudioSource>().playOnAwake = false;
        }
        else
        {
            var audio = Child(root, "AudioManager");
            audio.AddComponent<AudioManager>();
            var bgm = audio.AddComponent<AudioSource>(); bgm.loop = true; bgm.playOnAwake = false;
            audio.AddComponent<AudioSource>().playOnAwake = false;
        }

        var sys = Child(root, "GameSystems");
        sys.AddComponent<DoorManager>();
        sys.AddComponent<MonsterManager>();
        sys.AddComponent<MonsterSpawner>();
        sys.AddComponent<ProximityAlertSystem>();
        sys.AddComponent<InputHandler>();
        sys.AddComponent<DebugOverlay>();
    }

    // ===== セキュリティカメラ (3D空間用 Unity Camera × 8) =====
    // 名前は "SceneCam_<CameraID>" 固定。SecurityCameraSystem が名前で検索して RenderTexture に接続する。
    // Setup 後、各カメラを Inspector で監視したい3D位置へ移動してください。
    static void CreateSecurityCameras(GameObject root)
    {
        var camRoot = Child(root, "SecurityCameras");

        (string name, Vector3 pos, Vector3 rot)[] cams =
        {
            ("SceneCam_OUT_N",   new Vector3(  0, 3, -20), new Vector3(15,   0, 0)),
            ("SceneCam_OUT_E",   new Vector3( 20, 3,   0), new Vector3(15, -90, 0)),
            ("SceneCam_OUT_W",   new Vector3(-20, 3,   0), new Vector3(15,  90, 0)),
            ("SceneCam_OUT_TOP", new Vector3(  0, 5, -10), new Vector3(30,   0, 0)),
            ("SceneCam_IN_1F_A", new Vector3(  0, 3,  10), new Vector3(15, 180, 0)),
            ("SceneCam_IN_1F_B", new Vector3(  0, 3,  20), new Vector3(15, 180, 0)),
            ("SceneCam_IN_B1_A", new Vector3(  0, 3,  30), new Vector3(15, 180, 0)),
            ("SceneCam_IN_B1_B", new Vector3(  0, 3,  40), new Vector3(15, 180, 0)),
        };

        foreach (var (name, pos, rot) in cams)
        {
            var go  = Child(camRoot, name);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = rot;
            var cam = go.AddComponent<Camera>();
            cam.clearFlags       = CameraClearFlags.SolidColor;
            cam.backgroundColor  = new Color(0.05f, 0.05f, 0.07f);
            cam.nearClipPlane    = 0.1f;
            cam.farClipPlane     = 100f;
            cam.fieldOfView      = 70f;
            cam.enabled          = false; // SecurityCameraSystem が管理
            // AudioListener は不要（MainCamera のみ持つ）
        }

        Debug.Log("[NIGHTMARE] SecurityCameras 生成完了 (8台) — Inspector で各カメラ位置を調整してください");
    }

    // ===== Canvas =====
    static GameObject CreateCanvas(GameObject root)
    {
        var cGO = Child(root, "Canvas");
        var canvas = cGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = cGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(W, H);
        scaler.matchWidthOrHeight = 0.5f;

        cGO.AddComponent<GraphicRaycaster>();
        cGO.AddComponent<UIManager>();
        cGO.AddComponent<SecurityCameraSystem>();
        cGO.AddComponent<MainMenuManager>();
        cGO.AddComponent<JumpScareManager>();
        cGO.AddComponent<ScreenShakeEffect>();
        cGO.AddComponent<TitleSceneDirector>();

        // 背景
        Img("Background", cGO, C(0.04f, 0.05f, 0.08f), stretch: true);

        // ── ゲーム画面 ──
        BuildGameScreen(cGO);

        // ── ジャンプスケアオーバーレイ ──
        BuildJumpScareOverlay(cGO);

        // ── メインメニュー (最前面) ──
        BuildMainMenu(cGO);

        // ── システムパネル ──
        BuildSystemPanels(cGO);

        return cGO;
    }

    // ===== ゲーム画面 =====
    static void BuildGameScreen(GameObject canvas)
    {
        var game = Panel("GameScreen", canvas, Color.clear);
        Rect(game, 0, 0, W, H);

        // 単一モニター（中央上部）
        BuildSingleMonitor(game);

        // 下部HUD
        BuildHUD(game);

        // マップパネル（Mキーでトグル）
        var map = Panel("MapPanel", game, C(0.04f, 0.06f, 0.10f, 0.96f));
        Rect(map, W - 312, 152, 292, 622);
        map.AddComponent<FacilityMapUI>();
        map.SetActive(false);
    }

    static void BuildSingleMonitor(GameObject parent)
    {
        float mw = 960f, mh = 780f;
        float mx = (W - mw) * 0.5f;
        float my = 142f;   // HUD(140px) のすぐ上

        var panel = Panel("MonitorPanel", parent, C(0.06f, 0.08f, 0.12f));
        Rect(panel, mx, my, mw, mh);

        // 外枠
        var frame = Img("MonitorFrame", panel, C(0.15f, 0.22f, 0.35f), stretch: true);
        frame.AddComponent<Outline>().effectColor = C(0.30f, 0.55f, 0.85f);

        // ── 映像エリア ──
        float vx = 8f, vy = 34f, vw = mw - 16f, vh = mh - 70f;

        // RawImage（RenderTexture を表示する）
        var rawGO = new GameObject("MonitorDisplay", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
        rawGO.transform.SetParent(panel.transform, false);
        Rect(rawGO, vx, vy, vw, vh);
        rawGO.GetComponent<UnityEngine.UI.RawImage>().color = C(0.05f, 0.05f, 0.07f);
        rawGO.AddComponent<CameraViewEffect>();

        // Jammer ノイズオーバーレイ
        Img("MonitorNoiseOverlay",   panel, C(0.5f, 0.5f, 0.5f, 0.05f));
        Rect(Find(panel, "MonitorNoiseOverlay"),   vx, vy, vw, vh);
        // スタティックオーバーレイ（死亡 / Mimic）
        Img("MonitorStaticOverlay",  panel, C(0.8f, 0.8f, 0.8f, 0f));
        Rect(Find(panel, "MonitorStaticOverlay"),  vx, vy, vw, vh);
        // モンスターオーバーレイ（カメラ映像上にスプライトを表示）
        var monOverlay = Img("MonitorMonsterOverlay", panel, Color.white);
        Rect(Find(panel, "MonitorMonsterOverlay"), vx + vw * 0.15f, vy + 10f, vw * 0.7f, vh - 20f);
        monOverlay.GetComponent<Image>().preserveAspect = true;
        monOverlay.GetComponent<Image>().enabled = false;

        // ── ラベル行（映像の上端）──
        // 左: カメラ名
        Lbl("CameraNameText",     panel, "OUT-N  北ゲート", 13,
            vx, vy + vh + 2f, vw * 0.5f, 20f, C(0.4f, 0.8f, 1f), TextAnchor.MiddleLeft);
        // 右: 監視中の場所
        Lbl("CameraLocationText", panel, "監視中:  北ゲート外部", 13,
            vx + vw * 0.5f, vy + vh + 2f, vw * 0.5f, 20f, C(0.6f, 1f, 0.6f), TextAnchor.MiddleRight);

        // ── カメラ切替ボタン（8台、映像エリアの下）──
        (string label, string id)[] cams =
        {
            ("OUT-N",   "BtnOutN"),  ("OUT-E",   "BtnOutE"),
            ("OUT-W",   "BtnOutW"),  ("OUT-TOP", "BtnOutTop"),
            ("IN-1F-A", "BtnIn1FA"),("IN-1F-B", "BtnIn1FB"),
            ("IN-B1-A", "BtnInB1A"),("IN-B1-B", "BtnInB1B"),
        };
        float btnW = (vw - 14f) / 8f;
        float bx = vx;
        for (int i = 0; i < cams.Length; i++)
        {
            Btn(cams[i].id, panel, cams[i].label, bx, 4f, btnW, 28f, C(0.10f, 0.16f, 0.28f));
            bx += btnW + 2f;
        }
    }

    static void BuildHUD(GameObject parent)
    {
        var hud = Panel("HUD", parent, C(0.05f, 0.07f, 0.11f));
        Rect(hud, 0, 0, W, 140);
        Img("HUDLine", hud, C(0.2f, 0.3f, 0.45f)); Rect(Find(hud, "HUDLine"), 0, 138, W, 2);

        // 時計パネル
        var tp = Panel("TimePanel", hud, C(0.07f, 0.10f, 0.16f));
        Rect(tp, 12, 10, 250, 118);
        Lbl("TimeText", tp, "20:00", 52, 0, 42, 250, 64, C(0.7f, 1f, 0.7f));
        Lbl("DayText",  tp, "Day 1 ― 静寂", 13, 0, 22, 250, 22, C(0.5f, 0.7f, 1f));

        // 電力パネル（強調表示・大型テキスト）
        var pp = Panel("PowerPanel", hud, C(0.07f, 0.10f, 0.16f));
        Rect(pp, 272, 10, 260, 118);
        Lbl("PowerLabel", pp, "POWER",  13, 10, 98, 100, 18, C(0.5f, 0.7f, 1f),         TextAnchor.MiddleLeft);
        Lbl("PowerText",  pp, "100%",   36, 10, 50, 130, 46, C(0.2f, 0.9f, 0.2f),        TextAnchor.MiddleLeft);
        Lbl("PowerWarn",  pp, "",       13, 10, 18, 240, 20, C(1f, 0.3f, 0.1f),           TextAnchor.MiddleCenter);
        BuildSlider("PowerSlider", "PowerFill", pp, 10, 10, 240, 42);

        // ドアパネル
        var dp = Panel("DoorPanel", hud, C(0.07f, 0.10f, 0.16f));
        Rect(dp, 542, 10, 700, 118);
        Lbl("DoorTitle", dp, "DOOR CONTROL", 11, 4, 100, 700, 16, C(0.5f, 0.7f, 1f), TextAnchor.MiddleLeft);
        BuildDoorButton("BtnGate",     "IndGate",     dp, "外壁ゲート", 8,    4);
        BuildDoorButton("BtnEntrance", "IndEntrance", dp, "地上入口",   174,  4);
        BuildDoorButton("BtnBasement", "IndBasement", dp, "地下階段",   340,  4);
        BuildDoorButton("BtnB1",       "IndB1",       dp, "B1廊下",     506,  4);
        Btn("BtnEmergency", dp, "緊急封鎖", 638,  38, 52, 78, C(0.7f, 0.10f, 0.08f));

        // 天候/現象パネル
        var wp = Panel("WeatherPanel", hud, C(0.07f, 0.10f, 0.16f));
        Rect(wp, 1252, 10, 180, 118);
        Lbl("WeatherTitle",  wp, "PHENOMENA",   11,  6, 98, 168, 18, C(0.5f, 0.7f, 1f),   TextAnchor.MiddleLeft);
        Lbl("WeatherStatus", wp, "  -",         14,  6, 52, 168, 42, C(0.9f, 0.8f, 0.3f), TextAnchor.MiddleLeft);
        Lbl("WeatherTimer",  wp, "",            11,  6, 14, 168, 36, C(0.7f, 0.7f, 0.7f), TextAnchor.MiddleLeft);

        // カメラ操作パネル
        var cp = Panel("CamPanel", hud, C(0.07f, 0.10f, 0.16f));
        Rect(cp, 1442, 10, 330, 118);
        Lbl("CamTitle", cp, "CAMERA CONTROL", 11, 4, 100, 330, 16, C(0.5f, 0.7f, 1f), TextAnchor.MiddleLeft);
        Btn("BtnResetCam", cp, "カメラ リセット  (-3%)", 8, 68, 314, 34, C(0.18f, 0.30f, 0.50f));
        Btn("BtnEliminate", cp, "Jammer 駆除  (-5%)",   8, 30, 314, 34, C(0.4f, 0.08f, 0.55f));
        Btn("BtnMap",       cp, "[M] 施設マップ",        8,  -8, 314, 30, C(0.14f, 0.20f, 0.30f));

        // 異常現象テキスト
        var warn = Lbl("PhenomenaWarning", hud, "", 22,
            W * 0.5f - 300, H * 0.6f - 140, 600, 60,
            C(1f, 0.3f, 0.1f), TextAnchor.MiddleCenter);
        warn.SetActive(false);
    }

    static void BuildSlider(string name, string fillName, GameObject parent, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(parent.transform, false);
        Rect(go, x, y, w, h);
        var sl = go.GetComponent<Slider>();

        Img("SliderBG", go, C(0.15f, 0.15f, 0.18f)); Rect(Find(go, "SliderBG"), 0, 0, w, h);
        var fa = Panel("FillArea", go, Color.clear); Rect(fa, 0, 0, w, h);
        var fill = Img(fillName, fa, C(0.2f, 0.9f, 0.2f)); Rect(Find(fa, fillName), 0, 0, w, h);
        sl.fillRect = fill.GetComponent<RectTransform>();
        sl.value = 1f;
        sl.interactable = false;
    }

    static void BuildDoorButton(string btnName, string indName, GameObject parent, string label, float x, float y)
    {
        var ind = Img(indName, parent, C(0.2f, 0.8f, 0.2f));
        Rect(ind, x, y + 76, 158, 12);
        Btn(btnName, parent, label, x, y, 158, 72, C(0.10f, 0.16f, 0.26f));
    }

    // ===== ジャンプスケアオーバーレイ =====
    static void BuildJumpScareOverlay(GameObject canvas)
    {
        var layer = Panel("JumpScareLayer", canvas, Color.clear);
        Rect(layer, 0, 0, W, H);

        Img("JumpScareBlack", layer, C(0, 0, 0, 0), stretch: true);
        var face = Img("JumpScareFace", layer, Color.clear, stretch: true);
        face.GetComponent<Image>().preserveAspect = true;
        Img("JumpScareFlash", layer, C(1, 1, 1, 0), stretch: true);
    }

    // ===== メインメニュー =====
    static void BuildMainMenu(GameObject canvas)
    {
        var mm = Panel("MainMenuRoot", canvas, Color.clear);
        Rect(mm, 0, 0, W, H);

        // フェードオーバーレイ
        Img("MenuFadeOverlay", mm, C(0, 0, 0, 1), stretch: true);

        // ── メインメニューパネル ──
        var main = Panel("MainMenuPanel", mm, Color.clear);
        Rect(main, 0, 0, W, H);

        // 背景（暗いグラデーション）
        Img("MenuBG", main, C(0.02f, 0.03f, 0.05f), stretch: true);

        // 赤い雰囲気ライン
        Img("AccentLine", main, C(0.6f, 0.05f, 0.05f, 0.8f));
        Rect(Find(main, "AccentLine"), 0, H * 0.5f + 80, W, 2);

        // タイトル
        Lbl("TitleMain",  main, "NIGHTMARE",           88, 0, H * 0.5f + 100, W, 120, C(0.85f, 0.85f, 0.90f));
        Lbl("TitleSub",   main, "深夜管理  ―  地下施設夜間管理記録",  22, 0, H * 0.5f + 84,  W, 36,  C(0.5f, 0.55f, 0.65f));
        Lbl("TitleFlavor",main, "午後8時。あなたは一人、施設に残されている。", 16, 0, H * 0.5f + 50, W, 28, C(0.35f, 0.40f, 0.50f));

        // ボタン
        float bx = W * 0.5f - 120;
        float by = H * 0.5f - 20;
        Btn("BtnStart",    main, "開始する",  bx, by,        240, 52, C(0.15f, 0.35f, 0.20f));
        Btn("BtnLore",     main, "記録を読む", bx, by - 62,   240, 46, C(0.14f, 0.20f, 0.32f));
        Btn("BtnSettings", main, "設定",       bx, by - 118,  240, 46, C(0.14f, 0.20f, 0.32f));
        Btn("BtnQuit",     main, "終了",       bx, by - 174,  240, 36, C(0.22f, 0.10f, 0.10f));

        Lbl("MenuVersion", main, "v0.1.0  PROTOTYPE", 10, 8, 8, 200, 16, C(0.3f, 0.3f, 0.35f), TextAnchor.MiddleLeft);

        // ── ホラー演出用オーバーレイ (TitleSceneDirector が制御) ──
        // 赤ヴィネット
        var vignette = Img("TitleVignette", main, C(0.55f, 0.02f, 0.02f, 0f), stretch: true);
        vignette.GetComponent<Image>().raycastTarget = false;

        // 静電気ノイズ
        var noise = Img("TitleNoise", main, C(0.55f, 0.55f, 0.55f, 0f), stretch: true);
        noise.GetComponent<Image>().raycastTarget = false;

        // ドア際の影（右端の縦長シルエット）
        var shadow = Img("TitleShadow", main, C(0f, 0f, 0f, 0f));
        Rect(shadow, W - 90, 0, 70, H);
        shadow.GetComponent<Image>().raycastTarget = false;
        shadow.SetActive(false);

        // ── 設定パネル ──
        var settings = Panel("SettingsPanel", mm, C(0.04f, 0.06f, 0.10f, 0.88f));
        Rect(settings, 0, 0, W, H);
        settings.SetActive(false);

        Lbl("SettingsTitle", settings, "設定", 48, 0, H * 0.5f + 140, W, 72, C(0.7f, 0.8f, 1f));

        var sv = Panel("SettingsBox", settings, C(0.08f, 0.11f, 0.18f));
        Rect(sv, W * 0.5f - 240, H * 0.5f - 60, 480, 200);
        Lbl("LblBGM", sv, "BGM", 18, 20, 148, 100, 28, C(0.7f, 0.8f, 1f), TextAnchor.MiddleLeft);
        BuildSettingsSlider("BGMSlider", sv, 120, 150, 320, 24);
        Lbl("LblSFX", sv, "SE",  18, 20, 104, 100, 28, C(0.7f, 0.8f, 1f), TextAnchor.MiddleLeft);
        BuildSettingsSlider("SFXSlider", sv, 120, 106, 320, 24);

        Btn("BtnSettingsBack", settings, "← 戻る", W * 0.5f - 80, H * 0.5f - 200, 160, 44, C(0.20f, 0.28f, 0.42f));

        // ── 記録（ロア）パネル ──
        var lore = Panel("LorePanel", mm, C(0.02f, 0.03f, 0.05f, 0.88f));
        Rect(lore, 0, 0, W, H);
        lore.SetActive(false);

        Lbl("LoreTitle", lore, "管理記録 #001", 32, 0, H - 100, W, 52, C(0.5f, 0.65f, 1f));

        var loreBG = Panel("LoreTextBG", lore, C(0.06f, 0.08f, 0.13f));
        Rect(loreBG, W * 0.5f - 380, 160, 760, H - 300);

        var loreText = Lbl("LoreText", loreBG, "", 18,
            20, 20, 720, H - 340, C(0.80f, 0.85f, 0.90f), TextAnchor.UpperLeft);
        loreText.GetComponent<Text>().supportRichText = true;

        Btn("BtnLoreBack", lore, "← 戻る", W * 0.5f - 80, 70, 160, 44, C(0.20f, 0.28f, 0.42f));

        // ── ステージ選択パネル ──
        var ss = Panel("StageSelectPanel", mm, C(0.02f, 0.03f, 0.07f, 0.88f));
        Rect(ss, 0, 0, W, H);
        ss.SetActive(false);

        Lbl("SSTitle",    ss, "ステージ選択",           48, 0, H * 0.5f + 228, W, 72,  C(0.7f, 0.8f, 1f));
        Lbl("SSSubtitle", ss, "クリアした夜から再挑戦できます", 16, 0, H * 0.5f + 206, W, 30, C(0.4f, 0.5f, 0.7f));
        Lbl("SSWeatherNote", ss,
            "晴れ: Day 1-3  /  雨: Day 4-6  /  嵐: Day 7",
            13, 0, H * 0.5f + 186, W, 22, C(0.35f, 0.45f, 0.60f));

        float dayBtnW = 300f, dayBtnH = 52f, dayBtnSpacing = 58f;
        float dayBtnX = W * 0.5f - dayBtnW * 0.5f;
        float dayBtnStartY = H * 0.5f + 144f;

        string[] weatherLabels = { "晴れ", "晴れ", "晴れ", "雨", "雨", "雨", "嵐" };
        for (int i = 0; i < 7; i++)
        {
            int day = i + 1;
            Color btnCol = day <= 3 ? C(0.12f, 0.22f, 0.15f) :
                           day <= 6 ? C(0.10f, 0.14f, 0.28f) :
                                      C(0.22f, 0.10f, 0.10f);
            Btn($"BtnDay{day}", ss,
                $"Day {day}  [{weatherLabels[i]}]",
                dayBtnX, dayBtnStartY - i * dayBtnSpacing,
                dayBtnW, dayBtnH, btnCol);
        }

        Btn("BtnStageSelectBack", ss, "← 戻る",
            W * 0.5f - 80, H * 0.5f - 260, 160, 44, C(0.20f, 0.28f, 0.42f));
    }

    static void BuildSettingsSlider(string name, GameObject parent, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(parent.transform, false);
        Rect(go, x, y, w, h);
        var sl = go.GetComponent<Slider>();
        Img("BG", go, C(0.15f, 0.15f, 0.20f)); Rect(Find(go, "BG"), 0, 0, w, h);
        var fa = Panel("FA", go, Color.clear); Rect(fa, 0, 0, w, h);
        var fill = Img("Fill", fa, C(0.3f, 0.55f, 0.8f)); Rect(Find(fa, "Fill"), 0, 0, w, h);
        var handle = Img("Handle", go, C(0.7f, 0.8f, 1f)); Rect(handle, 0, -2, 12, h + 4);
        sl.fillRect  = fill.GetComponent<RectTransform>();
        sl.targetGraphic = handle.GetComponent<Image>();
        sl.handleRect = handle.GetComponent<RectTransform>();
        sl.value = 0.8f;
    }

    // ===== システムパネル (GameOver / Clear / DayTransition) =====
    static void BuildSystemPanels(GameObject canvas)
    {
        // ── ゲームオーバー ──
        var go = FullPanel("GameOverPanel", canvas, C(0, 0, 0, 0.92f));
        Lbl("GameOverTitle", go, "GAME\nOVER", 90, 0, H * 0.5f + 80, W, 240, C(0.8f, 0.08f, 0.05f));
        Lbl("GameOverSub",   go, "管理人室に侵入された", 24, 0, H * 0.5f + 54, W, 40, C(0.6f, 0.6f, 0.6f));
        Lbl("KillerLabel",   go, "", 16, 0, H * 0.5f + 30, W, 30, C(0.5f, 0.5f, 0.5f));
        Btn("RetryButton",      go, "同じ夜をやり直す", W * 0.5f - 270, H * 0.5f - 60, 240, 50, C(0.30f, 0.14f, 0.08f));
        Btn("BackToMenuButton", go, "タイトルへ戻る",   W * 0.5f +  30, H * 0.5f - 60, 240, 50, C(0.14f, 0.18f, 0.28f));

        // ── 夜クリア ──
        var clr = FullPanel("ClearPanel", canvas, C(0, 0, 0, 0.92f));
        Lbl("ClearTitle", clr, "SURVIVED", 80, 0, H * 0.5f + 120, W, 120, C(0.2f, 1f, 0.5f));
        Lbl("ClearSub",   clr, "夜明けを迎えた", 26, 0, H * 0.5f + 86, W, 44, C(0.6f, 0.9f, 0.6f));
        Lbl("TrueEndMsg", clr, "全7日間を乗り越えた\n施設からの脱出に成功した", 22, 0, H * 0.5f, W, 80, C(0.5f, 0.8f, 0.5f));
        Btn("TitleButton", clr, "タイトルへ戻る", W * 0.5f - 120, H * 0.5f - 100, 240, 50, C(0.14f, 0.25f, 0.38f));

        // ── 日付遷移 ──
        var dt = FullPanel("DayTransitionPanel", canvas, C(0, 0, 0, 0.94f));
        Lbl("DayTransitionText", dt, "", 21,
            W * 0.5f - 440, H * 0.5f - 80, 880, 500, C(0.80f, 0.88f, 1f), TextAnchor.UpperCenter)
            .GetComponent<Text>().supportRichText = true;
        Btn("NextNightButton", dt, "次の夜へ →", W * 0.5f - 120, H * 0.5f - 310, 240, 54, C(0.12f, 0.26f, 0.44f));

        // 近接危険ビネット（赤い縁どり）
        var vignette = Img("DangerVignette", canvas, C(0.8f, 0.04f, 0.04f, 0f), stretch: true);
        vignette.GetComponent<Image>().raycastTarget = false;

        // GhostSignalオーバーレイ
        var ghost = Img("GhostSignalOverlay", canvas, C(0.6f, 0.2f, 1f, 0.12f), stretch: true);
        ghost.SetActive(false);
    }

    // ===== EventSystem / Camera =====
    static void CreateEventSystem()
    {
        if (!Object.FindObjectOfType<EventSystem>())
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    static void SetupCamera()
    {
        var cam = Camera.main ?? new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
        cam.tag = "MainCamera";
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = C(0.02f, 0.02f, 0.03f);
        cam.orthographic = true;
    }

    // ===== ユーティリティ =====
    static GameObject Child(GameObject p, string name)
    {
        var g = new GameObject(name); g.transform.SetParent(p.transform, false); return g;
    }

    static GameObject Img(string name, object parent, Color col, bool stretch = false)
    {
        var p = ToTF(parent);
        var g = new GameObject(name, typeof(RectTransform), typeof(Image));
        g.transform.SetParent(p, false);
        g.GetComponent<Image>().color = col;
        if (stretch) Stretch(g);
        return g;
    }

    static GameObject Panel(string name, object parent, Color col)
        => Img(name, parent, col);

    static GameObject FullPanel(string name, GameObject parent, Color col)
    {
        var g = Panel(name, parent, col); Stretch(g); g.SetActive(false); return g;
    }

    static void Rect(object go, float x, float y, float w, float h)
    {
        var rt = ToRT(go);
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    static void Stretch(object go)
    {
        var rt = ToRT(go);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static GameObject Btn(string name, object parent, string label,
        float x, float y, float w, float h, Color bg)
    {
        var g = Img(name, parent, bg);
        Rect(g, x, y, w, h);
        g.AddComponent<Button>();
        Lbl(name + "_L", g, label, Mathf.Max(10, (int)(h * 0.36f)),
            0, 0, w, h, Color.white);
        return g;
    }

    static GameObject Lbl(string name, object parent, string text, int size,
        float x, float y, float w, float h, Color col,
        TextAnchor align = TextAnchor.MiddleCenter)
    {
        var p = ToTF(parent);
        var g = new GameObject(name, typeof(RectTransform), typeof(Text));
        g.transform.SetParent(p, false);
        Rect(g, x, y, w, h);
        var t = g.GetComponent<Text>();
        t.text = text; t.fontSize = size; t.color = col; t.alignment = align;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                 ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        return g;
    }

    static GameObject Find(object parent, string name)
    {
        var p = ToTF(parent);
        foreach (Transform c in p) if (c.name == name) return c.gameObject;
        return null;
    }

    static void MarkDirty(GameObject root)
    {
        EditorUtility.SetDirty(root);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    static void WireProximityAlert(GameObject root)
    {
        var ps = Object.FindObjectOfType<ProximityAlertSystem>();
        var vignetteGO = GameObject.Find("DangerVignette");
        if (ps == null || vignetteGO == null) return;
        var so = new SerializedObject(ps);
        so.FindProperty("dangerVignette").objectReferenceValue = vignetteGO.GetComponent<Image>();
        so.ApplyModifiedProperties();
    }

    static Color C(float r, float g, float b, float a = 1f) => new Color(r, g, b, a);
    static Transform ToTF(object o) => o is GameObject go ? go.transform : (Transform)o;
    static RectTransform ToRT(object o)
    {
        var t = ToTF(o);
        return t as RectTransform ?? t.GetComponent<RectTransform>();
    }
}
