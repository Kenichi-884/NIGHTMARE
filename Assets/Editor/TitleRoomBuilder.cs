// NIGHTMARE > Build Title Room でタイトル画面専用の3D管理人室を生成・プレハブ化する
using UnityEngine;
using UnityEditor;
using System.IO;

public static class TitleRoomBuilder
{
    const string PREFAB_PATH  = "Assets/Prefabs/TitleRoom.prefab";
    const string TEX_DIR      = "Assets/Textures/TitleRoom/";
    const string MAT_DIR      = "Assets/Materials/TitleRoom/";
    // ゲームステージと被らない位置
    static readonly Vector3 OFFSET = new Vector3(5000f, 0f, 0f);

    [MenuItem("NIGHTMARE/Build Title Room", priority = 10)]
    public static void Build()
    {
        // 既存を削除
        var old = GameObject.Find("[TitleRoom]");
        if (old) Object.DestroyImmediate(old);

        EnsureDir(TEX_DIR);
        EnsureDir(MAT_DIR);
        EnsureDir("Assets/Prefabs/");

        // テクスチャ生成
        var texConcrete = MakeConcreteTex("wall_concrete",  512, 512, new Color(0.07f, 0.065f, 0.062f));
        var texFloor    = MakeConcreteTex("floor_concrete", 512, 512, new Color(0.055f, 0.050f, 0.048f));
        var texMetal    = MakeMetalTex   ("metal_scratched", 256, 256);
        var texBlood    = MakeBloodTex   ("blood_stain",     256, 256);
        var texGround   = MakeConcreteTex("ground_stain",   256, 256, new Color(0.04f, 0.04f, 0.04f));

        // 部屋生成
        var root = new GameObject("[TitleRoom]");
        BuildRoom(root, texConcrete, texFloor, texMetal, texBlood, texGround);

        // プレハブ保存
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, PREFAB_PATH, InteractionMode.AutomatedAction);
        EditorUtility.SetDirty(root);
        Debug.Log("[NIGHTMARE] Title Room 生成完了 → " + PREFAB_PATH +
                  "\nTitleSceneDirector が自動的にこのオブジェクトを使用します。");
    }

    // ─── 部屋ジオメトリ ──────────────────────────────────────────

    static void BuildRoom(GameObject root,
        Texture2D texConcrete, Texture2D texFloor,
        Texture2D texMetal, Texture2D texBlood, Texture2D texGround)
    {
        // マテリアル（全て .mat ファイルとして保存 → プレハブで正しく参照される）
        var matWall    = LitMat(new Color(0.07f, 0.065f, 0.062f), texConcrete, 0f, 0.05f,  "tr_wall");
        var matFloor   = LitMat(new Color(0.055f, 0.050f, 0.048f), texFloor,   0f, 0.04f,  "tr_floor");
        var matCeiling = LitMat(new Color(0.045f, 0.040f, 0.040f), texConcrete,0f, 0.02f,  "tr_ceiling");
        var matMetal   = LitMat(new Color(0.12f, 0.12f, 0.14f),    texMetal,   0.6f, 0.28f,"tr_metal");
        var matDark    = LitMat(new Color(0.04f, 0.04f, 0.04f),    null,       0f, 0.02f,  "tr_dark");
        var matBlood   = TransparentMat(new Color(0.22f, 0.02f, 0.02f, 0.85f), texBlood,   "tr_blood");
        var matGround  = LitMat(new Color(0.04f, 0.035f, 0.035f),  texGround,  0f, 0.03f,  "tr_ground");

        // 発光マテリアル
        var matMonitor  = EmissiveMat(new Color(0.02f, 0.04f, 0.08f),
                                      new Color(0.02f, 0.07f, 0.22f) * 2.5f, "tr_monitor");
        var matExitSign = EmissiveMat(new Color(0.05f, 0.0f, 0.0f),
                                      new Color(0.9f, 0.05f, 0.02f) * 1.8f,  "tr_exitsign");
        var matChain    = LitMat(new Color(0.3f, 0.28f, 0.22f), null, 0.8f, 0.4f,          "tr_chain");

        // ── 構造体 ──────────────────────────────────────────────────

        // 床
        Box(root, "Floor",    V(0, 0, -4),      V(7f, 0.1f, 9f),        matFloor);
        // 天井
        Box(root, "Ceiling",  V(0, 4, -4),      V(7f, 0.1f, 9f),        matCeiling);
        // 奥壁
        Box(root, "WallBack", V(0, 2, -8.5f),   V(7f, 4.2f, 0.15f),     matWall);
        // 左壁
        Box(root, "WallLeft", V(-3.5f, 2, -4),  V(0.15f, 4.2f, 9f),     matWall);
        // 右壁
        Box(root, "WallRight",V( 3.5f, 2, -4),  V(0.15f, 4.2f, 9f),     matWall);

        // ドア枠（左壁手前）
        Box(root, "DoorFrameTop",  V(-3.42f, 2.6f,  0.2f), V(0.12f, 0.4f, 1.1f), matDark);
        Box(root, "DoorFrameL",    V(-3.42f, 1.2f, -0.4f), V(0.12f, 2.4f, 0.1f), matDark);
        Box(root, "DoorFrameR",    V(-3.42f, 1.2f,  0.8f), V(0.12f, 2.4f, 0.1f), matDark);
        // ドア（少し開いている）
        var door = Box(root, "Door", V(-3.35f, 1.2f, 0.2f), V(0.08f, 2.4f, 1.0f), matDark);
        door.transform.localRotation = Quaternion.Euler(0, -22f, 0);

        // 配管（天井）
        Box(root, "Pipe0", V(-1f, 3.88f, -4f), V(0.10f, 0.10f, 9f), matMetal);
        Box(root, "Pipe1", V( 1f, 3.88f, -4f), V(0.10f, 0.10f, 9f), matMetal);

        // ── 机 ──────────────────────────────────────────────────────

        Box(root, "DeskTop",   V(0, 0.78f, -6.5f), V(3.0f, 0.07f, 1.1f), matMetal);
        Box(root, "DeskLegFL", V(-1.3f, 0.39f, -6.0f), V(0.07f, 0.78f, 0.07f), matMetal);
        Box(root, "DeskLegFR", V( 1.3f, 0.39f, -6.0f), V(0.07f, 0.78f, 0.07f), matMetal);
        Box(root, "DeskLegBL", V(-1.3f, 0.39f, -7.0f), V(0.07f, 0.78f, 0.07f), matMetal);
        Box(root, "DeskLegBR", V( 1.3f, 0.39f, -7.0f), V(0.07f, 0.78f, 0.07f), matMetal);

        // モニター（青白発光）
        Box(root, "MonitorScreen", V(0, 1.26f, -7.42f), V(1.4f, 0.85f, 0.05f), matMonitor);
        Box(root, "MonitorBase",   V(0, 0.88f, -7.32f), V(0.2f, 0.2f,  0.2f),  matMetal);
        // キーボード・書類（散乱）
        Box(root, "Keyboard",   V( 0.1f, 0.82f, -6.15f), V(0.9f, 0.02f, 0.35f), matMetal);
        Box(root, "Paper0",     V(-0.8f, 0.82f, -6.5f),  V(0.4f, 0.01f, 0.28f), matDark);
        Box(root, "Paper1",     V( 0.9f, 0.82f, -6.3f),  V(0.35f,0.01f, 0.25f), matDark);
        Box(root, "Paper2",     V(-0.3f, 0.01f, -5.8f),  V(0.38f,0.01f, 0.27f), matDark);  // 床に落ちた書類
        // コーヒーカップ（倒れている）
        var cup = Cylinder(root, "Cup", V(1.2f, 0.82f, -6.2f), new Vector3(0.07f, 0.08f, 0.07f), matMetal);
        cup.transform.localRotation = Quaternion.Euler(90f, 0, 25f);
        // 枯れた植物
        Box(root, "PlantPot", V(-1.2f, 0.84f, -7.1f), V(0.12f, 0.12f, 0.12f), matDark);

        // ── 椅子（倒れている）────────────────────────────────────

        var chairRoot = new GameObject("Chair");
        chairRoot.transform.SetParent(root.transform);
        chairRoot.transform.localPosition = OFFSET + V(0.5f, 0, -4.8f);
        chairRoot.transform.localRotation = Quaternion.Euler(0, 35f, -82f); // 倒れた状態
        Box(chairRoot, "Seat", Vector3.zero,         V(0.72f, 0.07f, 0.72f), matDark, local: true);
        Box(chairRoot, "Back", V(0, 0.4f, -0.35f),  V(0.72f, 0.72f, 0.07f), matDark, local: true);
        Box(chairRoot, "LegFL",V(-0.3f, -0.23f, -0.3f), V(0.05f, 0.46f, 0.05f), matMetal, local: true);
        Box(chairRoot, "LegFR",V( 0.3f, -0.23f, -0.3f), V(0.05f, 0.46f, 0.05f), matMetal, local: true);
        Box(chairRoot, "LegBL",V(-0.3f, -0.23f,  0.3f), V(0.05f, 0.46f, 0.05f), matMetal, local: true);
        Box(chairRoot, "LegBR",V( 0.3f, -0.23f,  0.3f), V(0.05f, 0.46f, 0.05f), matMetal, local: true);

        // ── スチール棚（右壁）────────────────────────────────────

        Box(root, "Shelf",       V(3.0f, 0.9f,  -7.5f), V(0.65f, 1.8f, 0.7f), matMetal);
        Box(root, "ShelfDoc0",   V(3.0f, 1.5f,  -7.3f), V(0.3f, 0.25f, 0.2f), matDark);
        Box(root, "ShelfDoc1",   V(2.8f, 1.82f, -7.4f), V(0.15f,0.35f, 0.15f),matDark);

        // 非常口サイン（奥壁、赤く発光）
        Box(root, "ExitSign",    V(2.5f, 3.4f, -8.42f), V(0.55f, 0.22f, 0.05f), matExitSign);

        // 天井の蛍光灯本体（壊れかけ）
        Box(root, "LightFixture0", V(-0.5f, 3.92f, -4f), V(0.1f, 0.06f, 1.6f), matMetal);
        Box(root, "LightFixture1", V( 0.5f, 3.92f, -4f), V(0.1f, 0.06f, 1.6f), matMetal);

        // ドアに鎖（水平）
        Box(root, "Chain0", V(-3.38f, 1.6f, 0.2f), V(0.06f, 0.04f, 1.1f), matChain);
        Box(root, "Chain1", V(-3.38f, 1.2f, 0.2f), V(0.06f, 0.04f, 1.1f), matChain);

        // ── ホラー演出オブジェクト ───────────────────────────────

        // 血溜まり（床、ドア付近）
        var bloodPool = Quad(root, "BloodPool",
            V(-2.5f, 0.06f, 0.3f), new Vector3(1.2f, 1f, 0.9f), matBlood);
        bloodPool.transform.localRotation = Quaternion.Euler(90f, 15f, 0);

        // 壁の血痕（奥壁）
        var bloodWall = Quad(root, "BloodWall",
            V(-1.0f, 1.8f, -8.42f), new Vector3(1.0f, 1f, 1.4f), matBlood);

        // 床の引きずり跡（ドア→奥へ）
        var drag = Quad(root, "DragMark",
            V(-2.0f, 0.06f, -2.5f), new Vector3(0.35f, 1f, 3.5f), matBlood);
        drag.transform.localRotation = Quaternion.Euler(90f, 5f, 0);

        // 暗い袋（右奥コーナー）
        var bag = Box(root, "BodyBag", V(2.8f, 0.18f, -8.0f),
            V(0.45f, 0.36f, 1.3f), matGround);
        bag.transform.localRotation = Quaternion.Euler(0, -8f, 4f);

        // 天井セキュリティカメラ
        Box(root, "CamBody",    V(0, 3.7f, -2f), V(0.16f, 0.12f, 0.22f), matMetal);
        Box(root, "CamLens",    V(0, 3.58f, -2.12f), V(0.08f, 0.08f, 0.15f), matDark);

        // ── ライト ────────────────────────────────────────────────

        AddPointLight(root, "OverheadLight",  V(0, 3.8f, -4.5f),
            new Color(0.60f, 0.65f, 0.72f), 0.55f, 14f);
        AddPointLight(root, "MonitorBlue",    V(0, 1.1f, -6.8f),
            new Color(0.12f, 0.28f, 0.80f), 0.60f, 4f);
        AddPointLight(root, "RedAmbient",     V(3f, 3.2f, -7f),
            new Color(0.80f, 0.05f, 0.03f), 0.28f, 9f);
        AddPointLight(root, "ExitSignGlow",   V(2.5f, 3.2f, -8f),
            new Color(0.90f, 0.05f, 0.02f), 0.35f, 4f);
        AddPointLight(root, "CorridorLeakLight", V(-3f, 2.5f, 1.2f),
            new Color(0.45f, 0.40f, 0.30f), 0.12f, 5f);
    }

    // ─── テクスチャ生成 ────────────────────────────────────────────

    static Texture2D MakeConcreteTex(string name, int w, int h, Color baseCol)
    {
        var pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float nx = x / (float)w, ny = y / (float)h;
            float n  = Mathf.PerlinNoise(nx * 6f, ny * 6f) * 0.12f
                     + Mathf.PerlinNoise(nx * 18f, ny * 18f) * 0.04f
                     + Mathf.PerlinNoise(nx * 40f, ny * 40f) * 0.015f;
            // ひび割れライン
            float crack = Mathf.Abs(Mathf.PerlinNoise(nx * 3f + 0.5f, ny * 8f) - 0.5f);
            float crackV = crack < 0.04f ? -0.06f : 0f;
            float v = n + crackV - 0.05f;
            pixels[y * w + x] = new Color(
                Mathf.Clamp01(baseCol.r + v),
                Mathf.Clamp01(baseCol.g + v),
                Mathf.Clamp01(baseCol.b + v));
        }
        return SaveTex(name, w, h, pixels);
    }

    static Texture2D MakeMetalTex(string name, int w, int h)
    {
        var pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float nx = x / (float)w, ny = y / (float)h;
            // 横方向の磨き傷
            float scratch = Mathf.PerlinNoise(nx * 80f, ny * 2f) * 0.08f
                           + Mathf.PerlinNoise(nx * 30f, ny * 1f) * 0.04f;
            float v = 0.1f + scratch;
            pixels[y * w + x] = new Color(
                Mathf.Clamp01(v + 0.02f),
                Mathf.Clamp01(v + 0.02f),
                Mathf.Clamp01(v + 0.04f));
        }
        return SaveTex(name, w, h, pixels);
    }

    static Texture2D MakeBloodTex(string name, int w, int h)
    {
        var pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float nx = x / (float)w - 0.5f, ny = y / (float)h - 0.5f;
            float dist = Mathf.Sqrt(nx * nx + ny * ny);
            float n = Mathf.PerlinNoise(nx * 8f + 1f, ny * 8f + 1f);
            float alpha = Mathf.Clamp01((0.55f - dist * 1.2f) + n * 0.25f - 0.1f);
            float dark  = 0.18f + n * 0.05f;
            pixels[y * w + x] = new Color(dark, dark * 0.06f, dark * 0.04f, alpha);
        }
        return SaveTex(name, w, h, pixels, TextureFormat.RGBA32);
    }

    static Texture2D SaveTex(string name, int w, int h, Color[] pixels,
        TextureFormat fmt = TextureFormat.RGB24)
    {
        var tex  = new Texture2D(w, h, fmt, true);
        tex.SetPixels(pixels);
        tex.Apply();
        var path = TEX_DIR + name + ".png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        Object.DestroyImmediate(tex);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ─── マテリアルヘルパー ────────────────────────────────────────

    static bool IsURP() =>
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline != null;

    static Shader LitShader()
    {
        if (IsURP())
        {
            // パッケージパスから直接ロード（最も確実）
            var s = AssetDatabase.LoadAssetAtPath<Shader>(
                "Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader");
            if (s != null) return s;
            s = Shader.Find("Universal Render Pipeline/Lit");
            if (s != null) return s;
        }
        var fallback = Shader.Find("Standard") ?? Shader.Find("Diffuse");
        if (fallback == null)
            Debug.LogError("[TitleRoomBuilder] 使用可能なシェーダーが見つかりません。");
        return fallback;
    }

    // マテリアルを .mat ファイルとして保存してから参照を返す
    // → プレハブにシェーダー参照が正しく保持される
    static Material SaveMat(Material m, string name)
    {
        var path = MAT_DIR + name + ".mat";
        // 既存があれば上書き
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(m, existing);
            AssetDatabase.SaveAssets();
            Object.DestroyImmediate(m);
            return existing;
        }
        AssetDatabase.CreateAsset(m, path);
        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    static Material LitMat(Color col, Texture2D tex, float metallic, float smoothness, string name)
    {
        var shader = LitShader();
        if (shader == null) return new Material(Shader.Find("Hidden/InternalErrorShader"));
        var m = new Material(shader) { name = name };
        m.color = col;
        m.SetColor("_BaseColor", col);
        if (tex != null) { m.mainTexture = tex; m.SetTexture("_BaseMap", tex); }
        m.SetFloat("_Metallic",   metallic);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Glossiness", smoothness);
        return SaveMat(m, name);
    }

    static Material EmissiveMat(Color col, Color emissionCol, string name)
    {
        var shader = LitShader();
        if (shader == null) return new Material(Shader.Find("Hidden/InternalErrorShader"));
        var m = new Material(shader) { name = name };
        m.color = col;
        m.SetColor("_BaseColor", col);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", emissionCol);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        return SaveMat(m, name);
    }

    static Material TransparentMat(Color col, Texture2D tex, string name)
    {
        var shader = LitShader();
        if (shader == null) return new Material(Shader.Find("Hidden/InternalErrorShader"));
        var m = new Material(shader) { name = name };
        m.color = col;
        m.SetColor("_BaseColor", col);
        if (tex != null) { m.mainTexture = tex; m.SetTexture("_BaseMap", tex); }
        if (IsURP())
        {
            m.SetFloat("_Surface",   1f);
            m.SetFloat("_Blend",     0f);
            m.SetFloat("_AlphaClip", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        else
        {
            m.SetFloat("_Mode", 3f);
            m.EnableKeyword("_ALPHABLEND_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        return SaveMat(m, name);
    }

    // ─── ジオメトリヘルパー ────────────────────────────────────────

    static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

    static GameObject Box(GameObject parent, string name,
        Vector3 pos, Vector3 scale, Material mat, bool local = false)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent.transform);
        go.transform.localPosition = local ? pos : OFFSET + pos;
        go.transform.localScale    = scale;
        go.GetComponent<Renderer>().material = mat;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static GameObject Quad(GameObject parent, string name,
        Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        go.transform.SetParent(parent.transform);
        go.transform.localPosition = OFFSET + pos;
        go.transform.localScale    = scale;
        go.GetComponent<Renderer>().material = mat;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static GameObject Cylinder(GameObject parent, string name,
        Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent.transform);
        go.transform.localPosition = OFFSET + pos;
        go.transform.localScale    = scale;
        go.GetComponent<Renderer>().material = mat;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static void AddPointLight(GameObject parent, string name,
        Vector3 pos, Color col, float intensity, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform);
        go.transform.localPosition = OFFSET + pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = col; l.intensity = intensity; l.range = range;
    }

    static void EnsureDir(string path)
    {
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
    }
}
