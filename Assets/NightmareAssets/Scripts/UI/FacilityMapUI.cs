using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 施設マップをリアルタイムで表示する自己完結型UIコンポーネント。
///
/// ─ プレハブ対応 ─────────────────────────────────────────
/// Inspector の [theme] フィールドに MapTheme アセットをアサインすると
/// 全ての色・サイズを一括変更できる（null 時はデフォルト色を使用）。
/// MapPanel.prefab として保存しておけば他シーンで再利用可能。
///
/// ─ 生成フロー ────────────────────────────────────────────
/// Awake → BuildMap() でUI子オブジェクトを全生成。
/// Update → ドア/カメラ/モンスターの状態を毎フレーム更新。
/// 各部屋オブジェクトには MapRoomNode コンポーネントがアタッチされ、
/// 侵入者検知時のパルスアニメーションを自律的に処理する。
/// </summary>
[DisallowMultipleComponent]
public class FacilityMapUI : MonoBehaviour
{
    // ─────────────────────────────────────────────────────
    // Inspector
    // ─────────────────────────────────────────────────────
    [Tooltip("色・スタイル設定。NIGHTMARE/Map Theme で作成。null 時はデフォルト色を使用。")]
    [SerializeField] public MapTheme theme;

    [Tooltip("セクション別プレハブ管理。NIGHTMARE/Map Assembly で作成。\n" +
             "uiRoomNodeTemplate が設定されていると各部屋をそのプレハブから生成する。")]
    [SerializeField] public MapAssembly assembly;

    // ─────────────────────────────────────────────────────
    // 静的レイアウトデータ
    // ─────────────────────────────────────────────────────

    // 正規化座標 (X: 0=左 1=右, Y: 0=上 1=下)
    private static readonly Dictionary<FacilityLocation, Vector2> RoomNorm = new()
    {
        { FacilityLocation.Outside_North, new Vector2(0.50f, 0.04f) },
        { FacilityLocation.Outside_East,  new Vector2(0.85f, 0.15f) },
        { FacilityLocation.Outside_West,  new Vector2(0.15f, 0.15f) },
        { FacilityLocation.Outside_Top,   new Vector2(0.50f, 0.27f) },
        { FacilityLocation.Lobby_Main,    new Vector2(0.50f, 0.41f) },
        { FacilityLocation.Lobby_Stairs,  new Vector2(0.50f, 0.53f) },
        { FacilityLocation.B1_Corridor,   new Vector2(0.50f, 0.66f) },
        { FacilityLocation.B1_DoorFront,  new Vector2(0.50f, 0.78f) },
        { FacilityLocation.ManagersRoom,  new Vector2(0.50f, 0.91f) },
    };

    // 各ルームのサイズ (幅, 高さ) px
    private static readonly Dictionary<FacilityLocation, Vector2> RoomSize = new()
    {
        { FacilityLocation.Outside_North, new Vector2(76f, 26f) },
        { FacilityLocation.Outside_East,  new Vector2(52f, 24f) },
        { FacilityLocation.Outside_West,  new Vector2(52f, 24f) },
        { FacilityLocation.Outside_Top,   new Vector2(76f, 26f) },
        { FacilityLocation.Lobby_Main,    new Vector2(84f, 30f) },
        { FacilityLocation.Lobby_Stairs,  new Vector2(84f, 26f) },
        { FacilityLocation.B1_Corridor,   new Vector2(84f, 30f) },
        { FacilityLocation.B1_DoorFront,  new Vector2(84f, 26f) },
        { FacilityLocation.ManagersRoom,  new Vector2(102f, 36f) },
    };

    // カメラ → 担当ロケーション
    private static readonly Dictionary<CameraID, FacilityLocation> CameraLoc = new()
    {
        { CameraID.OUT_N,   FacilityLocation.Outside_North },
        { CameraID.OUT_E,   FacilityLocation.Outside_East  },
        { CameraID.OUT_W,   FacilityLocation.Outside_West  },
        { CameraID.OUT_TOP, FacilityLocation.Outside_Top   },
        { CameraID.IN_1F_A, FacilityLocation.Lobby_Main    },
        { CameraID.IN_1F_B, FacilityLocation.Lobby_Stairs  },
        { CameraID.IN_B1_A, FacilityLocation.B1_Corridor   },
        { CameraID.IN_B1_B, FacilityLocation.B1_DoorFront  },
    };

    // ドア → 遮断する接続 (from→to の中間に表示)
    private static readonly Dictionary<DoorID, (FacilityLocation from, FacilityLocation to)> DoorGate = new()
    {
        { DoorID.Gate,           (FacilityLocation.Outside_North, FacilityLocation.Outside_Top)  },
        { DoorID.Entrance,       (FacilityLocation.Outside_Top,   FacilityLocation.Lobby_Main)   },
        { DoorID.BasementStairs, (FacilityLocation.Lobby_Stairs,  FacilityLocation.B1_Corridor)  },
        { DoorID.B1Corridor,     (FacilityLocation.B1_DoorFront,  FacilityLocation.ManagersRoom) },
    };

    // 接続 (モンスター移動方向 from → to)
    private static readonly (FacilityLocation from, FacilityLocation to)[] Connections =
    {
        (FacilityLocation.Outside_North, FacilityLocation.Outside_Top),
        (FacilityLocation.Outside_East,  FacilityLocation.Outside_Top),
        (FacilityLocation.Outside_West,  FacilityLocation.Outside_Top),
        (FacilityLocation.Outside_Top,   FacilityLocation.Lobby_Main),
        (FacilityLocation.Lobby_Main,    FacilityLocation.Lobby_Stairs),
        (FacilityLocation.Lobby_Stairs,  FacilityLocation.B1_Corridor),
        (FacilityLocation.B1_Corridor,   FacilityLocation.B1_DoorFront),
        (FacilityLocation.B1_DoorFront,  FacilityLocation.ManagersRoom),
    };

    // フロアゾーン (日本語名, normYStart, normYEnd)
    private static readonly (string jp, float yS, float yE)[] FloorZones =
    {
        ("地上", 0.00f, 0.34f),
        ("1F",   0.34f, 0.59f),
        ("B1",   0.59f, 1.00f),
    };

    // ─────────────────────────────────────────────────────
    // レイアウト定数
    // ─────────────────────────────────────────────────────
    private const float LegendH = 106f;   // 下部凡例エリアの高さ
    private const float TitleH  = 18f;    // 上部タイトルバーの高さ

    // ─────────────────────────────────────────────────────
    // ランタイムフィールド
    // ─────────────────────────────────────────────────────
    private float mapW, mapH, contentH;
    private float blinkTimer;
    private float _updateTimer = 0f;
    private const float UpdateInterval = 0.1f; // マップは10fpsで更新すれば十分

    private readonly Dictionary<FacilityLocation, MapRoomNode> roomNodes   = new();
    private readonly Dictionary<DoorID,           Image>       doorBars    = new();
    private readonly Dictionary<CameraID,         Image>       camIcons    = new();
    // ロケーション → モンスタータイプ順のドットリスト
    private readonly Dictionary<FacilityLocation, Image[]>     monsterDots = new();

    // ─────────────────────────────────────────────────────
    // テーマカラー取得 (fallback 付き)
    // ─────────────────────────────────────────────────────
    private Color C_MapBG       => theme ? theme.mapBg         : new Color(0.04f, 0.06f, 0.10f, 0.97f);
    private Color C_ZoneOD      => theme ? theme.zoneOutdoor   : new Color(0.06f, 0.10f, 0.18f, 0.55f);
    private Color C_Zone1F      => theme ? theme.zone1F        : new Color(0.08f, 0.13f, 0.22f, 0.50f);
    private Color C_ZoneB1      => theme ? theme.zoneB1        : new Color(0.04f, 0.07f, 0.13f, 0.62f);
    private Color C_ZoneBorder  => theme ? theme.zoneBorder    : new Color(0.18f, 0.28f, 0.46f, 0.50f);
    private Color C_Line        => theme ? theme.lineColor     : new Color(0.22f, 0.34f, 0.54f, 1f);
    private Color C_Arrow       => theme ? theme.arrowColor    : new Color(0.32f, 0.52f, 0.74f, 1f);
    private float C_LineW       => theme ? theme.lineWidth     : 2f;
    private Color C_RoomBg      => theme ? theme.roomBg        : new Color(0.10f, 0.15f, 0.24f, 1f);
    private Color C_RoomBgYou   => theme ? theme.roomBgYou     : new Color(0.06f, 0.20f, 0.11f, 1f);
    private Color C_RoomBgSide  => theme ? theme.roomBgSide    : new Color(0.08f, 0.12f, 0.20f, 1f);
    private Color C_RoomBorder  => theme ? theme.roomBorder    : new Color(0.28f, 0.50f, 0.76f, 1f);
    private Color C_RoomBdYou   => theme ? theme.roomBorderYou : new Color(0.22f, 0.72f, 0.36f, 1f);
    private Color C_RoomLabel   => theme ? theme.roomLabel     : new Color(0.82f, 0.93f, 1.00f, 1f);
    private Color C_RoomDanger  => theme ? theme.roomDanger    : new Color(0.40f, 0.04f, 0.04f, 1f);
    private Color C_DoorOpen    => theme ? theme.doorOpen      : new Color(0.14f, 0.82f, 0.22f, 1f);
    private Color C_DoorClosed  => theme ? theme.doorClosed    : new Color(0.90f, 0.14f, 0.07f, 1f);
    private Color C_CamActive   => theme ? theme.camActive     : new Color(0.18f, 0.90f, 1.00f, 1f);
    private Color C_CamNormal   => theme ? theme.camNormal     : new Color(0.12f, 0.52f, 0.82f, 1f);
    private Color C_CamDead     => theme ? theme.camDead       : new Color(0.32f, 0.32f, 0.35f, 1f);
    private Color C_Title       => theme ? theme.textTitle     : new Color(0.44f, 0.76f, 1.00f, 1f);
    private Color C_Sub         => theme ? theme.textSub       : new Color(0.38f, 0.50f, 0.70f, 0.85f);
    private Color C_Hint        => theme ? theme.textHint      : new Color(0.28f, 0.36f, 0.50f, 0.80f);
    private Color C_DoorLbl     => theme ? theme.textDoorLabel : new Color(0.85f, 0.82f, 0.44f, 0.90f);
    private Color C_LegText     => theme ? theme.textLegend    : new Color(0.72f, 0.80f, 0.90f, 1.00f);

    private Color MonsterCol(MonsterType t) =>
        theme ? theme.GetMonsterColor(t) : t switch
        {
            MonsterType.Crawler => new Color(1.00f, 0.28f, 0.08f),
            MonsterType.Rusher  => new Color(1.00f, 0.62f, 0.00f),
            MonsterType.Jammer  => new Color(0.68f, 0.16f, 1.00f),
            MonsterType.Lurker  => new Color(0.50f, 0.52f, 0.60f),
            MonsterType.Mimic   => new Color(0.10f, 0.90f, 0.88f),
            MonsterType.Knocker => new Color(1.00f, 0.98f, 0.16f),
            _                   => Color.white
        };

    // ─────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────
    private void Awake()
    {
        var rt   = GetComponent<RectTransform>();
        mapW     = rt.rect.width  > 1f ? rt.rect.width  : 292f;
        mapH     = rt.rect.height > 1f ? rt.rect.height : 622f;
        contentH = mapH - LegendH - TitleH;
        BuildMap();
    }

    private void Update()
    {
        if (!gameObject.activeSelf) return;
        blinkTimer += Time.deltaTime;

        // マップ更新は10fpsで十分（毎フレームSetActive/color変更でCanvasをdirtyにしない）
        _updateTimer += Time.deltaTime;
        if (_updateTimer < UpdateInterval) return;
        _updateTimer = 0f;

        UpdateDoors();
        UpdateCameras();
        UpdateMonsters();
    }

    // ─────────────────────────────────────────────────────
    // ── マップ構築 ────────────────────────────────────────
    // ─────────────────────────────────────────────────────
    private void BuildMap()
    {
        // 全体背景
        Img("MapBG", transform, C_MapBG, V2(0, 0), V2(mapW, mapH), V2(0, 0));

        // ─ タイトルバー ─
        Img("TitleBar", transform,
            new Color(0.07f, 0.11f, 0.19f, 1f),
            V2(0, mapH - TitleH), V2(mapW, TitleH), V2(0, 0));
        Txt("MapTitle", transform, "施 設 見 取 り 図", 11,
            V2(mapW * 0.5f, mapH - TitleH * 0.5f), V2(mapW, TitleH),
            C_Title, TextAnchor.MiddleCenter);

        // ─ フロアゾーンバンド ─
        BuildFloorZones();

        // ─ 接続線 (ルームの下に描画) ─
        foreach (var (from, to) in Connections)
            BuildConnection(from, to);

        // ─ ドアインジケーター ─
        foreach (var (id, pair) in DoorGate)
            BuildDoorIndicator(id, pair.from, pair.to);

        // ─ ルームノード ─
        BuildRoom(FacilityLocation.Outside_North, "北ゲート");
        BuildRoom(FacilityLocation.Outside_East,  "東側");
        BuildRoom(FacilityLocation.Outside_West,  "西側");
        BuildRoom(FacilityLocation.Outside_Top,   "入口前");
        BuildRoom(FacilityLocation.Lobby_Main,    "ロビー");
        BuildRoom(FacilityLocation.Lobby_Stairs,  "階段・EV");
        BuildRoom(FacilityLocation.B1_Corridor,   "B1廊下");
        BuildRoom(FacilityLocation.B1_DoorFront,  "管理人室前");
        BuildRoom(FacilityLocation.ManagersRoom,  "▣  管理人室", isYou: true);

        // ─ カメラアイコン ─
        foreach (var (camId, loc) in CameraLoc)
            BuildCameraIcon(camId, loc);

        // ─ モンスタードット ─
        BuildMonsterDots();

        // ─ 凡例ストリップ ─
        BuildLegend();
    }

    // ─────────────────────────────────────────────────────
    private void BuildFloorZones()
    {
        Color[] cols = { C_ZoneOD, C_Zone1F, C_ZoneB1 };
        for (int i = 0; i < FloorZones.Length; i++)
        {
            var (jp, yS, yE) = FloorZones[i];
            float yBot = LegendH + (1f - yE) * contentH;
            float yH   = (yE - yS) * contentH;

            Img($"Zone_{i}", transform, cols[i], V2(0, yBot), V2(mapW, yH), V2(0, 0));

            // 上境界線（2px でより明確に）
            if (i > 0)
                Img($"ZoneLine_{i}", transform, C_ZoneBorder,
                    V2(0, yBot + yH - 2f), V2(mapW, 2f), V2(0, 0));

            // ゾーンラベル（右端縦配置 + 左端に細い色帯）
            Color zoneAccent = i == 0 ? new Color(0.30f, 0.55f, 0.85f, 0.70f)
                             : i == 1 ? new Color(0.28f, 0.52f, 0.80f, 0.60f)
                                      : new Color(0.18f, 0.32f, 0.58f, 0.80f);
            Img($"ZoneAccent_{i}", transform, zoneAccent,
                V2(0, yBot), V2(3f, yH), V2(0, 0));

            Txt($"ZoneLbl_{i}", transform, jp, 9,
                V2(mapW - 10f, yBot + yH * 0.5f), V2(10f, yH),
                C_Sub, TextAnchor.MiddleCenter);
        }
    }

    // ─────────────────────────────────────────────────────
    private void BuildConnection(FacilityLocation from, FacilityLocation to)
    {
        Vector2 p1    = ToUI(from);
        Vector2 p2    = ToUI(to);
        Vector2 mid   = (p1 + p2) * 0.5f;
        float   dist  = Vector2.Distance(p1, p2);
        float   angle = Mathf.Atan2(p2.y - p1.y, p2.x - p1.x) * Mathf.Rad2Deg;

        // ── メインライン（2.5px で少し太く鮮明に）──
        float lw = Mathf.Max(2.5f, C_LineW);
        var line = Img($"Line_{from}_{to}", transform, C_Line,
            mid, V2(dist, lw), V2(0.5f, 0.5f));
        line.localEulerAngles = V3(0, 0, angle);

        // ── 方向矢印（▶ で視認性向上、60% 地点）──
        Vector2 arrPos = Vector2.Lerp(p1, p2, 0.60f);
        var arr = Txt($"Arr_{from}_{to}", transform, "▶", 9,
            arrPos, V2(16f, 16f), C_Arrow, TextAnchor.MiddleCenter);
        arr.localEulerAngles = V3(0, 0, angle);
    }

    // ─────────────────────────────────────────────────────
    private void BuildDoorIndicator(DoorID id, FacilityLocation from, FacilityLocation to)
    {
        Vector2 p1    = ToUI(from);
        Vector2 p2    = ToUI(to);
        Vector2 mid   = (p1 + p2) * 0.5f;
        float   angle = Mathf.Atan2(p2.y - p1.y, p2.x - p1.x) * Mathf.Rad2Deg;

        // ── シャッターフレーム（外枠 - 暗い背景）──
        var outer = Img($"DoorOuter_{id}", transform, new Color(0.10f, 0.14f, 0.22f),
            mid, V2(28f, 12f), V2(0.5f, 0.5f));
        outer.localEulerAngles = V3(0, 0, angle);

        // ── シャッターバー（メイン - 開閉で色変化）──
        var bar = Img($"DoorBar_{id}", transform, C_DoorOpen,
            mid, V2(22f, 8f), V2(0.5f, 0.5f));
        bar.localEulerAngles = V3(0, 0, angle);
        doorBars[id] = bar.GetComponent<Image>();

        // ── シャッタースラット（横線 × 2 でシャッター感を演出）──
        // バーと同じ回転を持つ細い暗い横線を上下 2 本追加
        float rad = angle * Mathf.Deg2Rad;
        Vector2 perpDir = new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad));
        for (int s = -1; s <= 1; s += 2)
        {
            var slat = Img($"DoorSlat_{id}_{s}", transform,
                new Color(0.08f, 0.11f, 0.18f, 0.80f),
                mid + perpDir * (s * 2.2f), V2(22f, 1.5f), V2(0.5f, 0.5f));
            slat.localEulerAngles = V3(0, 0, angle);
        }

        // ── ラベル ──
        bool isVert = Mathf.Abs(Mathf.Sin(rad)) > 0.5f;
        Vector2 lblOff = isVert ? V2(22f, 0f) : V2(0f, 13f);
        string lbl = id switch
        {
            DoorID.Gate           => "Gate",
            DoorID.Entrance       => "入口",
            DoorID.BasementStairs => "地下",
            DoorID.B1Corridor     => "B1",
            _                     => id.ToString()
        };
        Txt($"DoorLbl_{id}", transform, lbl, 8,
            mid + lblOff, V2(32f, 14f), C_DoorLbl, TextAnchor.MiddleCenter);
    }

    // ─────────────────────────────────────────────────────
    private void BuildRoom(FacilityLocation loc, string label, bool isYou = false)
    {
        Vector2 pos    = ToUI(loc);
        Vector2 sz     = RoomSize[loc];
        bool    isSide = loc == FacilityLocation.Outside_East
                      || loc == FacilityLocation.Outside_West;

        Color bgCol = isYou  ? C_RoomBgYou
                    : isSide ? C_RoomBgSide
                    : C_RoomBg;
        Color bdCol = isYou ? C_RoomBdYou : C_RoomBorder;

        // ── テンプレートプレハブからインスタンス化（設定されている場合）──
        if (assembly?.uiRoomNodeTemplate != null)
        {
            var go  = Object.Instantiate(assembly.uiRoomNodeTemplate, transform);
            go.name = $"Room_{loc}";
            var rt  = go.GetComponent<RectTransform>();
            if (rt)
            {
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.pivot     = V2(0.5f, 0.5f);
                rt.anchoredPosition = pos;
                rt.sizeDelta        = sz;
            }
            var bgImg = go.GetComponent<Image>();
            if (bgImg) bgImg.color = bgCol;
            var ol = go.GetComponent<Outline>();
            if (ol) { ol.effectColor = bdCol; ol.effectDistance = V2(1.5f, 1.5f); }
            var lbl = go.GetComponentInChildren<Text>();
            if (lbl) { lbl.text = label; lbl.fontSize = isYou ? 11 : isSide ? 8 : 10; }
            var node = go.GetComponent<MapRoomNode>() ?? go.AddComponent<MapRoomNode>();
            node.Init(loc, isYou, bgImg, ol, bgCol, C_RoomDanger, bdCol);
            roomNodes[loc] = node;
            return;
        }

        // ── フォールバック: コードで直接生成 ──────────────────────────
        var panel   = Img($"Room_{loc}", transform, bgCol, pos, sz, V2(0.5f, 0.5f));
        var bgImage = panel.GetComponent<Image>();

        var outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor    = bdCol;
        outline.effectDistance = V2(2f, 2f);  // 少し太く

        int fontSize = isYou ? 11 : isSide ? 8 : 10;

        // ラベル（通常どおり panel の子）
        Txt($"RoomLbl_{loc}", panel, label, fontSize,
            Vector2.zero, sz - V2(4f, 0f), C_RoomLabel, TextAnchor.MiddleCenter);

        // 左端カラーアクセントバー（transform の子として canvas 座標で配置）
        Color accentCol = isYou  ? new Color(0.22f, 0.82f, 0.36f, 0.90f)
                        : isSide ? new Color(0.26f, 0.44f, 0.70f, 0.70f)
                                 : new Color(0.22f, 0.40f, 0.65f, 0.60f);
        Img($"RoomAccent_{loc}", transform, accentCol,
            pos + V2(-sz.x * 0.5f + 1.5f, 0f), V2(3f, sz.y - 2f), V2(0.5f, 0.5f));

        var roomNode = panel.gameObject.AddComponent<MapRoomNode>();
        roomNode.Init(loc, isYou, bgImage, outline, bgCol, C_RoomDanger, bdCol);
        roomNodes[loc] = roomNode;
    }

    // ─────────────────────────────────────────────────────
    private void BuildCameraIcon(CameraID camId, FacilityLocation loc)
    {
        Vector2 center = ToUI(loc);
        Vector2 sz     = RoomSize[loc];

        // 部屋の左上コーナーに配置
        Vector2 iconPos = center + V2(-sz.x * 0.5f + 5f, sz.y * 0.5f - 8f);

        // カメラアイコン本体（◉ 風の2重四角）
        // 外枠（少し大きめの暗い四角）
        var outerFrame = Img($"CamOuter_{camId}", transform, new Color(0.08f, 0.12f, 0.20f),
            iconPos, V2(10f, 8f), V2(0.5f, 0.5f));

        // 内側（実際の状態色を持つ四角）
        var icon = Img($"Cam_{camId}", transform, C_CamNormal,
            iconPos, V2(7f, 5f), V2(0.5f, 0.5f));
        camIcons[camId] = icon.GetComponent<Image>();

        // レンズドット（中央の小さな四角）
        Img($"CamLens_{camId}", transform, new Color(0.02f, 0.04f, 0.08f),
            iconPos, V2(3f, 3f), V2(0.5f, 0.5f));

        // カメラ ID ラベル（アイコン左側）
        string shortId = camId.ToString().Replace("OUT_", "").Replace("IN_", "");
        Txt($"CamTxt_{camId}", transform, shortId, 7,
            iconPos + V2(-3f, 0f), V2(28f, 10f), C_CamNormal, TextAnchor.MiddleRight);
    }

    // ─────────────────────────────────────────────────────
    private void BuildMonsterDots()
    {
        // 各ロケーションに6タイプ分のドットを右上コーナーに縦積み配置
        foreach (FacilityLocation loc in System.Enum.GetValues(typeof(FacilityLocation)))
        {
            if (loc == FacilityLocation.ManagersRoom) continue;

            Vector2 center = ToUI(loc);
            Vector2 sz     = RoomSize[loc];

            float dotX      = center.x + sz.x * 0.5f - 7f;
            float dotYStart = center.y + sz.y * 0.5f - 7f;
            float dotStep   = 6.5f;

            var types = System.Enum.GetValues(typeof(MonsterType));
            var dots  = new Image[types.Length];

            foreach (MonsterType mt in types)
            {
                int   i    = (int)mt;
                float dotY = dotYStart - i * dotStep;
                if (dotY < center.y - sz.y * 0.5f + 4f) break;  // ルーム外なら省略

                var dot  = Img($"MDot_{loc}_{mt}", transform, MonsterCol(mt),
                    V2(dotX, dotY), V2(5f, 5f), V2(0.5f, 0.5f));
                var img  = dot.GetComponent<Image>();
                img.gameObject.SetActive(false);
                dots[i] = img;
            }
            monsterDots[loc] = dots;
        }
    }

    // ─────────────────────────────────────────────────────
    private void BuildLegend()
    {
        // 背景
        Img("LegBG", transform,
            new Color(0.05f, 0.08f, 0.14f, 0.97f),
            V2(0, 0), V2(mapW, LegendH), V2(0, 0));

        // 上境界線
        Img("LegTopLine", transform, C_ZoneBorder,
            V2(0, LegendH - 1f), V2(mapW, 1f), V2(0, 0));

        // タイトル
        Txt("LegTitle", transform, "凡  例", 8,
            V2(mapW * 0.5f, LegendH - 7f), V2(mapW, 12f),
            C_Sub, TextAnchor.MiddleCenter);

        // 中央縦仕切り
        Img("LegDivider", transform,
            new Color(0.18f, 0.28f, 0.42f, 0.5f),
            V2(mapW * 0.5f - 1f, 14f), V2(1f, LegendH - 22f), V2(0, 0));

        // 行 Y 座標 (下から積み上げ)
        float[] rows = { 72f, 59f, 46f, 33f, 20f };  // 5 rows

        // ── 左カラム: ドア / カメラ ──────────────
        LegEntry("DO", "ドア  開",  C_DoorOpen,   5, 6f, 14f, rows[0], 5, 8);
        LegEntry("DC", "ドア  閉",  C_DoorClosed, 5, 6f, 14f, rows[1], 5, 8);
        LegEntry("CA", "Cam  選択", C_CamActive,  7, 6f, 15f, rows[2], 7, 8);
        LegEntry("CN", "Cam  通常", C_CamNormal,  7, 6f, 15f, rows[3], 7, 8);
        LegEntry("CD", "Cam  故障", C_CamDead,    7, 6f, 15f, rows[4], 7, 8);

        // ── 右カラム: モンスター (3行×2列) ────────
        (MonsterType mt, string nm)[] mons =
        {
            (MonsterType.Crawler, "Crawler"),
            (MonsterType.Rusher,  "Rusher"),
            (MonsterType.Jammer,  "Jammer"),
            (MonsterType.Lurker,  "Lurker"),
            (MonsterType.Mimic,   "Mimic"),
            (MonsterType.Knocker, "Knocker"),
        };

        // 3行2列: 左サブ列=0-2, 右サブ列=3-5
        float rColA = mapW * 0.5f + 4f;   // サブ列A ドット X
        float rColB = mapW * 0.5f + 72f;  // サブ列B ドット X
        for (int i = 0; i < 6; i++)
        {
            float dx  = i < 3 ? rColA   : rColB;
            float dtx = i < 3 ? dx + 9f : dx + 9f;
            float y   = rows[i < 3 ? i : i - 3];
            LegEntry($"M{i}", mons[i].nm, MonsterCol(mons[i].mt),
                7, dx, dtx, y, 7, 8);
        }

        // ヒント (最下行, 全幅センター)
        Txt("MapHint", transform, "[M] マップ切替", 8,
            V2(mapW * 0.5f, 7f), V2(mapW, 12f), C_Hint, TextAnchor.MiddleCenter);
    }

    private void LegEntry(string key, string label, Color col,
        float dotSize, float dotX, float txtX, float y, float dh, int fs)
    {
        Img($"LDot_{key}", transform, col,
            V2(dotX, y), V2(dotSize, dh), V2(0f, 0.5f));
        Txt($"LTxt_{key}", transform, label, fs,
            V2(txtX, y), V2(60f, 12f), C_LegText, TextAnchor.MiddleLeft);
    }

    // ─────────────────────────────────────────────────────
    // ── ランタイム更新 ─────────────────────────────────────
    // ─────────────────────────────────────────────────────
    private void UpdateDoors()
    {
        if (DoorManager.Instance == null) return;
        foreach (var (id, bar) in doorBars)
        {
            bool closed = DoorManager.Instance.IsClosed(id);
            bar.color = closed ? C_DoorClosed : C_DoorOpen;
        }
    }

    private void UpdateCameras()
    {
        if (SecurityCameraSystem.Instance == null) return;
        foreach (var (camId, icon) in camIcons)
        {
            bool dead   = SecurityCameraSystem.Instance.IsCameraDead(camId);
            bool active = SecurityCameraSystem.Instance.ActiveExternal == camId
                       || SecurityCameraSystem.Instance.ActiveInternal == camId;
            icon.color = dead ? C_CamDead : active ? C_CamActive : C_CamNormal;
        }
    }

    private void UpdateMonsters()
    {
        if (MonsterManager.Instance == null) return;

        // 全ドット非表示 + 危険フラグリセット
        foreach (var dots in monsterDots.Values)
            foreach (var d in dots) { if (d) d.gameObject.SetActive(false); }
        foreach (var node in roomNodes.Values)
            node.SetDanger(false);

        bool blink = Mathf.Sin(blinkTimer * 5f) > 0f;

        foreach (var monster in MonsterManager.Instance.ActiveMonsters)
        {
            var loc = monster.CurrentLocation;
            if (loc == FacilityLocation.ManagersRoom) continue;

            // 部屋の危険パルス
            if (roomNodes.TryGetValue(loc, out var node))
                node.SetDanger(true);

            // モンスタードット表示
            if (!monsterDots.TryGetValue(loc, out var dotArr)) continue;
            int idx = (int)monster.MonsterType;
            if (idx < 0 || idx >= dotArr.Length || dotArr[idx] == null) continue;

            var dot = dotArr[idx];
            if (monster.MonsterType == MonsterType.Lurker && !monster.IsVisible)
            {
                // Lurker 不可視: 薄いグレーで常時表示
                dot.gameObject.SetActive(true);
                dot.color = new Color(0.50f, 0.52f, 0.60f, 0.30f);
            }
            else
            {
                dot.gameObject.SetActive(blink);
                dot.color = MonsterCol(monster.MonsterType);
            }
        }
    }

    // ─────────────────────────────────────────────────────
    // ── 座標変換 ───────────────────────────────────────────
    // ─────────────────────────────────────────────────────

    /// <summary>
    /// 正規化座標 (X:0-1=左右, Y:0-1=上下) → マップ内UI座標 (左下=原点)。
    /// 凡例エリア(LegendH)とタイトルバー(TitleH)を除いたコンテンツ領域にマッピング。
    /// </summary>
    private Vector2 ToUI(Vector2 norm)
        => new Vector2(norm.x * mapW, LegendH + (1f - norm.y) * contentH);

    private Vector2 ToUI(FacilityLocation loc) => ToUI(RoomNorm[loc]);

    // ─────────────────────────────────────────────────────
    // ── UI生成ユーティリティ ───────────────────────────────
    // ─────────────────────────────────────────────────────
    private RectTransform Img(string name, Transform parent, Color col,
        Vector2 pos, Vector2 size, Vector2 pivot)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot          = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta      = size;
        go.GetComponent<Image>().color = col;
        return rt;
    }

    private RectTransform Txt(string name, Transform parent, string text, int fontSize,
        Vector2 pos, Vector2 size, Color col, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot          = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta      = size;
        var t = go.GetComponent<Text>();
        t.text      = text;
        t.fontSize  = fontSize;
        t.color     = col;
        t.alignment = align;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return rt;
    }

    // shorthand
    private static Vector2 V2(float x, float y) => new Vector2(x, y);
    private static Vector3 V3(float x, float y, float z) => new Vector3(x, y, z);
}
