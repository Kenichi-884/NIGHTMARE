// NIGHTMARE > Generate All Assets  でスプライト・Prefabを全自動生成します
// 依存なし: Texture2D を直接ピクセル描画して PNG 保存 → Sprite 化 → Prefab 化

using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using System.IO;

public static class NightmareAssetSetup
{
    private const int CAM_W = 512;
    private const int CAM_H = 384;
    private const int MON_W = 80;
    private const int MON_H = 140;

    private const string ROOT     = "Assets/NightmareAssets";
    private const string CAM_DIR  = ROOT + "/Sprites/Cameras";
    private const string MON_DIR  = ROOT + "/Sprites/Monsters";
    private const string PRE_DIR  = ROOT + "/Prefabs/Monsters";
    private const string JS_DIR   = ROOT + "/Sprites/JumpScares";
    private const string UI_DIR   = ROOT + "/Sprites/UI";
    private const string SFX_DIR  = ROOT + "/Audio";

    // =========================================================
    [MenuItem("NIGHTMARE/Generate All Assets %#g", priority = 2)]
    public static void GenerateAll()
    {
        EnsureDirs();

        // 1. カメラ背景スプライト (8枚)
        GenerateCameraSprites();
        // 2. モンスターオーバーレイスプライト (6枚)
        GenerateMonsterSprites();
        // 3. ジャンプスケアスプライト (6枚)
        GenerateJumpScareSprites();
        // 4. UIスプライト (ビネットなど)
        GenerateUISprites();
        // 5. プロシージャル効果音 (WAV)
        GenerateProceduralAudio();

        AssetDatabase.Refresh();

        // 6. モンスターPrefab作成
        CreateMonsterPrefabs();
        // 7. シーンのMonsterManagerに接続
        WireMonsterManager();
        // 8. SecurityCameraSystemにスプライト接続
        WireCameraSystem();
        // 9. JumpScareManagerにスプライト接続
        WireJumpScareManager();
        // 10. DangerVignetteにスプライト接続
        WireVignette();
        // 11. AudioManagerにSFX接続
        WireAudioManager();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[NIGHTMARE] 全アセット生成完了！");
    }

    // =========================================================
    // カメラ背景スプライト
    // =========================================================
    static void GenerateCameraSprites()
    {
        Save(DrawOutN(),   CAM_DIR + "/cam_out_n.png");
        Save(DrawOutE(),   CAM_DIR + "/cam_out_e.png");
        Save(DrawOutW(),   CAM_DIR + "/cam_out_w.png");
        Save(DrawOutTop(), CAM_DIR + "/cam_out_top.png");
        Save(DrawIn1FA(),  CAM_DIR + "/cam_in_1f_a.png");
        Save(DrawIn1FB(),  CAM_DIR + "/cam_in_1f_b.png");
        Save(DrawInB1A(),  CAM_DIR + "/cam_in_b1_a.png");
        Save(DrawInB1B(),  CAM_DIR + "/cam_in_b1_b.png");
    }

    // OUT-N: 北ゲート・駐車場 (真夜中の屋外)
    static Texture2D DrawOutN()
    {
        var t = NewTex(CAM_W, CAM_H);
        GradV(t, 0, 0, CAM_W, CAM_H, C(0.03f,0.04f,0.06f), C(0.06f,0.07f,0.09f));
        // 地面
        GradV(t, 0, 0, CAM_W, 100, C(0.08f,0.08f,0.07f), C(0.05f,0.05f,0.04f));
        // アスファルトのライン
        Fill(t, 220,20,80,6, C(0.3f,0.28f,0.20f,0.6f));
        Fill(t, 240,30,40,4, C(0.3f,0.28f,0.20f,0.4f));
        // フェンス（横バー）
        Fill(t, 0, 148, CAM_W, 4, C(0.45f,0.45f,0.45f));
        Fill(t, 0, 132, CAM_W, 3, C(0.35f,0.35f,0.35f));
        // フェンス縦柱
        for (int x = 0; x < CAM_W; x += 28)
            Fill(t, x, 100, 3, 60, C(0.4f,0.4f,0.4f));
        // ゲート（中央）
        Fill(t, 196,120, 120,6, C(0.6f,0.55f,0.3f));
        Fill(t, 196,120, 6,  56, C(0.6f,0.55f,0.3f));
        Fill(t, 310,120, 6,  56, C(0.6f,0.55f,0.3f));
        Fill(t, 250,152, 8,  24, C(0.5f,0.45f,0.2f));  // 錠前
        // 遠景の建物シルエット
        Fill(t, 0,   200, 140, 184, C(0.06f,0.07f,0.09f));
        Fill(t, 360, 220, 152, 164, C(0.07f,0.08f,0.10f));
        // 夜空の星
        for (int i = 0; i < 40; i++)
        {
            int sx = (i * 37 + 11) % CAM_W;
            int sy = 180 + (i * 19 + 7) % 200;
            Fill(t, sx, sy, 1, 1, C(0.9f,0.9f,1f,0.8f));
        }
        // 左上に "OUT-N" テキスト代わりのマーカー
        Fill(t, 8, CAM_H-20, 50, 12, C(0f,0f,0f,0.6f));
        ScanLines(t, 0.12f);
        Noise(t, 0.04f);
        t.Apply();
        return t;
    }

    // OUT-E: 東側非常口
    static Texture2D DrawOutE()
    {
        var t = NewTex(CAM_W, CAM_H);
        GradV(t, 0, 0, CAM_W, CAM_H, C(0.04f,0.05f,0.07f), C(0.07f,0.07f,0.09f));
        // 地面
        Fill(t, 0, 0, CAM_W, 90, C(0.09f,0.09f,0.08f));
        // 建物の外壁 (左)
        Fill(t, 0, 80, 180, CAM_H, C(0.12f,0.13f,0.15f));
        // コンクリートのライン（外壁テクスチャ）
        for (int y = 90; y < CAM_H; y += 30)
            Fill(t, 0, y, 180, 2, C(0.08f,0.09f,0.11f));
        for (int x = 0; x < 180; x += 40)
            Fill(t, x, 80, 2, CAM_H, C(0.08f,0.09f,0.11f));
        // 非常口ドア
        Fill(t, 60,  100, 70, 120, C(0.15f,0.18f,0.20f));
        Fill(t, 60,  100, 70,   4, C(0.5f,0.5f,0.5f));
        Fill(t, 60,  100,   4,120, C(0.5f,0.5f,0.5f));
        Fill(t,128,  100,   2,120, C(0.5f,0.5f,0.5f));
        Fill(t, 90,  148,  10,  8, C(0.6f,0.6f,0.0f));  // ドアノブ
        // EXIT サイン
        Fill(t, 70, 226, 56, 14, C(0.6f,0.0f,0.0f,0.9f));
        // 右側の路地
        GradV(t, 180, 0, CAM_W-180, CAM_H, C(0.05f,0.05f,0.07f), C(0.08f,0.08f,0.09f));
        // ゴミ箱
        Fill(t, 220, 30, 40, 56, C(0.18f,0.18f,0.16f));
        Fill(t, 218, 80, 44,  5, C(0.25f,0.25f,0.22f));
        ScanLines(t, 0.10f); Noise(t, 0.05f);
        t.Apply(); return t;
    }

    // OUT-W: 西側搬入口
    static Texture2D DrawOutW()
    {
        var t = NewTex(CAM_W, CAM_H);
        GradV(t, 0, 0, CAM_W, CAM_H, C(0.03f,0.04f,0.06f), C(0.07f,0.07f,0.09f));
        Fill(t, 0, 0, CAM_W, 80, C(0.10f,0.10f,0.09f));
        // 大型シャッター（搬入口）
        Fill(t, 80, 80, 340, 220, C(0.14f,0.15f,0.18f));
        for (int y = 80; y < 300; y += 22)
            Fill(t, 80, y, 340, 2, C(0.10f,0.11f,0.13f));
        // シャッターの縦レール
        Fill(t, 80,  80, 6, 220, C(0.2f,0.2f,0.22f));
        Fill(t, 414, 80, 6, 220, C(0.2f,0.2f,0.22f));
        // ハンドル
        Fill(t, 236,160,40,16, C(0.4f,0.38f,0.30f));
        // パレット
        Fill(t,  20, 20, 50, 40, C(0.25f,0.20f,0.15f));
        Fill(t,  20, 20, 50,  4, C(0.30f,0.25f,0.18f));
        Fill(t,  24, 24, 42,  4, C(0.30f,0.25f,0.18f));
        // コーン
        Fill(t, 440, 30, 18, 40, C(0.7f,0.3f,0.0f,0.8f));
        Fill(t, 438, 24, 22,  8, C(0.9f,0.5f,0.0f));
        ScanLines(t, 0.10f); Noise(t, 0.05f);
        t.Apply(); return t;
    }

    // OUT-TOP: 地上入口の真上（見下ろし）
    static Texture2D DrawOutTop()
    {
        var t = NewTex(CAM_W, CAM_H);
        Fill(t, 0, 0, CAM_W, CAM_H, C(0.06f,0.07f,0.09f));
        // 外の地面（周縁）
        Fill(t, 0, 0,   60, CAM_H, C(0.10f,0.10f,0.09f));
        Fill(t, CAM_W-60, 0, 60, CAM_H, C(0.10f,0.10f,0.09f));
        Fill(t, 0, 0, CAM_W, 60, C(0.10f,0.10f,0.09f));
        // 階段（見下ろし）
        for (int i = 0; i < 4; i++)
            Fill(t, 60+i*8, 60-i*6, CAM_W-120-i*16, 8+i*2, C(0.16f+i*0.02f, 0.16f+i*0.02f, 0.15f+i*0.02f));
        // 玄関ドア（中央）
        Fill(t, 156, 88, 200, 240, C(0.12f,0.14f,0.18f));
        Fill(t, 156, 88, 200,   4, C(0.3f,0.3f,0.35f));
        Fill(t, 156, 88,   4, 240, C(0.3f,0.3f,0.35f));
        Fill(t, 354, 88,   4, 240, C(0.3f,0.3f,0.35f));
        Fill(t, 252, 88,   4, 240, C(0.25f,0.25f,0.3f));  // 中心線
        // ガラスの光反射
        Fill(t, 165, 96, 80, 30, C(0.15f,0.18f,0.25f,0.6f));
        Fill(t, 267, 96, 80, 30, C(0.15f,0.18f,0.25f,0.6f));
        // ドアノブ×2
        Fill(t, 240, 200, 10, 10, C(0.6f,0.55f,0.3f));
        Fill(t, 262, 200, 10, 10, C(0.6f,0.55f,0.3f));
        ScanLines(t, 0.12f); Noise(t, 0.04f);
        t.Apply(); return t;
    }

    // IN-1F-A: 1Fロビー
    static Texture2D DrawIn1FA()
    {
        var t = NewTex(CAM_W, CAM_H);
        GradV(t, 0, 80, CAM_W, CAM_H-80, C(0.10f,0.11f,0.14f), C(0.07f,0.08f,0.10f));
        // 天井
        Fill(t, 0, CAM_H-70, CAM_W, 70, C(0.14f,0.15f,0.18f));
        // 蛍光灯
        Fill(t, 100, CAM_H-60, 120, 8, C(0.85f,0.90f,1.0f,0.9f));
        Fill(t, 280, CAM_H-60, 120, 8, C(0.85f,0.90f,1.0f,0.9f));
        // 床
        Fill(t, 0, 0, CAM_W, 80, C(0.12f,0.12f,0.13f));
        for (int x = 0; x < CAM_W; x += 80)
            Fill(t, x, 0, 2, 80, C(0.10f,0.10f,0.11f));
        // 奥の壁
        Fill(t, 80, 80, CAM_W-160, CAM_H-160, C(0.11f,0.12f,0.15f));
        // エレベーターのドア（奥）
        Fill(t, 168, 96, 80, 160, C(0.18f,0.20f,0.25f));
        Fill(t, 168, 96, 80,   3, C(0.4f,0.4f,0.5f));
        Fill(t, 168, 96,   3, 160, C(0.4f,0.4f,0.5f));
        Fill(t, 245, 96,   3, 160, C(0.4f,0.4f,0.5f));
        Fill(t, 205, 96,   3, 160, C(0.3f,0.3f,0.4f));
        Fill(t, 200,250,  12,  6, C(0.6f,0.55f,0.3f));  // ▲ボタン
        // 受付カウンター
        Fill(t,  60, 60, 160, 30, C(0.18f,0.18f,0.20f));
        Fill(t,  60, 88, 160,  4, C(0.3f,0.3f,0.32f));
        // 非常口サイン
        Fill(t, 380, 308, 40, 18, C(0.0f,0.4f,0.0f,0.9f));
        // 床の光反射（蛍光灯の映り込み）
        Fill(t, 130, 32, 80, 16, C(0.15f,0.16f,0.19f,0.5f));
        ScanLines(t, 0.08f); Noise(t, 0.04f);
        t.Apply(); return t;
    }

    // IN-1F-B: 1F 階段・EV前
    static Texture2D DrawIn1FB()
    {
        var t = NewTex(CAM_W, CAM_H);
        GradV(t, 0, 0, CAM_W, CAM_H, C(0.09f,0.10f,0.13f), C(0.06f,0.07f,0.09f));
        // 天井
        Fill(t, 0, CAM_H-50, CAM_W, 50, C(0.13f,0.14f,0.17f));
        // 薄暗い蛍光灯（明滅する設定なので暗め）
        Fill(t, 200, CAM_H-42, 100, 5, C(0.5f,0.55f,0.7f,0.6f));
        // 床
        Fill(t, 0, 0, CAM_W, 70, C(0.10f,0.10f,0.11f));
        // 左側：階段
        Fill(t, 0, 60, 220, CAM_H-110, C(0.08f,0.09f,0.11f));
        for (int i = 0; i < 7; i++)
        {
            int sx = 20 + i * 26;
            int sy = 70 + i * 28;
            Fill(t, sx, sy, 140 - i*16, 26, C(0.12f+i*0.01f, 0.13f+i*0.01f, 0.15f));
            Fill(t, sx, sy+24, 140-i*16, 2, C(0.25f,0.25f,0.28f));
        }
        // 手すり
        for (int i = 0; i < 7; i++)
            Fill(t, 20+i*26, 92+i*28, 3, CAM_H-160-i*28, C(0.35f,0.35f,0.38f));
        // 右側：EVドア
        Fill(t, 320, 70, 180, 250, C(0.16f,0.18f,0.22f));
        Fill(t, 320, 70, 180,   3, C(0.35f,0.35f,0.40f));
        Fill(t, 320, 70,   3, 250, C(0.35f,0.35f,0.40f));
        Fill(t, 498, 70,   3, 250, C(0.35f,0.35f,0.40f));
        Fill(t, 408, 70,   3, 250, C(0.28f,0.28f,0.32f));  // 中央分割線
        // ▼地下へのサイン
        Fill(t, 340, 280, 100, 20, C(0.1f,0.3f,0.5f,0.9f));
        ScanLines(t, 0.10f); Noise(t, 0.05f);
        t.Apply(); return t;
    }

    // IN-B1-A: B1メイン廊下
    static Texture2D DrawInB1A()
    {
        var t = NewTex(CAM_W, CAM_H);
        // 非常灯のみ（赤みがかった暗い廊下）
        GradV(t, 0, 0, CAM_W, CAM_H, C(0.04f,0.02f,0.02f), C(0.07f,0.04f,0.04f));
        // 天井
        Fill(t, 0, CAM_H-44, CAM_W, 44, C(0.08f,0.05f,0.05f));
        // 非常灯（赤）
        Fill(t, 50,  CAM_H-36, 30, 16, C(0.5f,0.05f,0.05f,0.8f));
        Fill(t, 420, CAM_H-36, 30, 16, C(0.5f,0.05f,0.05f,0.8f));
        // 廊下 遠近感（左右の壁）
        for (int i = 0; i < 8; i++)
        {
            int margin = i * 28;
            int wallH  = 20 + i * 20;
            Fill(t, margin, CAM_H-44-wallH, 4, wallH, C(0.12f,0.07f,0.07f));
            Fill(t, CAM_W-margin-4, CAM_H-44-wallH, 4, wallH, C(0.12f,0.07f,0.07f));
        }
        // 床の中央線（遠近感）
        for (int i = 1; i < 10; i++)
            Fill(t, CAM_W/2-2, i*30, 4, 20, C(0.12f,0.08f,0.08f,0.6f));
        // 廊下両側のドア
        Fill(t,  20, 100, 60, 110, C(0.10f,0.06f,0.06f));
        Fill(t,  20, 100, 60,   3, C(0.25f,0.15f,0.15f));
        Fill(t, 420, 140, 60,  90, C(0.10f,0.06f,0.06f));
        Fill(t, 420, 140, 60,   3, C(0.25f,0.15f,0.15f));
        // ドアノブ
        Fill(t,  72, 148, 8, 8, C(0.35f,0.25f,0.15f));
        Fill(t, 428, 178, 8, 8, C(0.35f,0.25f,0.15f));
        // 奥の壁
        Fill(t, 224, 152, 64, 172, C(0.09f,0.05f,0.05f));
        ScanLines(t, 0.08f); Noise(t, 0.07f);
        t.Apply(); return t;
    }

    // IN-B1-B: 管理人室前廊下 (最も暗い)
    static Texture2D DrawInB1B()
    {
        var t = NewTex(CAM_W, CAM_H);
        // 真っ暗に近い
        Fill(t, 0, 0, CAM_W, CAM_H, C(0.02f,0.01f,0.01f));
        // 天井
        Fill(t, 0, CAM_H-36, CAM_W, 36, C(0.05f,0.03f,0.03f));
        // 壁の輪郭（遠近感）
        for (int i = 0; i < 6; i++)
        {
            int margin = i * 36;
            Fill(t, margin, CAM_H-36-30-i*22, 3, 30+i*22, C(0.08f,0.04f,0.04f));
            Fill(t, CAM_W-margin-3, CAM_H-36-30-i*22, 3, 30+i*22, C(0.08f,0.04f,0.04f));
        }
        // 奥の管理人室ドア
        Fill(t, 192, 80, 128, 212, C(0.10f,0.07f,0.06f));
        Fill(t, 192, 80, 128,   3, C(0.3f,0.2f,0.15f));
        Fill(t, 192, 80,   3, 212, C(0.3f,0.2f,0.15f));
        Fill(t, 317, 80,   3, 212, C(0.3f,0.2f,0.15f));
        // ドアノブ
        Fill(t, 300, 170, 10, 10, C(0.45f,0.35f,0.20f));
        // ドアの下の光（管理人室の明かり）
        GradV(t, 192, 76, 128, 6, C(0.6f,0.5f,0.3f,0.7f), Color.clear);
        // 壁の「管理室」プレート
        Fill(t, 218, 264, 76, 16, C(0.18f,0.14f,0.10f));
        Noise(t, 0.10f); ScanLines(t, 0.06f);
        t.Apply(); return t;
    }

    // =========================================================
    // モンスタースプライト (シルエット)
    // =========================================================
    static void GenerateMonsterSprites()
    {
        Save(DrawCrawler(),  MON_DIR + "/monster_crawler.png");
        Save(DrawRusher(),   MON_DIR + "/monster_rusher.png");
        Save(DrawJammer(),   MON_DIR + "/monster_jammer.png");
        Save(DrawLurker(),   MON_DIR + "/monster_lurker.png");
        Save(DrawMimic(),    MON_DIR + "/monster_mimic.png");
        Save(DrawKnocker(),  MON_DIR + "/monster_knocker.png");
    }

    // Crawler: 猫背の人型
    static Texture2D DrawCrawler()
    {
        var t = NewTex(MON_W, MON_H);
        Fill(t, 0, 0, MON_W, MON_H, Color.clear);
        Color c = C(0.15f,0.10f,0.08f,0.95f);
        Fill(t, 28, 100, 24, 24, c); // 頭
        Fill(t, 24, 80,  32,  24, c); // 首〜肩
        Fill(t, 16, 52,  48,  36, c); // 胴体（猫背で前傾）
        Fill(t, 16, 16,  12,  40, c); // 左腕（下に長い）
        Fill(t, 52, 26,  12,  36, c); // 右腕
        Fill(t, 22,  8,  16,  52, c); // 左脚
        Fill(t, 42,  8,  16,  52, c); // 右脚
        // 目の光（赤）
        Fill(t, 32, 114, 4, 4, C(0.8f,0.1f,0.05f,1f));
        Fill(t, 44, 114, 4, 4, C(0.8f,0.1f,0.05f,1f));
        Noise(t, 0.05f); t.Apply(); return t;
    }

    // Rusher: 前傾姿勢で走る細身
    static Texture2D DrawRusher()
    {
        var t = NewTex(MON_W, MON_H);
        Fill(t, 0, 0, MON_W, MON_H, Color.clear);
        Color c = C(0.12f,0.08f,0.06f,0.95f);
        Fill(t, 36, 108, 22, 22, c); // 頭（前傾）
        Fill(t, 20, 86,  36, 28, c); // 胴体（斜め）
        Fill(t, 10, 60,  10, 36, c); // 左腕（後方）
        Fill(t, 54, 72,  14, 28, c); // 右腕（前方）
        Fill(t, 18,  8,  14, 58, c); // 左脚（長い）
        Fill(t, 36, 18,  14, 48, c); // 右脚
        Fill(t, 38, 114, 4, 4, C(1.0f,0.5f,0.0f,1f)); // 目（オレンジ）
        Fill(t, 48, 114, 4, 4, C(1.0f,0.5f,0.0f,1f));
        Noise(t, 0.04f); t.Apply(); return t;
    }

    // Jammer: 配線が絡まった丸い生物
    static Texture2D DrawJammer()
    {
        var t = NewTex(MON_W, MON_H);
        Fill(t, 0, 0, MON_W, MON_H, Color.clear);
        Color c = C(0.10f,0.08f,0.15f,0.95f);
        // 本体（楕円っぽい）
        Fill(t, 20, 60, 40, 50, c);
        Fill(t, 14, 70, 52, 32, c);
        // 触手/配線
        Fill(t,  6, 80,  20, 4, c);
        Fill(t,  4, 68,  16, 4, c);
        Fill(t, 54, 76,  20, 4, c);
        Fill(t, 56, 62,  16, 4, c);
        Fill(t, 28, 56,   4, 16, c);
        Fill(t, 48, 52,   4, 18, c);
        // 目（紫）
        Fill(t, 30, 88, 6, 6, C(0.7f,0.1f,1.0f,1f));
        Fill(t, 44, 88, 6, 6, C(0.7f,0.1f,1.0f,1f));
        // ノイズ効果（Jammer専用）
        for (int i = 0; i < 20; i++)
        {
            int nx = (i * 17 + 5) % MON_W;
            int ny = 50 + (i * 13 + 3) % 60;
            Fill(t, nx, ny, 2, 1, C(0.7f,0.2f,1.0f,0.5f));
        }
        Noise(t, 0.06f); t.Apply(); return t;
    }

    // Lurker: 非常に背が高く細い
    static Texture2D DrawLurker()
    {
        var t = NewTex(MON_W, MON_H);
        Fill(t, 0, 0, MON_W, MON_H, Color.clear);
        Color c = C(0.06f,0.06f,0.08f,0.88f); // 半透明の暗い影
        Fill(t, 32, 118, 16, 16, c); // 頭
        Fill(t, 30, 96,  20, 28, c); // 首
        Fill(t, 28, 52,  24, 52, c); // 胴体（細い）
        Fill(t, 16, 68,  14, 48, c); // 左腕（長い）
        Fill(t, 50, 60,  14, 54, c); // 右腕
        Fill(t, 28,  6,  10, 52, c); // 左脚
        Fill(t, 42,  6,  10, 52, c); // 右脚
        // 目が見えない（Lurkerは不可視）
        t.Apply(); return t;
    }

    // Mimic: グリッチした人型
    static Texture2D DrawMimic()
    {
        var t = NewTex(MON_W, MON_H);
        Fill(t, 0, 0, MON_W, MON_H, Color.clear);
        Color c = C(0.05f,0.15f,0.15f,0.90f);
        // 基本の人型（ずれた感じ）
        Fill(t, 30, 104, 20, 22, c);
        Fill(t, 26, 82,  28, 26, c);
        Fill(t, 22, 52,  12, 38, c);
        Fill(t, 46, 56,  12, 38, c);
        Fill(t, 26,  8,  12, 50, c);
        Fill(t, 42,  8,  12, 50, c);
        // グリッチライン（水平ずれ）
        for (int y = 50; y < 130; y += 8)
        {
            int offset = ((y / 8) % 3 == 0) ? 6 : 0;
            Fill(t, offset, y, 60, 3, C(0.1f,0.7f,0.7f,0.3f));
        }
        // 目（水色）
        Fill(t, 33, 116, 4, 4, C(0.2f,0.9f,0.9f,1f));
        Fill(t, 43, 116, 4, 4, C(0.2f,0.9f,0.9f,1f));
        Noise(t, 0.10f); t.Apply(); return t;
    }

    // Knocker: 重くがっしりした体格
    static Texture2D DrawKnocker()
    {
        var t = NewTex(MON_W, MON_H);
        Fill(t, 0, 0, MON_W, MON_H, Color.clear);
        Color c = C(0.18f,0.14f,0.10f,0.95f);
        Fill(t, 24, 106, 32, 30, c); // 頭（大きい）
        Fill(t, 14, 76,  52, 38, c); // 胴体（幅広）
        Fill(t,  4, 70,  16, 48, c); // 左腕（太い）
        Fill(t, 60, 70,  16, 48, c); // 右腕（太い）
        Fill(t, 20,  8,  18, 74, c); // 左脚
        Fill(t, 42,  8,  18, 74, c); // 右脚
        // 目（黄色）
        Fill(t, 32, 120, 6, 6, C(1.0f,0.9f,0.1f,1f));
        Fill(t, 42, 120, 6, 6, C(1.0f,0.9f,0.1f,1f));
        // 拳（強調）
        Fill(t, 2, 62, 20, 16, c);
        Fill(t, 58,62, 20, 16, c);
        Noise(t, 0.04f); t.Apply(); return t;
    }

    // =========================================================
    // ジャンプスケアスプライト (全画面・モンスターの顔)
    // =========================================================
    static void GenerateJumpScareSprites()
    {
        Directory.CreateDirectory(Application.dataPath + "/NightmareAssets/Sprites/JumpScares");
        Save(DrawJSCrawler(),  JS_DIR + "/js_crawler.png");
        Save(DrawJSRusher(),   JS_DIR + "/js_rusher.png");
        Save(DrawJSJammer(),   JS_DIR + "/js_jammer.png");
        Save(DrawJSLurker(),   JS_DIR + "/js_lurker.png");
        Save(DrawJSMimic(),    JS_DIR + "/js_mimic.png");
        Save(DrawJSKnocker(),  JS_DIR + "/js_knocker.png");
    }

    // Crawler: 暗赤色の顔、白い目、口元に歯
    static Texture2D DrawJSCrawler()
    {
        var t = NewTex(960, 720);
        Fill(t, 0, 0, 960, 720, C(0.04f, 0.01f, 0.01f));
        // 頭の輪郭
        FillOval(t, 240, 160, 480, 420, C(0.08f, 0.03f, 0.03f));
        FillOval(t, 260, 180, 440, 380, C(0.10f, 0.04f, 0.04f));
        // 目（白く光る）
        FillOval(t, 310, 380, 120, 80, C(0.9f, 0.9f, 1.0f));
        FillOval(t, 530, 380, 120, 80, C(0.9f, 0.9f, 1.0f));
        FillOval(t, 350, 390, 40,  40, C(0.05f,0.0f,0.0f));
        FillOval(t, 570, 390, 40,  40, C(0.05f,0.0f,0.0f));
        // 口
        Fill(t, 340, 260, 280, 6, C(0.6f, 0.05f, 0.05f));
        for (int i = 0; i < 8; i++) Fill(t, 352 + i*36, 266, 20, 40, C(0.85f, 0.85f, 0.80f));
        // 血のような滲み
        for (int i = 0; i < 15; i++)
        {
            int rx = 280 + (i * 29 % 400); int ry = 100 + (i * 17 % 200);
            Fill(t, rx, ry, 3+i%5, 20+i%30, C(0.5f, 0.02f, 0.02f, 0.7f));
        }
        ScanLines(t, 0.05f); Noise(t, 0.06f);
        t.Apply(); return t;
    }

    // Rusher: オレンジの目、歪んだ輪郭
    static Texture2D DrawJSRusher()
    {
        var t = NewTex(960, 720);
        Fill(t, 0, 0, 960, 720, C(0.03f, 0.02f, 0.01f));
        FillOval(t, 200, 140, 560, 460, C(0.08f, 0.05f, 0.02f));
        // 歪み効果（横にずれた輪郭）
        for (int i = 0; i < 10; i++)
            FillOval(t, 220 + i*4, 160 + i*2, 520, 420, C(0.07f+i*0.005f, 0.04f, 0.01f, 0.5f));
        FillOval(t, 300, 370, 130, 90, C(1.0f, 0.55f, 0.0f));
        FillOval(t, 530, 370, 130, 90, C(1.0f, 0.55f, 0.0f));
        FillOval(t, 340, 388, 50,  50, C(0.1f, 0.0f, 0.0f));
        FillOval(t, 570, 388, 50,  50, C(0.1f, 0.0f, 0.0f));
        Fill(t, 330, 250, 300, 5, C(0.7f, 0.3f, 0.0f));
        for (int i = 0; i < 9; i++) Fill(t, 342+i*34, 255, 18, 36, C(0.88f, 0.82f, 0.72f));
        Noise(t, 0.08f); ScanLines(t, 0.06f);
        t.Apply(); return t;
    }

    // Jammer: 紫の目、ノイズだらけ
    static Texture2D DrawJSJammer()
    {
        var t = NewTex(960, 720);
        Fill(t, 0, 0, 960, 720, C(0.02f, 0.01f, 0.04f));
        FillOval(t, 180, 120, 600, 500, C(0.06f, 0.03f, 0.10f));
        FillOval(t, 280, 360, 140, 100, C(0.7f, 0.1f, 1.0f));
        FillOval(t, 540, 360, 140, 100, C(0.7f, 0.1f, 1.0f));
        FillOval(t, 322, 384, 54,  54, C(0.0f, 0.0f, 0.05f));
        FillOval(t, 582, 384, 54,  54, C(0.0f, 0.0f, 0.05f));
        // ノイズライン
        for (int y = 80; y < 640; y += 6) Fill(t, 0, y, 960, 2, C(0.4f, 0.1f, 0.8f, 0.15f));
        for (int i = 0; i < 30; i++)
        {
            int gx = (i * 43) % 900; int gy = 80 + (i * 31) % 560;
            Fill(t, gx, gy, 40+i%20, 3, C(0.6f, 0.2f, 1.0f, 0.4f));
        }
        Noise(t, 0.12f); t.Apply(); return t;
    }

    // Lurker: ほぼ真っ暗、目だけが光る
    static Texture2D DrawJSLurker()
    {
        var t = NewTex(960, 720);
        Fill(t, 0, 0, 960, 720, C(0.01f, 0.01f, 0.015f));
        FillOval(t, 260, 180, 440, 400, C(0.04f, 0.04f, 0.05f, 0.6f));
        // 目だけがほのかに光る
        FillOval(t, 355, 370, 80, 50, C(0.4f, 0.5f, 0.6f, 0.8f));
        FillOval(t, 525, 370, 80, 50, C(0.4f, 0.5f, 0.6f, 0.8f));
        // 目の中心
        FillOval(t, 380, 382, 30, 30, C(0.1f, 0.15f, 0.2f));
        FillOval(t, 550, 382, 30, 30, C(0.1f, 0.15f, 0.2f));
        Noise(t, 0.04f); t.Apply(); return t;
    }

    // Mimic: グリッチ、二重像
    static Texture2D DrawJSMimic()
    {
        var t = NewTex(960, 720);
        Fill(t, 0, 0, 960, 720, C(0.01f, 0.03f, 0.03f));
        FillOval(t, 230, 150, 500, 430, C(0.05f, 0.10f, 0.10f));
        // ずれた二重像
        FillOval(t, 248, 162, 500, 430, C(0.08f, 0.15f, 0.15f, 0.5f));
        FillOval(t, 290, 355, 120, 85, C(0.2f, 0.9f, 0.9f));
        FillOval(t, 550, 355, 120, 85, C(0.2f, 0.9f, 0.9f));
        // ずれたコピーの目
        FillOval(t, 310, 370, 120, 85, C(0.0f, 0.5f, 0.5f, 0.5f));
        FillOval(t, 570, 370, 120, 85, C(0.0f, 0.5f, 0.5f, 0.5f));
        // グリッチライン
        for (int y = 100; y < 620; y += 10)
        {
            int offset = (y / 10 % 5 == 0) ? 18 : 0;
            Fill(t, offset, y, 960, 4, C(0.1f, 0.8f, 0.8f, 0.08f));
        }
        Noise(t, 0.10f); t.Apply(); return t;
    }

    // Knocker: 黄色い目、重い感じの顔
    static Texture2D DrawJSKnocker()
    {
        var t = NewTex(960, 720);
        Fill(t, 0, 0, 960, 720, C(0.03f, 0.025f, 0.02f));
        FillOval(t, 160, 100, 640, 540, C(0.09f, 0.07f, 0.05f));
        FillOval(t, 180, 120, 600, 500, C(0.11f, 0.09f, 0.06f));
        FillOval(t, 290, 350, 150, 110, C(1.0f, 0.9f, 0.1f));
        FillOval(t, 520, 350, 150, 110, C(1.0f, 0.9f, 0.1f));
        FillOval(t, 338, 378, 54,  54, C(0.05f, 0.04f, 0.0f));
        FillOval(t, 568, 378, 54,  54, C(0.05f, 0.04f, 0.0f));
        Fill(t, 320, 240, 320, 7, C(0.6f, 0.5f, 0.2f));
        for (int i = 0; i < 7; i++) Fill(t, 334 + i*44, 247, 26, 50, C(0.90f, 0.86f, 0.76f));
        Noise(t, 0.05f); ScanLines(t, 0.04f);
        t.Apply(); return t;
    }

    // 楕円を描く
    static void FillOval(Texture2D t, int x, int y, int w, int h, Color c)
    {
        float cx = x + w * 0.5f;
        float cy = y + h * 0.5f;
        float rx = w * 0.5f;
        float ry = h * 0.5f;
        for (int py = y; py < y + h; py++)
            for (int px = x; px < x + w; px++)
            {
                float dx = (px - cx) / rx;
                float dy = (py - cy) / ry;
                if (dx * dx + dy * dy <= 1f)
                {
                    var existing = t.GetPixel(px, py);
                    t.SetPixel(px, py, AlphaBlend(existing, c));
                }
            }
    }

    // =========================================================
    // JumpScareManager への接続
    // =========================================================
    static void WireJumpScareManager()
    {
        var jsm = Object.FindObjectOfType<JumpScareManager>();
        if (jsm == null) { Debug.LogWarning("[NIGHTMARE] JumpScareManagerが見つかりません。"); return; }

        var so = new SerializedObject(jsm);
        SetSpriteField(so, "faceCrawler", JS_DIR + "/js_crawler.png");
        SetSpriteField(so, "faceRusher",  JS_DIR + "/js_rusher.png");
        SetSpriteField(so, "faceJammer",  JS_DIR + "/js_jammer.png");
        SetSpriteField(so, "faceLurker",  JS_DIR + "/js_lurker.png");
        SetSpriteField(so, "faceMimic",   JS_DIR + "/js_mimic.png");
        SetSpriteField(so, "faceKnocker", JS_DIR + "/js_knocker.png");
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(jsm);
        Debug.Log("[NIGHTMARE] JumpScareManager 接続完了");
    }

    static void SetSpriteField(SerializedObject so, string field, string path)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null) sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        var prop = so.FindProperty(field);
        if (prop != null && sprite != null) prop.objectReferenceValue = sprite;
    }

    // =========================================================
    // Prefab 生成
    // =========================================================
    static void CreateMonsterPrefabs()
    {
        CreatePrefab<CrawlerAI>("Crawler", "monster_crawler");
        CreatePrefab<RusherAI> ("Rusher",  "monster_rusher");
        CreatePrefab<JammerAI> ("Jammer",  "monster_jammer");
        CreatePrefab<LurkerAI> ("Lurker",  "monster_lurker");
        CreatePrefab<MimicAI>  ("Mimic",   "monster_mimic");
        CreatePrefab<KnockerAI>("Knocker", "monster_knocker");
    }

    static void CreatePrefab<T>(string typeName, string spriteName) where T : MonsterBase
    {
        var prefabPath = $"{PRE_DIR}/Monster_{typeName}.prefab";
        var go = new GameObject($"Monster_{typeName}");
        go.AddComponent<T>();

        // cameraSprite をセット
        var sprite = LoadSprite($"{MON_DIR}/{spriteName}.png");
        if (sprite != null)
        {
            var so = new SerializedObject(go.GetComponent<T>());
            so.FindProperty("cameraSprite").objectReferenceValue = sprite;
            so.ApplyModifiedProperties();
        }

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        Debug.Log($"[NIGHTMARE] Prefab作成: {prefabPath}");
    }

    // =========================================================
    // MonsterManager に Prefab を接続
    // =========================================================
    static void WireMonsterManager()
    {
        var mgr = Object.FindObjectOfType<MonsterManager>();
        if (mgr == null) { Debug.LogWarning("[NIGHTMARE] MonsterManagerが見つかりません。シーンセットアップ後に実行してください。"); return; }

        var so = new SerializedObject(mgr);
        SetPrefabField(so, "crawlerPrefab", "Crawler");
        SetPrefabField(so, "rusherPrefab",  "Rusher");
        SetPrefabField(so, "jammerPrefab",  "Jammer");
        SetPrefabField(so, "lurkerPrefab",  "Lurker");
        SetPrefabField(so, "mimicPrefab",   "Mimic");
        SetPrefabField(so, "knockerPrefab", "Knocker");
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(mgr);
        Debug.Log("[NIGHTMARE] MonsterManager接続完了");
    }

    static void SetPrefabField(SerializedObject so, string field, string typeName)
    {
        var path = $"{PRE_DIR}/Monster_{typeName}.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) return;
        var prop = so.FindProperty(field);
        if (prop != null) prop.objectReferenceValue = prefab.GetComponent<MonsterBase>();
    }

    // =========================================================
    // SecurityCameraSystem にスプライトを接続
    // =========================================================
    static void WireCameraSystem()
    {
        var sys = Object.FindObjectOfType<SecurityCameraSystem>();
        if (sys == null) { Debug.LogWarning("[NIGHTMARE] SecurityCameraSystemが見つかりません。"); return; }

        var so = new SerializedObject(sys);
        var configsProp = so.FindProperty("cameraConfigs");
        if (configsProp == null || !configsProp.isArray) return;

        // カメラIDとスプライトのマッピング
        var spriteMap = new System.Collections.Generic.Dictionary<string, string>
        {
            {"OUT_N",   "cam_out_n"},
            {"OUT_E",   "cam_out_e"},
            {"OUT_W",   "cam_out_w"},
            {"OUT_TOP", "cam_out_top"},
            {"IN_1F_A", "cam_in_1f_a"},
            {"IN_1F_B", "cam_in_1f_b"},
            {"IN_B1_A", "cam_in_b1_a"},
            {"IN_B1_B", "cam_in_b1_b"},
        };

        for (int i = 0; i < configsProp.arraySize; i++)
        {
            var elem     = configsProp.GetArrayElementAtIndex(i);
            var idProp   = elem.FindPropertyRelative("id");
            var spriteProp = elem.FindPropertyRelative("backgroundSprite");
            if (idProp == null || spriteProp == null) continue;

            string idName = ((CameraID)idProp.enumValueIndex).ToString();
            if (spriteMap.TryGetValue(idName, out var spriteName))
            {
                var sprite = LoadSprite($"{CAM_DIR}/{spriteName}.png");
                if (sprite != null) spriteProp.objectReferenceValue = sprite;
            }
        }
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(sys);
        Debug.Log("[NIGHTMARE] SecurityCameraSystem スプライト接続完了");
    }

    // =========================================================
    // UIスプライト (ビネット)
    // =========================================================
    static void GenerateUISprites()
    {
        Save(DrawVignette(), UI_DIR + "/vignette.png");
    }

    // 放射状グラデーション（中央透明→周縁赤）のビネットテクスチャ
    static Texture2D DrawVignette()
    {
        int w = 256, h = 256;
        var t = NewTex(w, h);
        float cx = w * 0.5f, cy = h * 0.5f;
        float rMax = Mathf.Sqrt(cx * cx + cy * cy);
        for (int py = 0; py < h; py++)
            for (int px = 0; px < w; px++)
            {
                float dx = (px - cx) / cx;
                float dy = (py - cy) / cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy); // 0=中央, ~1.41=角
                float a = Mathf.Clamp01((dist - 0.55f) / 0.55f);
                a = a * a * a; // 強調
                t.SetPixel(px, py, new Color(1f, 0f, 0f, a));
            }
        t.Apply();
        return t;
    }

    // =========================================================
    // プロシージャル効果音 (WAV生成)
    // =========================================================
    static void GenerateProceduralAudio()
    {
        int rate = 44100;
        SaveWav(SFX_DIR + "/heartbeat.wav",  GenerateHeartbeat(rate));
        SaveWav(SFX_DIR + "/button_click.wav", GenerateClick(rate));
        SaveWav(SFX_DIR + "/camera_static.wav", GenerateStatic(rate, 0.3f));
        SaveWav(SFX_DIR + "/door_close.wav",  GenerateThump(rate, 120f, 0.15f));
        SaveWav(SFX_DIR + "/door_open.wav",   GenerateThump(rate, 200f, 0.12f));
        SaveWav(SFX_DIR + "/power_flicker.wav", GeneratePowerFlicker(rate));
    }

    // 二段階の心拍音 (低周波バス+アタック)
    static float[] GenerateHeartbeat(int rate)
    {
        int dur = (int)(rate * 0.6f);
        float[] s = new float[dur];
        // 1拍目: 0.0s
        Sine(s, rate, 0, 0.06f, 80f, 0.8f);
        Sine(s, rate, 0, 0.06f, 40f, 0.5f);
        // 2拍目: 0.15s
        Sine(s, rate, (int)(rate * 0.15f), 0.06f, 80f, 0.5f);
        Sine(s, rate, (int)(rate * 0.15f), 0.06f, 40f, 0.3f);
        EnvelopeAll(s, 0.005f, 0.03f, rate);
        return s;
    }

    // UIクリック音 (短い高域クリック)
    static float[] GenerateClick(int rate)
    {
        int dur = (int)(rate * 0.05f);
        float[] s = new float[dur];
        Sine(s, rate, 0, 0.05f, 1200f, 0.5f);
        Sine(s, rate, 0, 0.02f, 2400f, 0.25f);
        EnvelopeAll(s, 0.001f, 0.04f, rate);
        return s;
    }

    // カメラスタティック (帯域制限ノイズ)
    static float[] GenerateStatic(int rate, float duration)
    {
        int dur = (int)(rate * duration);
        float[] s = new float[dur];
        for (int i = 0; i < dur; i++)
            s[i] = (Random.value * 2f - 1f) * 0.4f;
        EnvelopeAll(s, 0.01f, 0.1f, rate);
        return s;
    }

    // ドアの衝撃音
    static float[] GenerateThump(int rate, float freq, float dur)
    {
        int n = (int)(rate * dur);
        float[] s = new float[n];
        Sine(s, rate, 0, dur, freq, 0.7f);
        Sine(s, rate, 0, dur, freq * 0.5f, 0.4f);
        EnvelopeAll(s, 0.002f, dur * 0.7f, rate);
        return s;
    }

    // 電力フリッカー音
    static float[] GeneratePowerFlicker(int rate)
    {
        int dur = (int)(rate * 0.4f);
        float[] s = new float[dur];
        for (int i = 0; i < dur; i++)
        {
            float t = (float)i / rate;
            float hum = Mathf.Sin(2 * Mathf.PI * 60f * t) * 0.3f;
            float noise = (Random.value * 2f - 1f) * 0.2f;
            float env = Mathf.Sin(Mathf.PI * t / 0.4f);
            s[i] = (hum + noise) * env;
        }
        return s;
    }

    static void Sine(float[] buf, int rate, int offset, float dur, float freq, float amp)
    {
        int end = Mathf.Min(offset + (int)(rate * dur), buf.Length);
        for (int i = offset; i < end; i++)
        {
            float t = (float)(i - offset) / rate;
            buf[i] += Mathf.Sin(2 * Mathf.PI * freq * t) * amp;
        }
    }

    static void EnvelopeAll(float[] buf, float attack, float release, int rate)
    {
        int attackSamples  = (int)(rate * attack);
        int releaseSamples = (int)(rate * release);
        for (int i = 0; i < buf.Length; i++)
        {
            float env = 1f;
            if (i < attackSamples)
                env = (float)i / attackSamples;
            int fromEnd = buf.Length - i;
            if (fromEnd < releaseSamples)
                env *= (float)fromEnd / releaseSamples;
            buf[i] *= env;
        }
    }

    static void SaveWav(string assetPath, float[] samples)
    {
        int rate = 44100;
        int channels = 1;
        int bitsPerSample = 16;
        int dataSize = samples.Length * 2;

        using var ms = new System.IO.MemoryStream();
        using var bw = new System.IO.BinaryWriter(ms);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + dataSize);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16);
        bw.Write((short)1);
        bw.Write((short)channels);
        bw.Write(rate);
        bw.Write(rate * channels * bitsPerSample / 8);
        bw.Write((short)(channels * bitsPerSample / 8));
        bw.Write((short)bitsPerSample);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        bw.Write(dataSize);
        foreach (float s in samples)
        {
            short v = (short)Mathf.Clamp(s * 32767f, -32768f, 32767f);
            bw.Write(v);
        }
        string full = Application.dataPath + "/../" + assetPath;
        File.WriteAllBytes(full, ms.ToArray());
    }

    // =========================================================
    // DangerVignetteにスプライト接続
    // =========================================================
    static void WireVignette()
    {
        var vignetteGO = GameObject.Find("DangerVignette");
        if (vignetteGO == null) return;
        var sprite = LoadSprite(UI_DIR + "/vignette.png");
        if (sprite == null) return;
        var img = vignetteGO.GetComponent<Image>();
        if (img == null) return;
        img.sprite = sprite;
        img.type = Image.Type.Simple;
        img.preserveAspect = false;
        EditorUtility.SetDirty(vignetteGO);
    }

    // =========================================================
    // AudioManagerに効果音を接続
    // =========================================================
    static void WireAudioManager()
    {
        var am = Object.FindObjectOfType<AudioManager>();
        if (am == null) { Debug.LogWarning("[NIGHTMARE] AudioManagerが見つかりません。"); return; }

        // プロシージャル生成した効果音のみ接続（BGMは手動で設定）
        var sfxMap = new System.Collections.Generic.Dictionary<string, string>
        {
            { "heartbeat",     SFX_DIR + "/heartbeat.wav"     },
            { "button_click",  SFX_DIR + "/button_click.wav"  },
            { "camera_static", SFX_DIR + "/camera_static.wav" },
            { "door_close",    SFX_DIR + "/door_close.wav"    },
            { "door_open",     SFX_DIR + "/door_open.wav"     },
            { "power_flicker", SFX_DIR + "/power_flicker.wav" },
        };

        var so = new SerializedObject(am);
        var listProp = so.FindProperty("sfxList");
        if (listProp == null || !listProp.isArray) return;

        foreach (var (key, path) in sfxMap)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) continue;

            // 既存エントリを探して更新、なければ追加
            bool found = false;
            for (int i = 0; i < listProp.arraySize; i++)
            {
                var elem = listProp.GetArrayElementAtIndex(i);
                if (elem.FindPropertyRelative("key").stringValue == key)
                {
                    elem.FindPropertyRelative("clip").objectReferenceValue = clip;
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                listProp.arraySize++;
                var elem = listProp.GetArrayElementAtIndex(listProp.arraySize - 1);
                elem.FindPropertyRelative("key").stringValue = key;
                elem.FindPropertyRelative("clip").objectReferenceValue = clip;
                elem.FindPropertyRelative("volume").floatValue = 1f;
            }
        }
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(am);
        Debug.Log("[NIGHTMARE] AudioManager SFX接続完了");
    }

    // =========================================================
    // ユーティリティ
    // =========================================================
    static void EnsureDirs()
    {
        Directory.CreateDirectory(Application.dataPath + "/NightmareAssets/Sprites/Cameras");
        Directory.CreateDirectory(Application.dataPath + "/NightmareAssets/Sprites/Monsters");
        Directory.CreateDirectory(Application.dataPath + "/NightmareAssets/Sprites/JumpScares");
        Directory.CreateDirectory(Application.dataPath + "/NightmareAssets/Sprites/UI");
        Directory.CreateDirectory(Application.dataPath + "/NightmareAssets/Prefabs/Monsters");
        Directory.CreateDirectory(Application.dataPath + "/NightmareAssets/Audio");
    }

    static Texture2D NewTex(int w, int h)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Fill(t, 0, 0, w, h, Color.clear);
        return t;
    }

    static void Save(Texture2D tex, string assetPath)
    {
        string fullPath = Application.dataPath + "/../" + assetPath;
        File.WriteAllBytes(fullPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    static Sprite LoadSprite(string assetPath)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (tex == null) return null;
        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath) ??
               Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
    }

    // ─── 描画プリミティブ ───
    static void Fill(Texture2D t, int x, int y, int w, int h, Color c)
    {
        x = Mathf.Clamp(x, 0, t.width);
        y = Mathf.Clamp(y, 0, t.height);
        w = Mathf.Clamp(w, 0, t.width  - x);
        h = Mathf.Clamp(h, 0, t.height - y);
        for (int py = y; py < y + h; py++)
            for (int px = x; px < x + w; px++)
            {
                var existing = t.GetPixel(px, py);
                t.SetPixel(px, py, AlphaBlend(existing, c));
            }
    }

    static void GradV(Texture2D t, int x, int y, int w, int h, Color top, Color bot)
    {
        for (int py = y; py < y + h && py < t.height; py++)
        {
            float f = (h <= 1) ? 0f : (float)(py - y) / (h - 1);
            var c = Color.Lerp(bot, top, f);
            Fill(t, x, py, w, 1, c);
        }
    }

    static void ScanLines(Texture2D t, float strength)
    {
        for (int py = 0; py < t.height; py += 2)
            for (int px = 0; px < t.width; px++)
            {
                var c = t.GetPixel(px, py);
                t.SetPixel(px, py, new Color(c.r * (1f - strength), c.g * (1f - strength), c.b * (1f - strength), c.a));
            }
    }

    static void Noise(Texture2D t, float strength)
    {
        for (int py = 0; py < t.height; py++)
            for (int px = 0; px < t.width; px++)
            {
                var c = t.GetPixel(px, py);
                if (c.a < 0.01f) continue;
                float n = (Random.value - 0.5f) * strength;
                t.SetPixel(px, py, new Color(
                    Mathf.Clamp01(c.r + n),
                    Mathf.Clamp01(c.g + n),
                    Mathf.Clamp01(c.b + n), c.a));
            }
    }

    static Color AlphaBlend(Color dst, Color src)
    {
        float a = src.a + dst.a * (1f - src.a);
        if (a <= 0f) return Color.clear;
        return new Color(
            (src.r * src.a + dst.r * dst.a * (1f - src.a)) / a,
            (src.g * src.a + dst.g * dst.a * (1f - src.a)) / a,
            (src.b * src.a + dst.b * dst.a * (1f - src.a)) / a, a);
    }

    static Color C(float r, float g, float b, float a = 1f) => new Color(r, g, b, a);
}
