using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

// 施設マップをリアルタイムで表示する
// Mキーまたはマップボタンでトグル
// このコンポーネントはAwakeで自分のUI子要素を全て生成する（自己完結型）
public class FacilityMapUI : MonoBehaviour
{
    // マップ内の各エリアの位置 (正規化 0-1, 左上が0,0)
    private static readonly Dictionary<FacilityLocation, Vector2> RoomPositions = new()
    {
        { FacilityLocation.Outside_North, new Vector2(0.50f, 0.04f) },
        { FacilityLocation.Outside_East,  new Vector2(0.85f, 0.13f) },
        { FacilityLocation.Outside_West,  new Vector2(0.15f, 0.13f) },
        { FacilityLocation.Outside_Top,   new Vector2(0.50f, 0.22f) },
        { FacilityLocation.Lobby_Main,    new Vector2(0.50f, 0.38f) },
        { FacilityLocation.Lobby_Stairs,  new Vector2(0.50f, 0.50f) },
        { FacilityLocation.B1_Corridor,   new Vector2(0.50f, 0.64f) },
        { FacilityLocation.B1_DoorFront,  new Vector2(0.50f, 0.76f) },
        { FacilityLocation.ManagersRoom,  new Vector2(0.50f, 0.90f) },
    };

    // カメラがカバーするエリア
    private static readonly Dictionary<CameraID, FacilityLocation> CameraLocations = new()
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

    // ドアが位置する接続間（From → To の中間に表示）
    private static readonly Dictionary<DoorID, (FacilityLocation from, FacilityLocation to)> DoorConnections = new()
    {
        { DoorID.Gate,           (FacilityLocation.Outside_North, FacilityLocation.Outside_Top)  },
        { DoorID.Entrance,       (FacilityLocation.Outside_Top,   FacilityLocation.Lobby_Main)   },
        { DoorID.BasementStairs, (FacilityLocation.Lobby_Stairs,  FacilityLocation.B1_Corridor)  },
        { DoorID.B1Corridor,     (FacilityLocation.B1_DoorFront,  FacilityLocation.ManagersRoom) },
    };

    // 色定義
    private static readonly Color ColRoom    = new Color(0.12f, 0.16f, 0.22f, 1f);
    private static readonly Color ColRoomYou = new Color(0.10f, 0.25f, 0.15f, 1f);
    private static readonly Color ColBorder  = new Color(0.3f,  0.5f,  0.7f,  1f);
    private static readonly Color ColDoorOpen   = new Color(0.2f, 0.7f, 0.2f, 1f);
    private static readonly Color ColDoorClosed = new Color(0.8f, 0.2f, 0.1f, 1f);
    private static readonly Color ColCamera  = new Color(0.2f, 0.8f, 1.0f, 1f);
    private static readonly Color ColCamDead = new Color(0.4f, 0.4f, 0.4f, 1f);
    private static readonly Color ColMonster = new Color(1.0f, 0.2f, 0.1f, 1f);
    private static readonly Color ColMapBG   = new Color(0.04f, 0.06f, 0.10f, 0.96f);
    private static readonly Color ColLine    = new Color(0.2f, 0.3f, 0.45f, 1f);
    private static readonly Color ColFloor1F = new Color(0.08f, 0.12f, 0.20f, 0.6f);
    private static readonly Color ColFloorB1 = new Color(0.06f, 0.10f, 0.16f, 0.6f);

    private RectTransform mapRoot;
    private readonly Dictionary<FacilityLocation, RectTransform> roomPanels = new();
    private readonly Dictionary<DoorID, Image> doorIndicators = new();
    private readonly Dictionary<CameraID, Image> cameraIcons = new();
    private readonly Dictionary<FacilityLocation, Image> monsterMarkers = new();
    private float mapW, mapH;
    private float blinkTimer;

    private void Awake()
    {
        var rt = GetComponent<RectTransform>();
        mapW = rt.rect.width  == 0 ? 280f : rt.rect.width;
        mapH = rt.rect.height == 0 ? 600f : rt.rect.height;

        BuildMap();
    }

    private void Update()
    {
        if (!gameObject.activeSelf) return;
        blinkTimer += Time.deltaTime;
        UpdateDoors();
        UpdateCameras();
        UpdateMonsters();
    }

    // ===== マップ構築 =====
    private void BuildMap()
    {
        // 背景
        CreateImage("MapBG", transform, ColMapBG, Vector2.zero, new Vector2(mapW, mapH), Vector2.zero);

        // タイトル
        CreateLabel("MapTitle", transform, "施設 見取り図", 13,
            new Vector2(mapW * 0.5f, mapH - 12f), new Vector2(mapW, 20f), new Color(0.5f, 0.8f, 1f));

        // フロア帯（地上 / 1F / B1）
        DrawFloorBand("地上エリア",  0.00f, 0.33f, new Color(0.08f, 0.14f, 0.22f, 0.5f));
        DrawFloorBand("1F ロビー",   0.33f, 0.58f, ColFloor1F);
        DrawFloorBand("B1 地下",     0.58f, 1.00f, ColFloorB1);

        // ルームをつなぐ接続線
        DrawConnection(FacilityLocation.Outside_North, FacilityLocation.Outside_Top);
        DrawConnection(FacilityLocation.Outside_East,  FacilityLocation.Outside_Top);
        DrawConnection(FacilityLocation.Outside_West,  FacilityLocation.Outside_Top);
        DrawConnection(FacilityLocation.Outside_Top,   FacilityLocation.Lobby_Main);
        DrawConnection(FacilityLocation.Lobby_Main,    FacilityLocation.Lobby_Stairs);
        DrawConnection(FacilityLocation.Lobby_Stairs,  FacilityLocation.B1_Corridor);
        DrawConnection(FacilityLocation.B1_Corridor,   FacilityLocation.B1_DoorFront);
        DrawConnection(FacilityLocation.B1_DoorFront,  FacilityLocation.ManagersRoom);

        // ルームパネル
        CreateRoom(FacilityLocation.Outside_North, "北ゲート",    52, 22);
        CreateRoom(FacilityLocation.Outside_East,  "東側",        40, 18);
        CreateRoom(FacilityLocation.Outside_West,  "西側",        40, 18);
        CreateRoom(FacilityLocation.Outside_Top,   "入口前",      56, 22);
        CreateRoom(FacilityLocation.Lobby_Main,    "ロビー",      68, 26);
        CreateRoom(FacilityLocation.Lobby_Stairs,  "階段・EV",    68, 22);
        CreateRoom(FacilityLocation.B1_Corridor,   "B1廊下",      68, 26);
        CreateRoom(FacilityLocation.B1_DoorFront,  "管理人室前",  68, 22);
        CreateRoom(FacilityLocation.ManagersRoom,  "▣ 管理人室", 80, 30, ColRoomYou);

        // ドアインジケーター
        CreateDoorIndicator(DoorID.Gate,           "外壁ゲート");
        CreateDoorIndicator(DoorID.Entrance,       "入口ドア");
        CreateDoorIndicator(DoorID.BasementStairs, "地下階段");
        CreateDoorIndicator(DoorID.B1Corridor,     "B1廊下ドア");

        // カメライコン
        foreach (var kvp in CameraLocations)
            CreateCameraIcon(kvp.Key, kvp.Value);

        // モンスターマーカー（各ロケーションに1つ用意）
        foreach (FacilityLocation loc in System.Enum.GetValues(typeof(FacilityLocation)))
        {
            if (loc == FacilityLocation.ManagersRoom) continue;
            var marker = CreateImage($"Marker_{loc}", transform, ColMonster,
                NormToMap(RoomPositions[loc]) + new Vector2(22f, 0f), new Vector2(10f, 10f), new Vector2(0.5f, 0.5f));
            marker.GetComponent<Image>().sprite = null;
            var img = marker.GetComponent<Image>();
            monsterMarkers[loc] = img;
            img.gameObject.SetActive(false);
        }

        // 凡例
        DrawLegend();
    }

    private void DrawFloorBand(string label, float normYStart, float normYEnd, Color col)
    {
        float yStart = mapH * (1f - normYEnd);
        float height = mapH * (normYEnd - normYStart);
        var band = CreateImage($"Band_{label}", transform, col,
            new Vector2(0, yStart), new Vector2(mapW, height), Vector2.zero);
        CreateLabel($"BandLabel_{label}", transform, label, 9,
            new Vector2(6f, yStart + height - 10f), new Vector2(80f, 14f), new Color(0.5f, 0.7f, 1f, 0.7f), TextAnchor.UpperLeft);
    }

    private void DrawConnection(FacilityLocation from, FacilityLocation to)
    {
        Vector2 p1 = NormToMap(RoomPositions[from]);
        Vector2 p2 = NormToMap(RoomPositions[to]);
        Vector2 mid = (p1 + p2) * 0.5f;
        float dist = Vector2.Distance(p1, p2);
        float angle = Mathf.Atan2(p2.y - p1.y, p2.x - p1.x) * Mathf.Rad2Deg;

        var line = CreateImage($"Line_{from}_{to}", transform, ColLine,
            mid, new Vector2(dist, 2f), new Vector2(0.5f, 0.5f));
        line.localEulerAngles = new Vector3(0, 0, angle);
    }

    private void CreateRoom(FacilityLocation loc, string label, float w, float h, Color? overrideCol = null)
    {
        Vector2 pos = NormToMap(RoomPositions[loc]);
        Color col = overrideCol ?? ColRoom;

        var panel = CreateImage($"Room_{loc}", transform, col, pos, new Vector2(w, h), new Vector2(0.5f, 0.5f));

        // 枠線
        var border = CreateImage($"Border_{loc}", panel, Color.clear, Vector2.zero, new Vector2(w, h), new Vector2(0.5f, 0.5f));
        border.GetComponent<Image>().color = Color.clear;
        var outline = border.gameObject.AddComponent<Outline>();
        outline.effectColor = ColBorder;
        outline.effectDistance = new Vector2(1.5f, 1.5f);

        // ラベル
        CreateLabel($"RoomLabel_{loc}", panel, label, 10,
            Vector2.zero, new Vector2(w - 4f, h), new Color(0.85f, 0.95f, 1f), TextAnchor.MiddleCenter);

        roomPanels[loc] = panel;
    }

    private void CreateDoorIndicator(DoorID id, string label)
    {
        var (from, to) = DoorConnections[id];
        Vector2 p1 = NormToMap(RoomPositions[from]);
        Vector2 p2 = NormToMap(RoomPositions[to]);
        Vector2 mid = (p1 + p2) * 0.5f;

        var door = CreateImage($"Door_{id}", transform, ColDoorOpen, mid, new Vector2(16f, 6f), new Vector2(0.5f, 0.5f));
        CreateLabel($"DoorLabel_{id}", transform, label, 8,
            mid + new Vector2(20f, 0f), new Vector2(50f, 14f), new Color(0.8f, 0.8f, 0.5f), TextAnchor.MiddleLeft);

        doorIndicators[id] = door.GetComponent<Image>();
    }

    private void CreateCameraIcon(CameraID camId, FacilityLocation loc)
    {
        Vector2 pos = NormToMap(RoomPositions[loc]) + new Vector2(-22f, 8f);
        var icon = CreateImage($"Cam_{camId}", transform, ColCamera, pos, new Vector2(8f, 8f), new Vector2(0.5f, 0.5f));
        CreateLabel($"CamLabel_{camId}", transform, camId.ToString().Replace("_", "-"), 7,
            pos + new Vector2(0f, -10f), new Vector2(50f, 12f), new Color(0.3f, 0.8f, 1f, 0.8f), TextAnchor.UpperCenter);
        cameraIcons[camId] = icon.GetComponent<Image>();
    }

    private void DrawLegend()
    {
        float x = 6f;
        float y = 55f;
        CreateImage("Leg_Door_Open",   transform, ColDoorOpen,   new Vector2(x, y),      new Vector2(10f, 5f), Vector2.zero);
        CreateLabel("Leg_Door_OpenT",  transform, "ドア開",  8, new Vector2(x + 14f, y), new Vector2(40f, 12f), Color.white, TextAnchor.MiddleLeft);
        CreateImage("Leg_Door_Closed", transform, ColDoorClosed, new Vector2(x, y - 14f), new Vector2(10f, 5f), Vector2.zero);
        CreateLabel("Leg_Door_ClosedT",transform, "ドア閉",  8, new Vector2(x + 14f, y - 14f), new Vector2(40f, 12f), Color.white, TextAnchor.MiddleLeft);
        CreateImage("Leg_Cam",   transform, ColCamera,   new Vector2(x, y - 28f), new Vector2(8f, 8f), Vector2.zero);
        CreateLabel("Leg_CamT",  transform, "カメラ", 8, new Vector2(x + 14f, y - 28f), new Vector2(40f, 12f), Color.white, TextAnchor.MiddleLeft);
        CreateImage("Leg_Mon",   transform, ColMonster,  new Vector2(x, y - 42f), new Vector2(8f, 8f), Vector2.zero);
        CreateLabel("Leg_MonT",  transform, "侵入者", 8, new Vector2(x + 14f, y - 42f), new Vector2(40f, 12f), Color.white, TextAnchor.MiddleLeft);

        CreateLabel("MapHint", transform, "[M] マップ切替", 8,
            new Vector2(mapW * 0.5f, 8f), new Vector2(mapW, 14f), new Color(0.5f, 0.5f, 0.5f), TextAnchor.MiddleCenter);
    }

    // ===== 更新 =====
    private void UpdateDoors()
    {
        if (DoorManager.Instance == null) return;
        foreach (var kvp in doorIndicators)
        {
            bool closed = DoorManager.Instance.IsClosed(kvp.Key);
            kvp.Value.color = closed ? ColDoorClosed : ColDoorOpen;
        }
    }

    private void UpdateCameras()
    {
        if (SecurityCameraSystem.Instance == null) return;
        foreach (var kvp in cameraIcons)
        {
            bool dead = SecurityCameraSystem.Instance.IsCameraDead(kvp.Key);
            bool active = SecurityCameraSystem.Instance.ActiveExternal == kvp.Key
                       || SecurityCameraSystem.Instance.ActiveInternal == kvp.Key;
            kvp.Value.color = dead ? ColCamDead : active ? Color.white : ColCamera;
        }
    }

    private void UpdateMonsters()
    {
        if (MonsterManager.Instance == null) return;

        // 全マーカーを一旦非表示
        foreach (var m in monsterMarkers.Values) m.gameObject.SetActive(false);

        bool blink = Mathf.Sin(blinkTimer * 4f) > 0f;

        foreach (var monster in MonsterManager.Instance.ActiveMonsters)
        {
            var loc = monster.CurrentLocation;
            if (loc == FacilityLocation.ManagersRoom) continue;
            if (!monsterMarkers.TryGetValue(loc, out var marker)) continue;

            marker.gameObject.SetActive(blink);

            // モンスタータイプごとに色を変える
            marker.color = monster.MonsterType switch
            {
                MonsterType.Crawler => new Color(1.0f, 0.3f, 0.1f),
                MonsterType.Rusher  => new Color(1.0f, 0.6f, 0.0f),
                MonsterType.Jammer  => new Color(0.6f, 0.2f, 1.0f),
                MonsterType.Lurker  => monster.IsVisible ? new Color(0.5f, 0.5f, 0.5f) : Color.clear,
                MonsterType.Mimic   => new Color(0.2f, 0.8f, 0.8f),
                MonsterType.Knocker => new Color(1.0f, 1.0f, 0.2f),
                _ => ColMonster
            };
        }
    }

    // ===== ユーティリティ =====
    private Vector2 NormToMap(Vector2 norm)
    {
        // normは (0=左, 1=右), (0=上, 1=下)
        // Unity UI座標は左下が(0,0)
        return new Vector2(norm.x * mapW, (1f - norm.y) * mapH);
    }

    private RectTransform CreateImage(string name, Transform parent, Color col,
        Vector2 anchoredPos, Vector2 size, Vector2 pivot)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        go.GetComponent<Image>().color = col;
        return rt;
    }

    private void CreateLabel(string name, Transform parent, string text, int fontSize,
        Vector2 pos, Vector2 size, Color col, TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var t = go.GetComponent<Text>();
        t.text = text;
        t.fontSize = fontSize;
        t.color = col;
        t.alignment = anchor;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
