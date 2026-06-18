using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;

/// <summary>
/// NIGHTMARE > Generate Missing SFX
/// AllSfxKeys に登録されているうち、ファイルが存在しない SFX と
/// AtmosphericAudioController 用の環境音ループを
/// プロシージャル合成で生成し NightmareAssets/Audio/SFX/ に保存します。
/// </summary>
public static class SfxGenerator
{
    const int SR = 44100;
    const string SFX_DIR  = "NightmareAssets/Audio/SFX";
    const string ATMO_DIR = "NightmareAssets/Audio/Ambient";

    [MenuItem("NIGHTMARE/Generate Missing SFX")]
    public static void Generate()
    {
        // ── ゲームプレイ SFX ────────────────────────────────────────
        string sfxDir = Path.Combine(Application.dataPath, SFX_DIR);
        Directory.CreateDirectory(sfxDir);

        Save(sfxDir, "jumpscare_stinger", JumpscareStinger());
        Save(sfxDir, "camera_destroyed",  CameraDestroyed());
        Save(sfxDir, "knock_regular",     KnockRegular());
        Save(sfxDir, "knock_irregular",   KnockIrregular());
        Save(sfxDir, "lurker_appear",     LurkerAppear());
        Save(sfxDir, "rusher_stomp",      RusherStomp());
        Save(sfxDir, "power_out",         PowerOut());
        Save(sfxDir, "power_restore",     PowerRestore());
        Save(sfxDir, "fake_footstep",     FakeFootstep());
        Save(sfxDir, "time_warp",         TimeWarp());
        Save(sfxDir, "ghost_signal",      GhostSignal());
        Save(sfxDir, "blackout",          Blackout());
        Save(sfxDir, "menu_ambience",     MenuAmbience());

        // ── AtmosphericAudioController 用の環境音 ──────────────────
        string atmoDir = Path.Combine(Application.dataPath, ATMO_DIR);
        Directory.CreateDirectory(atmoDir);

        Save(atmoDir, "atmo_hum",   AtmoHum());    // clipHum
        Save(atmoDir, "atmo_hvac",  AtmoHvac());   // clipHvac
        Save(atmoDir, "atmo_drip",  AtmoDrip());   // clipDrip
        Save(atmoDir, "atmo_creak", AtmoCreak());  // clipCreak
        Save(atmoDir, "atmo_wind",  AtmoWind());   // clipWind
        Save(atmoDir, "atmo_storm", AtmoStorm());  // clipStorm

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "SFX Generator",
            "不足SFXの生成が完了しました。\n\n" +
            "・NightmareAssets/Audio/SFX/     ← ゲームSFX\n" +
            "・NightmareAssets/Audio/Ambient/ ← 環境音ループ\n\n" +
            "AtmosphericAudioController の Inspector で\n" +
            "atmo_*.wav を各 Clip フィールドにアサインしてください。",
            "OK");
    }

    static void Save(string dir, string name, float[] samples)
    {
        string path = Path.Combine(dir, name + ".wav");
        if (File.Exists(path))
        {
            Debug.Log($"[SFX] スキップ (既存): {name}.wav");
            return;
        }
        File.WriteAllBytes(path, WavEncode(samples, SR));
        Debug.Log($"[SFX] 生成: {name}.wav  ({samples.Length / (float)SR:F2}s)");
    }

    // ── Sound generators ────────────────────────────────────────────────────

    // ジャンプスケア: 白色雑音バースト + 高周波ダウンスイープ + 金属音
    static float[] JumpscareStinger()
    {
        const float DUR = 0.9f;
        var buf = Buf(DUR);
        var rng = new System.Random(1);
        float phase = 0f;
        for (int i = 0; i < buf.Length; i++)
        {
            float t  = T(i);
            float n  = t / DUR;
            float env = Mathf.Exp(-t * 3.5f);
            float noise = (float)(rng.NextDouble() * 2 - 1);
            // 3000Hz → 150Hz にスイープ
            float freq = Mathf.Lerp(3000f, 150f, n);
            phase += freq / SR;
            float sweep = Mathf.Sin(2f * Mathf.PI * phase);
            buf[i] = Clamp(env * (noise * 0.55f + sweep * 0.55f));
        }
        return buf;
    }

    // カメラ破壊: 電気的パチッ + ノイズフェードアウト
    static float[] CameraDestroyed()
    {
        const float DUR = 0.55f;
        var buf = Buf(DUR);
        var rng = new System.Random(2);
        for (int i = 0; i < buf.Length; i++)
        {
            float t   = T(i);
            float env = Mathf.Exp(-t * 9f);
            float noise = (float)(rng.NextDouble() * 2 - 1);
            // 最初30msの強いバースト
            float burst = t < 0.03f ? 2.2f : 1f;
            // 電気的ブザー音
            float buzz = Mathf.Sin(2f * Mathf.PI * 440f * t) * 0.3f;
            buf[i] = Clamp(env * (noise * burst + buzz));
        }
        return buf;
    }

    // 規則的なノック: 3回、等間隔 (0.6s おき)
    static float[] KnockRegular()
    {
        const float DUR = 2.8f;
        var buf = Buf(DUR);
        float[] times = { 0.3f, 0.9f, 1.5f };
        float[] freqs = { 170f, 170f, 170f };
        float[] vols  = { 0.9f, 0.9f, 0.9f };
        for (int k = 0; k < times.Length; k++)
            AddKnock(buf, times[k], vols[k], freqs[k], 22f);
        return buf;
    }

    // 不規則なノック: 5回、間隔・強さがバラバラ
    static float[] KnockIrregular()
    {
        const float DUR = 3.5f;
        var buf = Buf(DUR);
        float[] times = { 0.2f, 0.48f, 1.3f, 1.7f, 2.6f };
        float[] vols  = { 0.9f, 0.65f, 1.0f, 0.55f, 0.85f };
        float[] freqs = { 160f, 145f,  175f, 130f,  165f };
        for (int k = 0; k < times.Length; k++)
            AddKnock(buf, times[k], vols[k], freqs[k], 20f);
        return buf;
    }

    static void AddKnock(float[] buf, float startSec, float vol, float freq, float decay)
    {
        int start = (int)(startSec * SR);
        for (int i = start; i < buf.Length; i++)
        {
            float t = (float)(i - start) / SR;
            float env = Mathf.Exp(-t * decay);
            if (env < 0.001f) break;
            // 打撃 + 残響倍音
            float s = env * (Mathf.Sin(2f * Mathf.PI * freq * t)
                           + Mathf.Sin(2f * Mathf.PI * freq * 2.1f * t) * 0.3f) * vol;
            buf[i] = Clamp(buf[i] + s);
        }
    }

    // Lurker 出現: 低周波グロウル、フェードイン
    static float[] LurkerAppear()
    {
        const float DUR = 1.8f;
        var buf = Buf(DUR);
        var rng = new System.Random(5);
        for (int i = 0; i < buf.Length; i++)
        {
            float t   = T(i);
            float n   = t / DUR;
            float env = n < 0.3f ? n / 0.3f : 1f - (n - 0.3f) / 0.7f; // fade in/out
            float growl    = Mathf.Sin(2f * Mathf.PI * 58f  * t) * 0.45f;
            float harmonic = Mathf.Sin(2f * Mathf.PI * 116f * t) * 0.20f;
            float sub      = Mathf.Sin(2f * Mathf.PI * 29f  * t) * 0.25f;
            float noise    = (float)(rng.NextDouble() * 2 - 1) * 0.08f;
            buf[i] = Clamp(env * (growl + harmonic + sub + noise));
        }
        return buf;
    }

    // Rusher 足音: 重い衝撃 + ピッチダウン
    static float[] RusherStomp()
    {
        const float DUR = 0.45f;
        var buf = Buf(DUR);
        var rng = new System.Random(6);
        float phase = 0f;
        for (int i = 0; i < buf.Length; i++)
        {
            float t   = T(i);
            float env = Mathf.Exp(-t * 11f);
            // 120Hz → 30Hz へ急速ピッチダウン
            float freq = Mathf.Lerp(120f, 30f, Mathf.Clamp01(t * 10f));
            phase += freq / SR;
            float thump = Mathf.Sin(2f * Mathf.PI * phase);
            float noise = (float)(rng.NextDouble() * 2 - 1) * 0.18f;
            buf[i] = Clamp(env * (thump * 0.82f + noise));
        }
        return buf;
    }

    // 停電: ハムが下降して沈黙へ
    static float[] PowerOut()
    {
        const float DUR = 2.2f;
        var buf = Buf(DUR);
        float phase = 0f;
        for (int i = 0; i < buf.Length; i++)
        {
            float t   = T(i);
            float n   = t / DUR;
            float env = Mathf.Pow(1f - n, 1.5f);
            float freq = Mathf.Lerp(120f, 18f, n);
            phase += freq / SR;
            float hum = Mathf.Sin(2f * Mathf.PI * phase);
            float click = i < 80 ? 0.9f : 0f; // 冒頭の電気的パチッ
            buf[i] = Clamp(env * hum * 0.65f + click);
        }
        return buf;
    }

    // 電力復旧: ハムが立ち上がる
    static float[] PowerRestore()
    {
        const float DUR = 1.6f;
        var buf = Buf(DUR);
        float phase = 0f;
        for (int i = 0; i < buf.Length; i++)
        {
            float t   = T(i);
            float n   = t / DUR;
            float env = Mathf.Pow(n, 0.5f); // √カーブで素早く立ち上がり
            float freq = Mathf.Lerp(25f, 120f, n);
            phase += freq / SR;
            float hum = Mathf.Sin(2f * Mathf.PI * phase);
            // 倍音追加でリアリティ
            float hum2 = Mathf.Sin(2f * Mathf.PI * phase * 2f) * 0.2f;
            float click = i < 120 ? 0.7f : 0f;
            buf[i] = Clamp(env * (hum * 0.6f + hum2) + click);
        }
        return buf;
    }

    // フェイク足音: 遠くから聞こえる軽い足音
    static float[] FakeFootstep()
    {
        const float DUR = 0.38f;
        var buf = Buf(DUR);
        var rng = new System.Random(9);
        for (int i = 0; i < buf.Length; i++)
        {
            float t   = T(i);
            float env = Mathf.Exp(-t * 14f);
            float step  = Mathf.Sin(2f * Mathf.PI * 210f * t) * 0.45f;
            float noise = (float)(rng.NextDouble() * 2 - 1) * 0.32f;
            buf[i] = Clamp(env * (step + noise));
        }
        return buf;
    }

    // タイムワープ: 周波数変調の歪んだ音
    static float[] TimeWarp()
    {
        const float DUR = 1.6f;
        var buf = Buf(DUR);
        float phase = 0f;
        for (int i = 0; i < buf.Length; i++)
        {
            float t = T(i);
            float n = t / DUR;
            // 600Hz → 180Hz → 900Hz の不思議なカーブ
            float freq = n < 0.5f
                ? Mathf.Lerp(600f, 180f, n * 2f)
                : Mathf.Lerp(180f, 900f, (n - 0.5f) * 2f);
            // 揺らぎ (LFO 4Hz)
            freq += Mathf.Sin(2f * Mathf.PI * 4f * t) * 30f;
            phase += freq / SR;
            float env = Mathf.Sin(Mathf.PI * n); // ベル形エンベロープ
            float tone = Mathf.Sin(2f * Mathf.PI * phase);
            // リングモジュレーション風
            float ring = tone * Mathf.Sin(2f * Mathf.PI * 7f * t);
            buf[i] = Clamp(env * (tone * 0.55f + ring * 0.35f));
        }
        return buf;
    }

    // ゴーストシグナル: ラジオ干渉ノイズ + キャリア
    static float[] GhostSignal()
    {
        const float DUR = 1.1f;
        var buf = Buf(DUR);
        var rng = new System.Random(11);
        for (int i = 0; i < buf.Length; i++)
        {
            float t   = T(i);
            float n   = t / DUR;
            float env = Mathf.Sin(Mathf.PI * n); // 中央ピーク
            float noise   = (float)(rng.NextDouble() * 2 - 1);
            float carrier = Mathf.Sin(2f * Mathf.PI * 1400f * t);
            float carrier2= Mathf.Sin(2f * Mathf.PI * 1800f * t) * 0.4f;
            buf[i] = Clamp(env * (noise * 0.35f + carrier * 0.45f + carrier2));
        }
        return buf;
    }

    // 全停電: 強烈な衝撃 + 低周波ドスン
    static float[] Blackout()
    {
        const float DUR = 0.85f;
        var buf = Buf(DUR);
        var rng = new System.Random(12);
        float phase = 0f;
        for (int i = 0; i < buf.Length; i++)
        {
            float t   = T(i);
            float env = Mathf.Exp(-t * 5.5f);
            float noise = (float)(rng.NextDouble() * 2 - 1);
            float freq = Mathf.Lerp(80f, 25f, Mathf.Clamp01(t * 4f));
            phase += freq / SR;
            float thump = Mathf.Sin(2f * Mathf.PI * phase);
            buf[i] = Clamp(env * (noise * 0.28f + thump * 0.82f));
        }
        return buf;
    }

    // メニューアンビエンス: 不気味な低周波ドローン (ループ対応)
    static float[] MenuAmbience()
    {
        const float DUR = 6.0f;
        var buf = Buf(DUR);
        var rng = new System.Random(13);
        for (int i = 0; i < buf.Length; i++)
        {
            float t = T(i);
            float n = t / DUR;
            // 両端フェードでループ継ぎ目をなだらか
            float env = Mathf.Clamp01(Mathf.Sin(Mathf.PI * n) * 2f);
            float d1 = Mathf.Sin(2f * Mathf.PI * 55f   * t) * 0.38f;
            float d2 = Mathf.Sin(2f * Mathf.PI * 82.5f * t) * 0.22f; // 完全5度
            float d3 = Mathf.Sin(2f * Mathf.PI * 110f  * t) * 0.14f; // オクターブ
            float d4 = Mathf.Sin(2f * Mathf.PI * 27.5f * t) * 0.18f; // 1オクターブ下
            // ゆっくりした揺らぎ (0.3Hz)
            float mod = 1f + Mathf.Sin(2f * Mathf.PI * 0.3f * t) * 0.12f;
            float noise = (float)(rng.NextDouble() * 2 - 1) * 0.04f;
            buf[i] = Clamp(env * (d1 + d2 + d3 + d4 + noise) * mod);
        }
        return buf;
    }

    // ── Atmospheric audio (AtmosphericAudioController 用ループ素材) ──────────

    // 電気系ハム音ループ: 60Hz 基音 + 倍音で電磁ノイズ感 (シームレスループ)
    static float[] AtmoHum()
    {
        // 60Hzの整数倍=44100サンプルで正確に1周期が整数個入るよう長さを設定
        // 60Hzの周期 = 735サンプル → 60周期 = 44100サンプル = 1.0秒でループ
        const float DUR = 1.0f;
        var buf = Buf(DUR);
        var rng = new System.Random(20);
        for (int i = 0; i < buf.Length; i++)
        {
            float t = T(i);
            float h1 = Mathf.Sin(2f * Mathf.PI * 60f  * t) * 0.45f; // 基音
            float h2 = Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.20f; // 2倍音
            float h3 = Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.10f; // 3倍音
            float h4 = Mathf.Sin(2f * Mathf.PI * 240f * t) * 0.05f; // 4倍音
            float noise = (float)(rng.NextDouble() * 2 - 1) * 0.03f;
            buf[i] = Clamp(h1 + h2 + h3 + h4 + noise);
        }
        return buf;
    }

    // HVACファン音ループ: 帯域制限ホワイトノイズ + 低周波モーター音 (2秒ループ)
    static float[] AtmoHvac()
    {
        const float DUR = 2.0f;
        var buf = Buf(DUR);
        var rng = new System.Random(21);
        // 前段: ローパス近似のために隣接サンプルを平均するシンプルフィルタ
        var raw = new float[buf.Length];
        for (int i = 0; i < raw.Length; i++)
            raw[i] = (float)(rng.NextDouble() * 2 - 1);
        // 軽いスムージング (LPF近似)
        for (int i = 1; i < buf.Length - 1; i++)
            raw[i] = (raw[i - 1] + raw[i] * 2f + raw[i + 1]) / 4f;

        for (int i = 0; i < buf.Length; i++)
        {
            float t = T(i);
            float motor = Mathf.Sin(2f * Mathf.PI * 48f * t) * 0.15f; // モーター振動
            // 両端フェードでループのクリックを防ぐ
            float edge = Mathf.Clamp01(Mathf.Sin(Mathf.PI * (float)i / buf.Length) * 10f);
            buf[i] = Clamp((raw[i] * 0.35f + motor) * edge);
        }
        return buf;
    }

    // 水滴音: 鋭いアタック + 鈴のような余韻 (ワンショット)
    static float[] AtmoDrip()
    {
        const float DUR = 0.55f;
        var buf = Buf(DUR);
        for (int i = 0; i < buf.Length; i++)
        {
            float t = T(i);
            float env = Mathf.Exp(-t * 8f);
            // 水滴の共鳴 (1800Hz + 倍音)
            float drop = Mathf.Sin(2f * Mathf.PI * 1800f * t) * 0.5f
                       + Mathf.Sin(2f * Mathf.PI * 2700f * t) * 0.2f
                       + Mathf.Sin(2f * Mathf.PI * 900f  * t) * 0.15f;
            buf[i] = Clamp(env * drop);
        }
        return buf;
    }

    // 構造体きしみ音: 不規則なきしみ感 (ワンショット)
    static float[] AtmoCreak()
    {
        const float DUR = 0.6f;
        var buf = Buf(DUR);
        var rng = new System.Random(23);
        float phase = 0f;
        for (int i = 0; i < buf.Length; i++)
        {
            float t = T(i);
            float n = t / DUR;
            // 上昇→下降するきしみ音程
            float freq = n < 0.4f
                ? Mathf.Lerp(220f, 380f, n / 0.4f)
                : Mathf.Lerp(380f, 180f, (n - 0.4f) / 0.6f);
            // ゆらぎ追加
            freq += Mathf.Sin(2f * Mathf.PI * 12f * t) * 20f;
            phase += freq / SR;
            float env = Mathf.Sin(Mathf.PI * n) * Mathf.Exp(-n * 2.5f);
            float noise = (float)(rng.NextDouble() * 2 - 1) * 0.2f;
            buf[i] = Clamp(env * (Mathf.Sin(2f * Mathf.PI * phase) * 0.6f + noise));
        }
        return buf;
    }

    // 風ループ: 低周波バンドパスノイズ (4秒ループ)
    static float[] AtmoWind()
    {
        const float DUR = 4.0f;
        var buf = Buf(DUR);
        var rng = new System.Random(24);
        var raw = new float[buf.Length];
        for (int i = 0; i < raw.Length; i++)
            raw[i] = (float)(rng.NextDouble() * 2 - 1);
        // 複数段スムージングで低周波帯域のノイズに
        for (int pass = 0; pass < 12; pass++)
            for (int i = 1; i < raw.Length - 1; i++)
                raw[i] = (raw[i - 1] + raw[i] + raw[i + 1]) / 3f;

        for (int i = 0; i < buf.Length; i++)
        {
            float t = T(i);
            // 緩やかなうねり変調 (0.15Hz)
            float swell = 0.5f + Mathf.Sin(2f * Mathf.PI * 0.15f * t) * 0.35f;
            // ループ端のクリック防止
            float edge = Mathf.Clamp01(Mathf.Sin(Mathf.PI * (float)i / buf.Length) * 8f);
            buf[i] = Clamp(raw[i] * swell * edge * 0.8f);
        }
        return buf;
    }

    // 嵐ループ: 荒れた高密度ノイズ + 低周波うなり (4秒ループ)
    static float[] AtmoStorm()
    {
        const float DUR = 4.0f;
        var buf = Buf(DUR);
        var rng = new System.Random(25);
        var raw = new float[buf.Length];
        for (int i = 0; i < raw.Length; i++)
            raw[i] = (float)(rng.NextDouble() * 2 - 1);
        // 少なめのスムージング → 高周波成分を残す
        for (int pass = 0; pass < 5; pass++)
            for (int i = 1; i < raw.Length - 1; i++)
                raw[i] = (raw[i - 1] + raw[i] + raw[i + 1]) / 3f;

        for (int i = 0; i < buf.Length; i++)
        {
            float t = T(i);
            // 速いうねり変調 (0.4Hz) + 低周波ゴロゴロ
            float swell = 0.65f + Mathf.Sin(2f * Mathf.PI * 0.4f * t) * 0.3f;
            float rumble = Mathf.Sin(2f * Mathf.PI * 35f * t) * 0.25f;
            float edge = Mathf.Clamp01(Mathf.Sin(Mathf.PI * (float)i / buf.Length) * 8f);
            buf[i] = Clamp((raw[i] * swell + rumble) * edge);
        }
        return buf;
    }

    // ── Utility ─────────────────────────────────────────────────────────────

    static float[] Buf(float dur)   => new float[(int)(SR * dur)];
    static float   T(int i)         => (float)i / SR;
    static float   Clamp(float v)   => Mathf.Clamp(v, -1f, 1f);

    /// <summary>float[] → 16bit mono WAV バイト列</summary>
    static byte[] WavEncode(float[] samples, int sampleRate)
    {
        const int channels      = 1;
        const int bitsPerSample = 16;
        int byteRate   = sampleRate * channels * bitsPerSample / 8;
        int blockAlign = channels * bitsPerSample / 8;
        int dataSize   = samples.Length * blockAlign;

        using var ms = new MemoryStream(44 + dataSize);
        using var bw = new BinaryWriter(ms);

        // RIFF header
        bw.Write(Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + dataSize);
        bw.Write(Encoding.ASCII.GetBytes("WAVE"));
        // fmt chunk
        bw.Write(Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16);
        bw.Write((short)1);               // PCM
        bw.Write((short)channels);
        bw.Write(sampleRate);
        bw.Write(byteRate);
        bw.Write((short)blockAlign);
        bw.Write((short)bitsPerSample);
        // data chunk
        bw.Write(Encoding.ASCII.GetBytes("data"));
        bw.Write(dataSize);
        foreach (float s in samples)
            bw.Write((short)(s * short.MaxValue));

        return ms.ToArray();
    }
}
