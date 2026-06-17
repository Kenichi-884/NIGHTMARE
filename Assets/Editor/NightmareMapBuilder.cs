// NIGHTMARE > Build Temp 3D Map
// Z-fighting 対策: 各フロアは独立したXZエリアを持ち、表面が重複しない
// テクスチャ: Assets/NightmareAssets/Materials/TempMap/Textures/ に保存

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.IO;

public static class NightmareMapBuilder
{
    // ─── 座標定義 ─────────────────────────────────────────────────
    // 1F  床表面: Y =  0.0
    // B1  床表面: Y = -4.0
    // 1F  天井:   Y =  3.0 (床から3m)
    // B1  天井:   Y = -1.3 (B1床から2.7m)
    const float B1Y  = -4.0f;
    const float B1CH =  2.7f;
    const float F1H  =  3.0f;

    const string MatDir = "Assets/NightmareAssets/Materials/TempMap";
    const string TexDir = "Assets/NightmareAssets/Materials/TempMap/Textures";

    static readonly Dictionary<string, Material>  Mats = new();
    static readonly Dictionary<string, Texture2D> Texs = new();
    static Shader _shader;

    // ═══════════════════════════════════════════════════════════════
    //  エントリポイント
    // ═══════════════════════════════════════════════════════════════
    [MenuItem("NIGHTMARE/Build Temp 3D Map", priority = 5)]
    public static void BuildMap()
    {
        var old = GameObject.Find("[NIGHTMARE Map]");
        if (old != null)
        {
            if (!EditorUtility.DisplayDialog("NIGHTMARE Map Builder",
                "既存の [NIGHTMARE Map] を削除して再生成しますか？", "再生成", "キャンセル"))
                return;
            Object.DestroyImmediate(old);
        }

        Directory.CreateDirectory(MatDir);
        Directory.CreateDirectory(TexDir);
        AssetDatabase.Refresh();

        _shader = null;
        Texs.Clear();
        Mats.Clear();

        GenTextures();
        InitMaterials();

        var root = new GameObject("[NIGHTMARE Map]");

        BuildExterior(root);
        Build1F(root);
        BuildStaircase(root);
        BuildB1(root);
        BuildDoors(root);
        BuildLights(root);
        BuildProps(root);
        BuildPathArrows(root);
        BuildRoof(root);
        BuildSignage(root);
        UpdateSceneCamPositions();
        BuildCCTVMounts(root);

        AssetDatabase.SaveAssets();
        MarkDirty(root);
        Selection.activeGameObject = root;
        SceneView.lastActiveSceneView?.FrameSelected();
        Debug.Log("[NIGHTMARE] 仮3Dマップ生成完了！");
    }

    // ═══════════════════════════════════════════════════════════════
    //  テクスチャ生成
    // ═══════════════════════════════════════════════════════════════
    static void GenTextures()
    {
        SaveTex("concrete",  256, 256, (p, w, h) =>
        {
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float n = ((x * 7 + y * 13) % 23) / 23f * 0.06f - 0.03f;
                float g = 0.40f + n;
                bool grid = (x % 64) < 2 || (y % 64) < 2;
                if (grid) g -= 0.07f;
                p[y * w + x] = new Color(g, g, g + 0.004f);
            }
        });

        SaveTex("b1_floor", 256, 256, (p, w, h) =>
        {
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float n = ((x * 11 + y * 7) % 19) / 19f * 0.04f;
                float b = 0.18f + n;
                bool grid = (x % 48) < 2 || (y % 48) < 2;
                if (grid) b -= 0.05f;
                p[y * w + x] = new Color(b * 0.82f, b * 0.88f, b * 1.2f);
            }
        });

        SaveTex("pavement", 256, 256, (p, w, h) =>
        {
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float n = ((x * 5 + y * 17) % 31) / 31f * 0.04f;
                float g = 0.28f + n;
                bool joint = (x % 32) < 2 || (y % 32) < 2;
                if (joint) g -= 0.06f;
                p[y * w + x] = new Color(g, g - 0.005f, g - 0.012f);
            }
        });

        SaveTex("wall_dark", 256, 256, (p, w, h) =>
        {
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float n = ((x * 3 + y * 19) % 29) / 29f * 0.025f;
                float g = 0.11f + n;
                bool v = (x % 80) < 3;
                bool h2 = (y % 80) < 2;
                if (v || h2) g -= 0.02f;
                p[y * w + x] = new Color(g, g, g + 0.006f);
            }
        });

        SaveTex("metal", 256, 256, (p, w, h) =>
        {
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float band = Mathf.Sin(y * 0.25f) * 0.015f;
                float g = 0.30f + band;
                bool seam = (x % 96) < 2;
                if (seam) g -= 0.05f;
                p[y * w + x] = new Color(g, g, g + 0.01f);
            }
        });

        SaveTex("mgr_floor", 256, 256, (p, w, h) =>
        {
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float n = ((x * 9 + y * 11) % 17) / 17f * 0.04f;
                float r = 0.28f + n;
                // 斜め警告ストライプ（コーナー付近）
                bool stripe = ((x + y) % 32) < 8 && (x < 32 || y < 32 || x > w - 32 || y > h - 32);
                if (stripe) { r += 0.15f; p[y * w + x] = new Color(r + 0.25f, r * 0.6f, 0); continue; }
                bool grid = (x % 64) < 2 || (y % 64) < 2;
                if (grid) r -= 0.05f;
                p[y * w + x] = new Color(r, r * 0.18f, r * 0.18f);
            }
        });

        AssetDatabase.Refresh();
        // Texture2D をキャッシュに再ロード
        foreach (var key in new[] { "concrete","b1_floor","pavement","wall_dark","metal","mgr_floor" })
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{key}.png");
            if (t != null) Texs[key] = t;
        }
    }

    static void SaveTex(string key, int w, int h, System.Action<Color[], int, int> draw)
    {
        string path = $"{TexDir}/{key}.png";
        var t = new Texture2D(w, h, TextureFormat.RGB24, true);
        var p = new Color[w * h];
        draw(p, w, h);
        t.SetPixels(p);
        t.Apply();
        File.WriteAllBytes(Path.GetFullPath(path), t.EncodeToPNG());
        Object.DestroyImmediate(t);
    }

    // ═══════════════════════════════════════════════════════════════
    //  マテリアル定義
    // ═══════════════════════════════════════════════════════════════
    static void InitMaterials()
    {
        // フロア系（テクスチャあり）
        M("concrete",   new Color(0.40f, 0.40f, 0.43f), "concrete",  8, 8);
        M("conc_dark",  new Color(0.26f, 0.26f, 0.30f), "concrete",  6, 6);
        M("b1_floor",   new Color(0.22f, 0.24f, 0.32f), "b1_floor",  6, 6);
        M("pavement",   new Color(0.32f, 0.30f, 0.27f), "pavement",  6, 6);
        M("mgr_floor",  new Color(0.32f, 0.07f, 0.07f), "mgr_floor", 4, 4);

        // 壁・構造系（テクスチャあり）
        M("wall",       new Color(0.13f, 0.13f, 0.16f), "wall_dark", 8, 8);
        M("b1_wall",    new Color(0.09f, 0.09f, 0.13f), "wall_dark", 6, 6);
        M("door_frame", new Color(0.22f, 0.23f, 0.28f), "metal",     4, 4);
        M("door_panel", new Color(0.30f, 0.32f, 0.38f), "metal",     4, 4);
        M("desk",       new Color(0.18f, 0.16f, 0.13f), "wall_dark", 4, 4);
        M("cctv_body",  new Color(0.18f, 0.18f, 0.22f), "metal",     2, 2);
        M("cctv_lens",  new Color(0.04f, 0.06f, 0.10f));
        M("parking",    new Color(0.82f, 0.80f, 0.74f));
        M("arrow",      new Color(1.00f, 0.88f, 0.10f));

        // エミッシブ系
        ME("em_white",   new Color(0.98f, 0.95f, 0.88f), 1.6f);
        ME("em_amber",   new Color(1.00f, 0.58f, 0.08f), 1.0f);
        ME("em_red",     new Color(0.90f, 0.04f, 0.04f), 0.9f);
        ME("em_blue",    new Color(0.28f, 0.60f, 1.00f), 1.4f);
        ME("em_green",   new Color(0.08f, 0.90f, 0.22f), 0.7f);
        ME("em_caution", new Color(0.95f, 0.80f, 0.00f), 0.7f);
        ME("sign_exit",  new Color(0.06f, 0.82f, 0.28f), 1.2f);
        ME("sign_danger",new Color(0.90f, 0.15f, 0.05f), 0.9f);
    }

    // ═══════════════════════════════════════════════════════════════
    //  外部エリア（各ゾーン独立フロア ─ Z-fight なし）
    // ═══════════════════════════════════════════════════════════════
    static void BuildExterior(GameObject root)
    {
        var ext = G(root, "Exterior");

        // ── フロア（表面 Y=0, 各XZエリアは重複なし） ──
        Floor(ext, "F_North",   0,     -23f, 28f, 12f, "pavement");   // Outside_North
        Floor(ext, "F_EastN",  24f,    -20f, 12f,  6f, "pavement");   // 北東コーナー
        Floor(ext, "F_WestN", -24f,    -20f, 12f,  6f, "pavement");   // 北西コーナー
        Floor(ext, "F_Top",     0f,    -12.5f,16f,  9f, "pavement");  // Outside_Top
        Floor(ext, "F_East",   22f,     -5f, 12f, 16f, "pavement");   // Outside_East
        Floor(ext, "F_West",  -22f,     -5f, 12f, 16f, "pavement");   // Outside_West

        // ── 駐車場ライン（Y=0 +0.04 → 表面より上で Z-fight なし） ──
        const float MY = 0.04f;
        var pk = G(ext, "ParkingLines");
        for (int i = -1; i <= 1; i++)
        {
            C($"PkV_{i}", pk, new Vector3(i * 4.5f, MY, -24f),  new Vector3(0.1f, 0.04f, 10f), "parking");
        }
        C("PkH_F", pk, new Vector3(0, MY, -19.5f), new Vector3(21f, 0.04f, 0.12f), "parking");
        C("PkH_B", pk, new Vector3(0, MY, -29.0f), new Vector3(21f, 0.04f, 0.12f), "parking");
        for (int i = 0; i < 5; i++)
            C($"CL_{i}", pk, new Vector3(0, MY, -13.5f + i * 1.6f), new Vector3(0.12f, 0.04f, 0.9f), "parking");

        // ── フェンス ──
        var fn = G(ext, "Fence");
        C("Fn_N",  fn, new Vector3(  0,  1f, -29.4f), new Vector3(30, 2f, 0.25f), "wall");
        C("Fn_E",  fn, new Vector3( 30f, 1f,    5f),  new Vector3(0.25f, 2f, 50f), "wall");
        C("Fn_W",  fn, new Vector3(-30f, 1f,    5f),  new Vector3(0.25f, 2f, 50f), "wall");
        C("GW_E",  fn, new Vector3( 10.5f, 1f, -17.2f), new Vector3(7, 2f, 0.25f), "wall");
        C("GW_W",  fn, new Vector3(-10.5f, 1f, -17.2f), new Vector3(7, 2f, 0.25f), "wall");

        // ── 建物外壁ファサード ──
        var fa = G(ext, "Facade");
        C("Fa_E",   fa, new Vector3( 7.15f, F1H*0.5f, -8.3f), new Vector3(0.4f, F1H, 9f), "wall");
        C("Fa_W",   fa, new Vector3(-7.15f, F1H*0.5f, -8.3f), new Vector3(0.4f, F1H, 9f), "wall");
        C("FaFr_E", fa, new Vector3( 4.2f,  F1H*0.5f, -8.3f), new Vector3(4.4f, F1H, 0.4f), "concrete");
        C("FaFr_W", fa, new Vector3(-4.2f,  F1H*0.5f, -8.3f), new Vector3(4.4f, F1H, 0.4f), "concrete");
        C("FaTop",  fa, new Vector3(0,      F1H+0.2f, -8.3f), new Vector3(15f,  0.4f, 0.5f), "concrete");
    }

    // ═══════════════════════════════════════════════════════════════
    //  1F
    // ═══════════════════════════════════════════════════════════════
    static void Build1F(GameObject root)
    {
        var f = G(root, "1F");
        const float WH = F1H;  // wall height = 3m

        // ── ロビーメイン (Z: -8 〜 +5) ──
        var lb = G(f, "Lobby_Main");
        Floor(lb, "Floor",  0,   -1.5f, 14f, 13f, "concrete");
        Ceil(lb,  "Ceil",   0,   -1.5f, 14.4f, 13f);
        Wall(lb,  "W_E",    7.15f, WH*0.5f,  -1.5f, 0.3f, WH, 13f);
        Wall(lb,  "W_W",   -7.15f, WH*0.5f,  -1.5f, 0.3f, WH, 13f);
        // 北壁（入口開口の両脇）
        Wall(lb,  "W_N_E",  4.2f,  WH*0.5f, -8.15f, 4.4f, WH, 0.3f);
        Wall(lb,  "W_N_W", -4.2f,  WH*0.5f, -8.15f, 4.4f, WH, 0.3f);

        // サポートピラー（4本）
        var pil = G(f, "Pillars");
        foreach (var (px, pz) in new[]{(5f,-6f),(-5f,-6f),(5f,2f),(-5f,2f)})
            C($"Pil", pil, new Vector3(px, WH*0.5f, pz), new Vector3(0.55f, WH+0.1f, 0.55f), "concrete");

        // ── 階段前エリア (Z: +5 〜 +16) ──
        var st = G(f, "Lobby_Stairs");
        Floor(st, "Floor",  0,  10.5f, 10f, 11f, "concrete");
        Ceil(st,  "Ceil",   0,  10.5f, 10.4f, 11f);
        Wall(st,  "W_E",    5.15f, WH*0.5f, 10.5f, 0.3f, WH, 11f);
        Wall(st,  "W_W",   -5.15f, WH*0.5f, 10.5f, 0.3f, WH, 11f);
        Wall(st,  "W_S",    0,     WH*0.5f, 16.15f, 10f, WH, 0.3f);
        // ロビーとの仕切り（中央開口あり）
        Wall(f,   "Div_E",  5.15f, WH*0.5f, 5.15f, 4f, WH, 0.3f);
        Wall(f,   "Div_W", -5.15f, WH*0.5f, 5.15f, 4f, WH, 0.3f);
    }

    // ═══════════════════════════════════════════════════════════════
    //  階段（1F Y=0 → B1 Y=B1Y）
    // ═══════════════════════════════════════════════════════════════
    static void BuildStaircase(GameObject root)
    {
        var s = G(root, "Staircase");
        const int n = 9;
        for (int i = 0; i < n; i++)
        {
            float z = 16.5f + i * 0.72f;
            float y = i * B1Y / n;  // 0 → B1Y
            // 踏み板（表面が前ステップと重複しないように厚み小さめ）
            C($"Step_{i:D2}", s,
                new Vector3(0, y - 0.12f, z), new Vector3(7.5f, 0.24f, 0.72f), "conc_dark");
        }
        // 手すり
        C("Rail_E", s, new Vector3( 4f, -1.5f, 20f), new Vector3(0.08f, 2f, 7f), "wall");
        C("Rail_W", s, new Vector3(-4f, -1.5f, 20f), new Vector3(0.08f, 2f, 7f), "wall");
        // 側壁
        Wall(s, "SW_E",  4.15f, -2f, 20f, 0.3f, 5.5f, 8f);
        Wall(s, "SW_W", -4.15f, -2f, 20f, 0.3f, 5.5f, 8f);
    }

    // ═══════════════════════════════════════════════════════════════
    //  B1（床表面 Y=B1Y, 天井 Y=B1Y+B1CH）
    // ═══════════════════════════════════════════════════════════════
    static void BuildB1(GameObject root)
    {
        var b = G(root, "B1");

        // ── B1廊下 (Z: 22〜32) ──
        B1Room(b, "B1_Corridor",  27f, 10f, "b1_floor");
        Wall(b, "W_N_B1", 0, B1Y + B1CH*0.5f, 22.15f, 8f, B1CH, 0.3f);

        // ── 管理人室前廊下 (Z: 32〜42) ──
        B1Room(b, "B1_DoorFront", 37f, 10f, "b1_floor");

        // ── 管理人室 (Z: 42〜56) ──
        var m = G(b, "ManagersRoom");
        Floor(m, "Floor",  0,  49f, 10f, 14f, "mgr_floor");
        Ceil(m,  "Ceil",   0,  49f, 10.4f, 14f);
        Wall(m,  "W_E",    5.15f, B1Y+B1CH*0.5f, 49f, 0.3f, B1CH, 14f);
        Wall(m,  "W_W",   -5.15f, B1Y+B1CH*0.5f, 49f, 0.3f, B1CH, 14f);
        Wall(m,  "W_S",    0,     B1Y+B1CH*0.5f, 56.15f, 10f, B1CH, 0.3f);
        // 管理人室 入口側壁（ドア開口の両脇）
        Wall(m,  "W_N_E",  3.5f,  B1Y+B1CH*0.5f, 42.15f, 3f, B1CH, 0.3f);
        Wall(m,  "W_N_W", -3.5f,  B1Y+B1CH*0.5f, 42.15f, 3f, B1CH, 0.3f);
        Wall(m,  "W_N_T",  0,     B1Y+B1CH-0.2f, 42.15f, 4f, 0.4f, 0.3f);

        // ── パイプ・配線 ──
        var pi = G(b, "Pipes");
        C("PL_E",  pi, new Vector3( 2.5f, B1Y+B1CH+0.05f, 35f), new Vector3(0.2f,  0.2f,  28f), "wall");
        C("PL_W",  pi, new Vector3(-2.0f, B1Y+B1CH+0.05f, 35f), new Vector3(0.15f, 0.2f,  28f), "wall");
        for (int i = 0; i < 5; i++)
            C($"PC_{i}", pi, new Vector3(0, B1Y+B1CH+0.12f, 24f+i*6f), new Vector3(8f, 0.15f, 0.15f), "wall");

        // ── ハザードストライプ（Y=B1Y+0.05f → 床表面より明確に上） ──
        const float SY = 0.05f;
        var hz = G(b, "Hazard");
        for (int i = 0; i < 4; i++)
        {
            string mc = i % 2 == 0 ? "em_caution" : "b1_wall";
            C($"HN_{i}", hz, new Vector3(0, B1Y+SY, 22.6f+i*0.9f), new Vector3(8f,  0.04f, 0.45f), mc);
            C($"HB_{i}", hz, new Vector3(0, B1Y+SY, 32.6f+i*0.9f), new Vector3(8f,  0.04f, 0.45f), mc);
            C($"HM_{i}", hz, new Vector3(0, B1Y+SY, 42.2f+i*0.9f), new Vector3(4.5f,0.04f, 0.45f), mc);
        }
    }

    // B1ルーム共通（床・天井・側壁）
    static void B1Room(GameObject parent, string name, float cz, float sz, string floorMat)
    {
        var r = G(parent, name);
        Floor(r, "Floor", 0, cz, 8f, sz, floorMat);
        Ceil(r, "Ceil",   0, cz, 8.4f, sz);
        Wall(r, "W_E",  4.15f, B1Y+B1CH*0.5f, cz, 0.3f, B1CH, sz);
        Wall(r, "W_W", -4.15f, B1Y+B1CH*0.5f, cz, 0.3f, B1CH, sz);
    }

    // ═══════════════════════════════════════════════════════════════
    //  ドア
    // ═══════════════════════════════════════════════════════════════
    static void BuildDoors(GameObject root)
    {
        Door(root, DoorID.Gate,
            name: "Door_Gate  [外壁ゲート -3%/分]",
            cen: new Vector3(0, 0, -17.2f),
            ow: 14f, oh: 3f, pt: 0.28f,
            slide: new Vector3(15f, 0, 0), fh: 4f);

        Door(root, DoorID.Entrance,
            name: "Door_Entrance  [地上入口 -2%/分]",
            cen: new Vector3(0, 0, -8.2f),
            ow: 8f, oh: 3f, pt: 0.22f,
            slide: new Vector3(0, 3.4f, 0), fh: 3.7f);

        Door(root, DoorID.BasementStairs,
            name: "Door_BasementStairs  [地下階段 -4%/分]",
            cen: new Vector3(0, B1Y, 22.2f),
            ow: 7.5f, oh: B1CH, pt: 0.25f,
            slide: new Vector3(0, B1CH+0.4f, 0), fh: B1CH);

        Door(root, DoorID.B1Corridor,
            name: "Door_B1Corridor  [B1廊下 -5%/分]",
            cen: new Vector3(0, B1Y, 32.2f),
            ow: 7.5f, oh: B1CH, pt: 0.25f,
            slide: new Vector3(8f, 0, 0), fh: B1CH);
    }

    static void Door(GameObject root, DoorID id, string name,
        Vector3 cen, float ow, float oh, float pt, Vector3 slide, float fh)
    {
        const float pw = 0.45f, fd = 0.52f, th = 0.48f;

        var dr = G(root, name);
        dr.transform.position = cen;

        // フレーム
        var fr = G(dr, "Frame");
        C("Pil_L", fr, cen + new Vector3(-(ow*.5f+pw*.5f), fh*.5f, 0), new Vector3(pw, fh, fd), "door_frame");
        C("Pil_R", fr, cen + new Vector3(  ow*.5f+pw*.5f,  fh*.5f, 0), new Vector3(pw, fh, fd), "door_frame");
        C("TopBar",fr, cen + new Vector3(0, oh+th*.5f, 0), new Vector3(ow+pw*2, th, fd+.1f), "door_frame");

        // パネル（DoorAnimator が動かす）
        Vector3 pw2 = cen + new Vector3(0, oh*.5f, 0);
        var panel = C("DoorPanel", dr, pw2, new Vector3(ow-.06f, oh-.06f, pt), "door_panel");

        // パネル装飾ライン（パネルの子 → アニメーション追従）
        for (int i = -1; i <= 1; i += 2)
            C($"Line_{(i<0?"L":"R")}", panel,
                pw2 + new Vector3(i*ow*.3f, 0, -(pt*.5f+.02f)),
                new Vector3(.07f, oh*.9f, .04f), "door_frame");

        // 警告ランプ
        C("WL", dr, cen + new Vector3(0, oh+th+.3f, -.2f), new Vector3(.38f,.38f,.38f), "em_amber");

        // 床警告ストライプ（Y = 表面 + 0.05f）
        for (int i = 0; i < 3; i++)
        {
            string mc = i%2==0 ? "em_caution" : "wall";
            float sy = cen.y + .05f;
            C($"SF_{i}", dr, new Vector3(0, sy, cen.z-.55f-i*.55f), new Vector3(ow*.85f,.04f,.42f), mc);
            C($"SB_{i}", dr, new Vector3(0, sy, cen.z+.55f+i*.55f), new Vector3(ow*.85f,.04f,.42f), mc);
        }

        var anim = dr.AddComponent<DoorAnimator>();
        anim.Setup(id, panel.transform, slide);
    }

    // ═══════════════════════════════════════════════════════════════
    //  照明
    // ═══════════════════════════════════════════════════════════════
    static void BuildLights(GameObject root)
    {
        var li = G(root, "CeilingLights");

        // エミッシブストリップ（視覚）
        for (int i = 0; i < 4; i++)
            C($"Lb_{i}", li, new Vector3(0, F1H-.02f, -5.5f+i*2.5f), new Vector3(9f,.07f,.28f), "em_white");
        C("Ls_0",  li, new Vector3(0, F1H-.02f,  8f),  new Vector3(7f,.07f,.28f), "em_white");
        C("Ls_1",  li, new Vector3(0, F1H-.02f, 13f),  new Vector3(7f,.07f,.28f), "em_white");
        C("LC_L",  li, new Vector3(-3f,2.9f,-10.5f), new Vector3(.22f,.07f,7f), "em_white");
        C("LC_R",  li, new Vector3( 3f,2.9f,-10.5f), new Vector3(.22f,.07f,7f), "em_white");

        float by = B1Y+B1CH-.02f;
        for (int i = 0; i < 6; i++)
            C($"LB1_{i}", li, new Vector3(0, by, 23f+i*5f), new Vector3(5.5f,.05f,.18f), "em_amber");
        C("LMR_L",  li, new Vector3(-3f,by,46f), new Vector3(.18f,.05f,5f), "em_red");
        C("LMR_R",  li, new Vector3( 3f,by,46f), new Vector3(.18f,.05f,5f), "em_red");
        C("LMW",    li, new Vector3( 0f,by,53f), new Vector3(4.5f,.07f,.28f), "em_white");

        // Unity PointLight
        var pl = G(root, "PointLights");
        PL(pl,"PL_Lb0", new Vector3(-3f, 2.8f, -4f),   new Color(1f,.93f,.80f), .7f, 6f);
        PL(pl,"PL_Lb1", new Vector3( 3f, 2.8f,  2f),   new Color(1f,.93f,.80f), .7f, 6f);
        PL(pl,"PL_St",  new Vector3( 0f, 2.8f, 11f),   new Color(1f,.93f,.80f), .5f, 5f);
        PL(pl,"PL_Ent", new Vector3( 0f, 2.5f,-10.5f), new Color(.85f,.9f,1f),  .6f, 7f);
        float pb = B1Y+B1CH-.1f;
        PL(pl,"PL_B1_0",new Vector3(0f,pb,25f), new Color(1f,.55f,.05f), .5f,5f);
        PL(pl,"PL_B1_1",new Vector3(0f,pb,32f), new Color(1f,.55f,.05f), .5f,5f);
        PL(pl,"PL_B1_2",new Vector3(0f,pb,39f), new Color(1f,.55f,.05f), .4f,5f);
        PL(pl,"PL_Mr",  new Vector3(0f,pb,46f), new Color(.9f,.08f,.06f), .5f,6f);
        PL(pl,"PL_Mw",  new Vector3(0f,pb,52f), new Color(.95f,.9f,.8f), .6f,5f);
    }

    // ═══════════════════════════════════════════════════════════════
    //  プロップ
    // ═══════════════════════════════════════════════════════════════
    static void BuildProps(GameObject root)
    {
        var pr = G(root, "Props");
        const float SY = 0.05f;

        // ボラード（Y=0.5, 地面に半分埋まっている → 底面Z-fight無し）
        foreach (int xi in new[]{-6,-3,3,6})
            C($"Bol_{xi}", pr, new Vector3(xi, .5f, -19.8f), new Vector3(.32f,1f,.32f), "wall");

        // ロビー受付カウンター
        C("Cnt_M",  pr, new Vector3( 0,    .5f, 3.5f), new Vector3(6f,  1f, .5f), "desk");
        C("Cnt_S",  pr, new Vector3( 2.75f,.5f, 3.0f), new Vector3(.5f, 1f, 1.5f),"desk");
        C("Cnt_T",  pr, new Vector3( 0,   1.03f,3.5f), new Vector3(6.2f,.06f,.6f),"desk");

        // 管理人室デスク
        float dz = 44.5f;
        C("Dk_M",   pr, new Vector3(0,    B1Y+.4f, dz),     new Vector3(7.5f,.8f,1f),   "desk");
        C("Dk_L",   pr, new Vector3(-3.2f,B1Y+.4f, dz+1.2f),new Vector3(1.1f,.8f,2.6f), "desk");
        C("Dk_R",   pr, new Vector3( 3.2f,B1Y+.4f, dz+1.2f),new Vector3(1.1f,.8f,2.6f), "desk");
        C("Dk_T",   pr, new Vector3(0,    B1Y+.83f,dz),     new Vector3(7.5f,.05f,1f),  "desk");

        // モニター
        float mY = B1Y+1.15f;
        C("Mon_L",  pr, new Vector3(-2.5f,mY,dz-.42f), new Vector3(1.2f,.7f,.07f),  "em_blue");
        C("Mon_C",  pr, new Vector3(0,    mY,dz-.42f), new Vector3(1.5f,.78f,.07f), "em_blue");
        C("Mon_R",  pr, new Vector3( 2.5f,mY,dz-.42f), new Vector3(1.2f,.7f,.07f),  "em_blue");
        foreach (float mx in new[]{-2.5f,0f,2.5f})
            C($"MS_{mx}", pr, new Vector3(mx, B1Y+.72f,dz-.42f), new Vector3(.07f,.68f,.07f),"desk");

        // セキュリティパネル
        C("SecP",   pr, new Vector3(0, B1Y+1.4f, 55.88f), new Vector3(4f,1.6f,.1f), "desk");
        for (int i = 0; i < 8; i++)
            C($"LED_{i}",pr, new Vector3(-1.75f+i*.5f, B1Y+1.55f, 55.83f),
                new Vector3(.1f,.1f,.04f), i<5 ? "em_green" : "em_red");

        // 椅子
        C("ChS", pr, new Vector3(0, B1Y+.26f,dz+1.9f), new Vector3(.65f,.12f,.65f),"desk");
        C("ChB", pr, new Vector3(0, B1Y+.6f, dz+2.2f), new Vector3(.65f,.65f,.08f),"desk");
        foreach (var (cx,cz2) in new[]{(-.28f,-.2f),(.28f,-.2f),(-.28f,.28f),(.28f,.28f)})
            C($"CL{cx}", pr, new Vector3(cx, B1Y+.1f,dz+1.9f+cz2), new Vector3(.07f,.52f,.07f),"wall");
    }

    // ═══════════════════════════════════════════════════════════════
    //  経路矢印
    // ═══════════════════════════════════════════════════════════════
    static void BuildPathArrows(GameObject root)
    {
        var ar = G(root, "PathArrows");
        float by = B1Y + .2f;
        Arrow(ar, "N→Top",         new Vector3(0,.2f,-17f),  new Vector3(0,.2f,-13f));
        Arrow(ar, "Top→Lobby",     new Vector3(0,.2f, -8f),  new Vector3(0,.2f, -1.5f));
        Arrow(ar, "Lobby→Stairs",  new Vector3(0,.2f,  5f),  new Vector3(0,.2f, 10.5f));
        Arrow(ar, "Stairs→B1",     new Vector3(0, 0f, 16.5f),new Vector3(0,by,  22f));
        Arrow(ar, "B1→DoorFront",  new Vector3(0,by,  32f),  new Vector3(0,by,  37f));
        Arrow(ar, "DoorFront→Mgr", new Vector3(0,by,  42f),  new Vector3(0,by,  49f));
    }

    // ═══════════════════════════════════════════════════════════════
    //  屋根
    // ═══════════════════════════════════════════════════════════════
    static void BuildRoof(GameObject root)
    {
        var rf = G(root, "Roof");
        C("Slab",    rf, new Vector3(0,  F1H+.2f, 3f),    new Vector3(14.8f,.4f,22.8f), "wall");
        C("Para_N",  rf, new Vector3(0,  F1H+.5f,-8.4f),  new Vector3(14.8f,.3f,.4f),   "wall");
        C("Para_S",  rf, new Vector3(0,  F1H+.5f,16.4f),  new Vector3(14.8f,.3f,.4f),   "wall");
        C("Para_E",  rf, new Vector3( 7.4f,F1H+.5f,4f),   new Vector3(.4f,.3f,25f),     "wall");
        C("Para_W",  rf, new Vector3(-7.4f,F1H+.5f,4f),   new Vector3(.4f,.3f,25f),     "wall");
        C("HVAC_0",  rf, new Vector3(-3f, F1H+.65f,-2f),  new Vector3(2.5f,.9f,1.5f),   "wall");
        C("HVAC_1",  rf, new Vector3( 3f, F1H+.65f, 6f),  new Vector3(2.5f,.9f,1.5f),   "wall");
        C("Duct_E",  rf, new Vector3( 6.5f,F1H+.35f,4f),  new Vector3(.5f,.25f,3f),     "wall");
        C("Duct_W",  rf, new Vector3(-6.5f,F1H+.35f,4f),  new Vector3(.5f,.25f,3f),     "wall");
    }

    // ═══════════════════════════════════════════════════════════════
    //  サイン
    // ═══════════════════════════════════════════════════════════════
    static void BuildSignage(GameObject root)
    {
        var sg = G(root, "Signage");
        C("S_Gate", sg, new Vector3(0,  3.8f,          -17.1f), new Vector3(1.2f,.35f,.06f), "sign_exit");
        C("S_Ent",  sg, new Vector3(0,  3.6f,           -8.1f), new Vector3(1.0f,.30f,.06f), "sign_exit");
        C("S_BSt",  sg, new Vector3(0,  B1Y+B1CH+.5f,  22.1f), new Vector3(.8f,.25f,.06f),  "sign_exit");
        C("S_B1C",  sg, new Vector3(0,  B1Y+B1CH+.5f,  32.1f), new Vector3(.8f,.25f,.06f),  "sign_danger");
        C("S_Mgr",  sg, new Vector3(2.7f,B1Y+1.6f,     42.1f), new Vector3(.5f,.18f,.06f),  "em_red");
        C("FL_1F",  sg, new Vector3(6.8f,2.2f,          -1.5f), new Vector3(.4f,.25f,.04f),  "em_white");
        C("FL_B1",  sg, new Vector3(3.8f,B1Y+2.0f,      23f),   new Vector3(.4f,.22f,.04f),  "em_amber");
    }

    // ═══════════════════════════════════════════════════════════════
    //  CCTV プロップ
    // ═══════════════════════════════════════════════════════════════
    static void BuildCCTVMounts(GameObject root)
    {
        var cc = G(root, "CCTVMounts");
        (string n, Vector3 e)[] cams =
        {
            ("SceneCam_OUT_N",   new Vector3(20,   0, 0)),
            ("SceneCam_OUT_E",   new Vector3(15, -90, 0)),
            ("SceneCam_OUT_W",   new Vector3(15,  90, 0)),
            ("SceneCam_OUT_TOP", new Vector3(35,   0, 0)),
            ("SceneCam_IN_1F_A", new Vector3(15, 180, 0)),
            ("SceneCam_IN_1F_B", new Vector3(15, 180, 0)),
            ("SceneCam_IN_B1_A", new Vector3(15, 180, 0)),
            ("SceneCam_IN_B1_B", new Vector3(15, 180, 0)),
        };
        foreach (var (cn, euler) in cams)
        {
            var go = GameObject.Find(cn);
            if (go == null) continue;
            Vector3 pos = go.transform.position;
            Vector3 fwd = Quaternion.Euler(euler) * Vector3.forward;
            var m = G(cc, $"CCTV_{cn}");
            C("Bracket", m, pos+Vector3.up*.18f, new Vector3(.05f,.36f,.05f), "door_frame");
            var body = C("Body", m, pos, new Vector3(.16f,.12f,.24f), "cctv_body");
            body.transform.rotation = Quaternion.LookRotation(fwd.normalized);
            var lens = C("Lens", m, pos+fwd.normalized*.13f, new Vector3(.08f,.08f,.07f), "cctv_lens");
            lens.transform.rotation = body.transform.rotation;
            C("LED",  m, pos+fwd.normalized*.11f+body.transform.up*.04f, new Vector3(.02f,.02f,.02f),"em_red");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  SceneCam 更新
    // ═══════════════════════════════════════════════════════════════
    static void UpdateSceneCamPositions()
    {
        (string n, Vector3 pos, Vector3 rot)[] cams =
        {
            ("SceneCam_OUT_N",   new Vector3(  0, 3.0f,-23f),    new Vector3(20,  0,0)),
            ("SceneCam_OUT_E",   new Vector3( 22, 3.0f, -5f),    new Vector3(15,-90,0)),
            ("SceneCam_OUT_W",   new Vector3(-22, 3.0f, -5f),    new Vector3(15, 90,0)),
            ("SceneCam_OUT_TOP", new Vector3(  0, 5.0f,-12.5f),  new Vector3(35,  0,0)),
            ("SceneCam_IN_1F_A", new Vector3(  0, 3.0f, -1.5f),  new Vector3(15,180,0)),
            ("SceneCam_IN_1F_B", new Vector3(  0, 3.0f, 10.5f),  new Vector3(15,180,0)),
            ("SceneCam_IN_B1_A", new Vector3(  0, B1Y+2.1f, 27f),new Vector3(15,180,0)),
            ("SceneCam_IN_B1_B", new Vector3(  0, B1Y+2.1f, 37f),new Vector3(15,180,0)),
        };
        int moved = 0;
        foreach (var (n, pos, rot) in cams)
        {
            var go = GameObject.Find(n);
            if (go == null) continue;
            Undo.RecordObject(go.transform, $"MapBuilder:{n}");
            go.transform.position    = pos;
            go.transform.eulerAngles = rot;
            EditorUtility.SetDirty(go);
            moved++;
        }
        Debug.Log($"[NIGHTMARE] SceneCam_* 更新 ({moved}/8)");
    }

    // ═══════════════════════════════════════════════════════════════
    //  マテリアルヘルパー
    // ═══════════════════════════════════════════════════════════════
    static Shader GetShader()
    {
        if (_shader != null) return _shader;
        var rp = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
        if (rp != null)
        {
            string rn = rp.GetType().Name;
            if (rn.Contains("Universal") || rn.Contains("URP"))
                _shader = Shader.Find("Universal Render Pipeline/Lit")
                       ?? Shader.Find("Universal Render Pipeline/Simple Lit");
            else if (rn.Contains("HDRender") || rn.Contains("HDRP"))
                _shader = Shader.Find("HDRP/Lit");
        }
        _shader ??= Shader.Find("Standard") ?? Shader.Find("Diffuse");
        if (_shader == null)
            Debug.LogError("[NIGHTMARE] シェーダー未検出。Graphics設定を確認してください。");
        return _shader;
    }

    static void M(string key, Color col, string texKey = null, float tx = 4, float ty = 4)
    {
        var sh = GetShader(); if (sh == null) return;
        string path = $"{MatDir}/{key}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path) ?? new Material(sh);
        mat.shader = sh;
        mat.color  = col;
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.04f);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.04f);
        if (mat.HasProperty("_Metallic"))   mat.SetFloat("_Metallic",   0.00f);
        mat.DisableKeyword("_EMISSION");
        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);
        if (texKey != null && Texs.TryGetValue(texKey, out var tex))
        {
            mat.mainTexture = tex;
            mat.mainTextureScale = new Vector2(tx, ty);
        }
        if (!AssetDatabase.Contains(mat)) AssetDatabase.CreateAsset(mat, path);
        EditorUtility.SetDirty(mat);
        Mats[key] = mat;
    }

    static void ME(string key, Color col, float em)
    {
        M(key, col);
        var mat = Mats[key];
        mat.EnableKeyword("_EMISSION");
        if (mat.HasProperty("_EmissionColor"))
            mat.SetColor("_EmissionColor", col * em);
        EditorUtility.SetDirty(mat);
    }

    // ═══════════════════════════════════════════════════════════════
    //  ジオメトリヘルパー
    // ═══════════════════════════════════════════════════════════════

    // 床スラブ（中心 Y = surface - 0.15）
    static GameObject Floor(GameObject p, string n, float cx, float cz, float sx, float sz, string mat)
        => C(n, p, new Vector3(cx, 0 - .15f, cz), new Vector3(sx, .3f, sz), mat);

    // B1 床スラブ
    static GameObject B1Floor(GameObject p, string n, float cx, float cz, float sx, float sz, string mat)
        => C(n, p, new Vector3(cx, B1Y - .15f, cz), new Vector3(sx, .3f, sz), mat);

    // 天井スラブ（1F: 中心 Y = F1H + 0.15）
    static void Ceil(GameObject p, string n, float cx, float cz, float sx, float sz)
        => C(n, p, new Vector3(cx, F1H + .15f, cz), new Vector3(sx, .3f, sz), "wall");

    // B1 天井スラブ
    static void B1Ceil(GameObject p, string n, float cx, float cz, float sx, float sz)
        => C(n, p, new Vector3(cx, B1Y + B1CH + .15f, cz), new Vector3(sx, .3f, sz), "b1_wall");

    // 壁（中心座標を直接指定）
    static void Wall(GameObject p, string n, float cx, float cy, float cz, float sx, float sy, float sz)
        => C(n, p, new Vector3(cx, cy, cz), new Vector3(sx, sy, sz), "wall");

    static void Arrow(GameObject p, string n, Vector3 f, Vector3 t)
    {
        var d = t - f; float l = d.magnitude;
        if (l < .01f) return;
        var go = C(n, p, (f+t)*.5f, new Vector3(.28f,.06f,l), "arrow");
        go.transform.rotation = Quaternion.LookRotation(d.normalized);
    }

    static void PL(GameObject p, string n, Vector3 pos, Color col, float intensity, float range)
    {
        var go = new GameObject(n);
        go.transform.SetParent(p.transform);
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point; l.color = col;
        l.intensity = intensity; l.range = range;
        l.shadows = LightShadows.Soft;
    }

    static GameObject C(string n, GameObject p, Vector3 pos, Vector3 sz, string mk)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = n;
        go.transform.SetParent(p.transform);
        go.transform.position   = pos;
        go.transform.localScale = sz;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        if (Mats.TryGetValue(mk, out var mat))
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    static GameObject G(GameObject p, string n)
    {
        var go = new GameObject(n);
        go.transform.SetParent(p.transform);
        return go;
    }

    static void MarkDirty(GameObject root)
    {
        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }
}
