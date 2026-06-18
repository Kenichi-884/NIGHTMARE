using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Reflection;
using System.Text;

/// <summary>
/// NIGHTMARE > WAV Trimmer
/// 波形を見ながら開始・終了位置をドラッグで調整し、WAV をトリムして上書き保存する。
///
/// 操作方法:
///   白いハンドル = 開始位置、黄色いハンドル = 終了位置
///   ドラッグ or 下部スライダーで移動
///   赤い領域 = 削除される部分、緑の波形 = 残る部分
/// </summary>
public class WavTrimmerWindow : EditorWindow
{
    [MenuItem("NIGHTMARE/WAV Trimmer")]
    public static void Open()
    {
        var w = GetWindow<WavTrimmerWindow>("WAV Trimmer");
        w.minSize = new Vector2(500, 320);
    }

    // ── オーディオデータ ─────────────────────────────────────────
    private AudioClip _clip;
    private float[]   _samples;              // 全サンプル（チャンネル込みインターリーブ）
    private float[]   _peaks;               // 波形描画用ピーク値（ピクセル列ごと）
    private int       _channels;
    private int       _sampleRate;
    private int       _totalPerCh;          // チャンネルあたりのサンプル数

    // ── トリム位置（0〜1 正規化）────────────────────────────────
    private float _trimStart = 0f;
    private float _trimEnd   = 1f;

    // ── テクスチャ ───────────────────────────────────────────────
    private Texture2D _tex;
    private int       _texWidth = 0;
    private const int WAVE_H = 110;

    // ── ドラッグ状態 ─────────────────────────────────────────────
    private enum Drag { None, Start, End }
    private Drag _drag = Drag.None;
    private const float GRAB_PX = 9f;

    // ── プレビュー用一時クリップ ─────────────────────────────────
    private AudioClip _previewClip;
    private bool      _isMp3;      // 元ファイルが MP3 かどうか

    // =====================================================================
    // GUI
    // =====================================================================
    private void OnGUI()
    {
        EditorGUILayout.Space(6);

        // ── クリップ選択 ──────────────────────────────────────────
        EditorGUI.BeginChangeCheck();
        _clip = (AudioClip)EditorGUILayout.ObjectField(
            "AudioClip", _clip, typeof(AudioClip), false);
        if (EditorGUI.EndChangeCheck())
        {
            if (_clip != null) LoadClip();
            else               Unload();
        }

        if (_clip == null)
        {
            EditorGUILayout.Space(12);
            EditorGUILayout.HelpBox("上の欄に AudioClip (.wav) をドロップしてください。", MessageType.Info);
            return;
        }

        EditorGUILayout.Space(6);

        // ── 波形エリア ────────────────────────────────────────────
        Rect wave = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
            GUILayout.Height(WAVE_H), GUILayout.ExpandWidth(true));

        // ウィンドウ幅変化でテクスチャを再生成
        int w = Mathf.Max(1, (int)wave.width);
        if (_tex == null || _texWidth != w)
            BuildTexture(w);

        if (_tex != null)
            GUI.DrawTexture(wave, _tex, ScaleMode.StretchToFill);

        DrawOverlay(wave);
        HandleMouse(wave);

        EditorGUILayout.Space(5);

        // ── 数値情報 ──────────────────────────────────────────────
        float total  = _totalPerCh / (float)_sampleRate;
        float sSec   = _trimStart * total;
        float eSec   = _trimEnd   * total;
        float durSec = eSec - sSec;

        using (new EditorGUILayout.HorizontalScope())
        {
            var s = new GUIStyle(EditorStyles.miniLabel);
            s.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
            GUILayout.Label($"元の長さ: {total:F3}s", s);
            GUILayout.FlexibleSpace();
            s.normal.textColor = Color.white;
            GUILayout.Label($"開始: {sSec:F3}s", s);
            s.normal.textColor = Color.yellow;
            GUILayout.Label($"終了: {eSec:F3}s", s);
            s.normal.textColor = new Color(0.4f, 1f, 0.6f);
            GUILayout.Label($"→ {durSec:F3}s 残る", s);
        }

        // ── スライダー ────────────────────────────────────────────
        EditorGUI.BeginChangeCheck();
        float ns = EditorGUILayout.Slider("開始 (s)", sSec, 0f, total);
        float ne = EditorGUILayout.Slider("終了 (s)", eSec, 0f, total);
        if (EditorGUI.EndChangeCheck())
        {
            _trimStart = Mathf.Clamp01(ns / total);
            _trimEnd   = Mathf.Clamp01(ne / total);
            ClampHandles(Drag.None);
            BuildTexture(w);
            Repaint();
        }

        EditorGUILayout.Space(8);

        // ── ボタン行 ──────────────────────────────────────────────
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("▶  プレビュー再生", GUILayout.Height(30)))
                PlayPreview();

            if (GUILayout.Button("■  停止", GUILayout.Height(30), GUILayout.Width(80)))
                StopPreview();

            if (GUILayout.Button("⟲  全体に戻す", GUILayout.Height(30), GUILayout.Width(110)))
            {
                _trimStart = 0f; _trimEnd = 1f;
                BuildTexture(w); Repaint();
            }

            GUILayout.FlexibleSpace();

            GUI.backgroundColor = new Color(1f, 0.55f, 0.55f);
            if (GUILayout.Button("✂  トリム保存 (上書き)", GUILayout.Height(30), GUILayout.Width(170)))
                ApplyTrim();
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.Space(4);

        if (_isMp3)
            EditorGUILayout.HelpBox(
                "MP3 ファイルは PCM デコード済みデータで .wav として保存されます。\n" +
                "保存先: 同フォルダに「元のファイル名.wav」で新規作成されます。",
                MessageType.Info);

        DrawLegend();
    }

    // =====================================================================
    // クリップ読み込み
    // =====================================================================
    private void LoadClip()
    {
        _channels   = _clip.channels;
        _sampleRate = _clip.frequency;
        _totalPerCh = _clip.samples;
        _trimStart  = 0f;
        _trimEnd    = 1f;

        _samples = new float[_totalPerCh * _channels];
        _clip.GetData(_samples, 0);

        string ap = AssetDatabase.GetAssetPath(_clip);
        _isMp3 = ap.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase);

        _tex = null;
        Repaint();
    }

    private void Unload()
    {
        StopPreview();
        _samples = null;
        _peaks   = null;
        if (_tex != null) { DestroyImmediate(_tex); _tex = null; }
    }

    // =====================================================================
    // 波形テクスチャ生成
    // =====================================================================
    private void BuildTexture(int w)
    {
        _texWidth = w;

        if (_samples == null) return;

        // ピーク値計算
        _peaks = new float[w];
        float step = (float)_totalPerCh / w;
        for (int px = 0; px < w; px++)
        {
            int s0 = Mathf.Clamp((int)(px * step),       0, _totalPerCh - 1);
            int s1 = Mathf.Clamp((int)((px + 1) * step), s0 + 1, _totalPerCh);
            float peak = 0f;
            for (int s = s0; s < s1; s++)
                for (int c = 0; c < _channels; c++)
                    peak = Mathf.Max(peak, Mathf.Abs(_samples[s * _channels + c]));
            _peaks[px] = peak;
        }

        // テクスチャ描画
        if (_tex != null) DestroyImmediate(_tex);
        _tex = new Texture2D(w, WAVE_H, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point };

        Color bgDark  = new Color(0.10f, 0.10f, 0.10f);
        Color bgLight = new Color(0.16f, 0.16f, 0.16f);
        Color wKeep   = new Color(0.28f, 0.82f, 0.42f);   // 緑：残す
        Color wCut    = new Color(0.60f, 0.18f, 0.18f);   // 赤：削除
        Color center  = new Color(0.35f, 0.35f, 0.35f);

        int startPx = (int)(_trimStart * w);
        int endPx   = (int)(_trimEnd   * w);
        int midY    = WAVE_H / 2;

        for (int x = 0; x < w; x++)
        {
            bool inRange = x >= startPx && x < endPx;
            Color wc  = inRange ? wKeep : wCut;
            Color bg  = inRange ? bgLight : bgDark;
            int   hh  = Mathf.RoundToInt(_peaks[x] * (midY - 2));

            for (int y = 0; y < WAVE_H; y++)
            {
                int dist = Mathf.Abs(y - midY);
                Color px = (y == midY) ? center
                         : (dist <= hh) ? wc
                         : bg;
                _tex.SetPixel(x, y, px);
            }
        }

        _tex.Apply();
    }

    // =====================================================================
    // ハンドルオーバーレイ描画
    // =====================================================================
    private void DrawOverlay(Rect r)
    {
        if (Event.current.type != EventType.Repaint) return;

        float sx = r.x + _trimStart * r.width;
        float ex = r.x + _trimEnd   * r.width;

        // 削除領域の半透明オーバーレイ
        var cutTint = new Color(0.7f, 0.1f, 0.1f, 0.28f);
        if (_trimStart > 0.001f)
            EditorGUI.DrawRect(new Rect(r.x, r.y, sx - r.x, r.height), cutTint);
        if (_trimEnd < 0.999f)
            EditorGUI.DrawRect(new Rect(ex, r.y, r.xMax - ex, r.height), cutTint);

        // 開始ハンドル（白）
        EditorGUI.DrawRect(new Rect(sx - 1.5f, r.y,      3f,  r.height), Color.white);
        EditorGUI.DrawRect(new Rect(sx - 1.5f, r.y,      9f,  6f),       Color.white);  // 上部▶
        EditorGUI.DrawRect(new Rect(sx - 1.5f, r.yMax-6, 9f,  6f),       Color.white);  // 下部▶

        // 終了ハンドル（黄）
        EditorGUI.DrawRect(new Rect(ex - 1.5f, r.y,      3f,  r.height), Color.yellow);
        EditorGUI.DrawRect(new Rect(ex - 7.5f, r.y,      9f,  6f),       Color.yellow);
        EditorGUI.DrawRect(new Rect(ex - 7.5f, r.yMax-6, 9f,  6f),       Color.yellow);

        // 選択領域 上部ライン
        EditorGUI.DrawRect(new Rect(sx, r.y, ex - sx, 2f), new Color(1f, 1f, 1f, 0.35f));

        // 時刻ラベル
        float total = _totalPerCh / (float)_sampleRate;
        var ls = new GUIStyle(EditorStyles.miniLabel);

        ls.normal.textColor = Color.white;
        GUI.Label(new Rect(sx + 4, r.y + 3, 70, 16), $"{_trimStart * total:F3}s", ls);

        ls.normal.textColor = Color.yellow;
        string eLabel = $"{_trimEnd * total:F3}s";
        GUI.Label(new Rect(ex - 62, r.y + 3, 60, 16), eLabel, ls);
    }

    // =====================================================================
    // マウスイベント
    // =====================================================================
    private void HandleMouse(Rect r)
    {
        Event e = Event.current;
        float mx = e.mousePosition.x;

        float sx = r.x + _trimStart * r.width;
        float ex = r.x + _trimEnd   * r.width;

        bool nearS = Mathf.Abs(mx - sx) <= GRAB_PX;
        bool nearE = Mathf.Abs(mx - ex) <= GRAB_PX;

        if (nearS || nearE || _drag != Drag.None)
            EditorGUIUtility.AddCursorRect(r, MouseCursor.ResizeHorizontal);

        if (e.type == EventType.MouseDown && r.Contains(e.mousePosition))
        {
            // 近い方を優先（両方同時に近い場合は終点）
            if (nearE)       _drag = Drag.End;
            else if (nearS)  _drag = Drag.Start;
            e.Use();
        }

        if (e.type == EventType.MouseDrag && _drag != Drag.None)
        {
            float n = Mathf.Clamp01((mx - r.x) / r.width);
            if (_drag == Drag.Start) _trimStart = n;
            else                     _trimEnd   = n;
            ClampHandles(_drag);
            BuildTexture(_texWidth);
            Repaint();
            e.Use();
        }

        if (e.type == EventType.MouseUp && _drag != Drag.None)
        {
            _drag = Drag.None;
            e.Use();
        }
    }

    private void ClampHandles(Drag moved)
    {
        _trimStart = Mathf.Clamp01(_trimStart);
        _trimEnd   = Mathf.Clamp01(_trimEnd);
        const float MIN_GAP = 0.001f;
        if (_trimEnd - _trimStart < MIN_GAP)
        {
            if (moved == Drag.Start) _trimStart = _trimEnd   - MIN_GAP;
            else                     _trimEnd   = _trimStart + MIN_GAP;
        }
    }

    // =====================================================================
    // プレビュー再生
    // =====================================================================
    private void PlayPreview()
    {
        if (_samples == null) return;
        StopPreview();

        int s0  = Mathf.Clamp((int)(_trimStart * _totalPerCh), 0, _totalPerCh - 1);
        int s1  = Mathf.Clamp((int)(_trimEnd   * _totalPerCh), s0 + 1, _totalPerCh);
        int len = s1 - s0;

        float[] buf = new float[len * _channels];
        Array.Copy(_samples, s0 * _channels, buf, 0, len * _channels);

        _previewClip = AudioClip.Create("__WavTrim_Preview__", len, _channels, _sampleRate, false);
        _previewClip.SetData(buf, 0);
        AudioUtilPlay(_previewClip);
    }

    private void StopPreview()
    {
        AudioUtilStop();
        if (_previewClip != null) { DestroyImmediate(_previewClip); _previewClip = null; }
    }

    // UnityEditor.AudioUtil (リフレクション)
    private static readonly Type _audioUtil =
        typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");

    private static void AudioUtilPlay(AudioClip clip)
    {
        try
        {
            var mi = _audioUtil?.GetMethod("PlayPreviewClip",
                BindingFlags.Static | BindingFlags.Public, null,
                new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
            mi?.Invoke(null, new object[] { clip, 0, false });
        }
        catch (Exception ex) { Debug.LogWarning($"[WavTrimmer] プレビュー再生失敗: {ex.Message}"); }
    }

    private static void AudioUtilStop()
    {
        try
        {
            var mi = _audioUtil?.GetMethod("StopAllPreviewClips",
                BindingFlags.Static | BindingFlags.Public);
            mi?.Invoke(null, null);
        }
        catch { }
    }

    // =====================================================================
    // トリム保存
    // =====================================================================
    private void ApplyTrim()
    {
        if (_samples == null || _clip == null) return;

        string srcAsset = AssetDatabase.GetAssetPath(_clip);
        if (string.IsNullOrEmpty(srcAsset))
        {
            EditorUtility.DisplayDialog("WAV Trimmer", "アセットパスを取得できません。", "OK");
            return;
        }

        bool isMp3 = srcAsset.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase);
        bool isWav = srcAsset.EndsWith(".wav", StringComparison.OrdinalIgnoreCase);
        if (!isMp3 && !isWav)
        {
            EditorUtility.DisplayDialog("WAV Trimmer",
                ".wav / .mp3 ファイルのみ対応しています。", "OK");
            return;
        }

        // 保存先アセットパスを決定
        // WAV → 同パスに上書き / MP3 → 同フォルダに .wav で新規保存
        string saveAsset = isMp3
            ? Path.ChangeExtension(srcAsset, ".wav")
            : srcAsset;

        float total = _totalPerCh / (float)_sampleRate;
        float sSec  = _trimStart * total;
        float eSec  = _trimEnd   * total;

        string msg = isMp3
            ? $"MP3 → WAV に変換して保存します\n\n保存先: {saveAsset}\n  {sSec:F3}s 〜 {eSec:F3}s  ({eSec - sSec:F3}s)\n\n続けますか？"
            : $"元のファイルを上書き保存します\n\nファイル: {Path.GetFileName(saveAsset)}\n  {sSec:F3}s 〜 {eSec:F3}s  ({eSec - sSec:F3}s)\n\n元に戻せません。続けますか？";

        if (!EditorUtility.DisplayDialog("WAV Trimmer", msg, "保存する", "キャンセル"))
            return;

        int s0  = Mathf.Clamp((int)(_trimStart * _totalPerCh), 0, _totalPerCh - 1);
        int s1  = Mathf.Clamp((int)(_trimEnd   * _totalPerCh), s0 + 1, _totalPerCh);
        int len = s1 - s0;

        float[] buf = new float[len * _channels];
        Array.Copy(_samples, s0 * _channels, buf, 0, len * _channels);

        string root     = Path.GetDirectoryName(Application.dataPath) ?? "";
        string fullPath = Path.GetFullPath(Path.Combine(root,
            saveAsset.Replace('/', Path.DirectorySeparatorChar)));

        File.WriteAllBytes(fullPath, WavEncode(buf, _sampleRate, _channels));
        AssetDatabase.ImportAsset(saveAsset);

        // 保存後は新しいアセットをロード
        _clip = AssetDatabase.LoadAssetAtPath<AudioClip>(saveAsset);
        if (_clip != null) LoadClip();

        EditorUtility.DisplayDialog("WAV Trimmer",
            $"保存しました。\n{saveAsset}", "OK");
    }

    // =====================================================================
    // WAV エンコーダ（16bit PCM）
    // =====================================================================
    private static byte[] WavEncode(float[] samples, int sr, int ch)
    {
        const int bps = 16;
        int byteRate   = sr * ch * bps / 8;
        int blockAlign = ch * bps / 8;
        int dataSize   = samples.Length * blockAlign / ch;   // samples already interleaved

        // samples.Length = sampleCount * ch → dataSize = samples.Length * 2
        dataSize = samples.Length * 2;

        using var ms = new MemoryStream(44 + dataSize);
        using var bw = new BinaryWriter(ms);
        bw.Write(Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + dataSize);
        bw.Write(Encoding.ASCII.GetBytes("WAVE"));
        bw.Write(Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16);
        bw.Write((short)1);
        bw.Write((short)ch);
        bw.Write(sr);
        bw.Write(byteRate);
        bw.Write((short)blockAlign);
        bw.Write((short)bps);
        bw.Write(Encoding.ASCII.GetBytes("data"));
        bw.Write(dataSize);
        foreach (float s in samples)
            bw.Write((short)(Mathf.Clamp(s, -1f, 1f) * short.MaxValue));
        return ms.ToArray();
    }

    // =====================================================================
    // 凡例
    // =====================================================================
    private void DrawLegend()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            var s = new GUIStyle(EditorStyles.miniLabel);

            DrawColorBox(new Color(0.28f, 0.82f, 0.42f));
            s.normal.textColor = new Color(0.6f, 1f, 0.7f);
            GUILayout.Label("残す部分 (緑)", s, GUILayout.Width(90));

            DrawColorBox(new Color(0.60f, 0.18f, 0.18f));
            s.normal.textColor = new Color(1f, 0.6f, 0.6f);
            GUILayout.Label("削除される部分 (赤)", s, GUILayout.Width(130));

            s.normal.textColor = Color.white;
            GUILayout.Label("│  白ハンドル = 開始", s, GUILayout.Width(130));
            s.normal.textColor = Color.yellow;
            GUILayout.Label("│  黄ハンドル = 終了", s);
        }
    }

    private void DrawColorBox(Color c)
    {
        Rect r = GUILayoutUtility.GetRect(12, 12, GUILayout.Width(12), GUILayout.Height(12));
        EditorGUI.DrawRect(r, c);
        GUILayout.Space(3);
    }

    private void OnDestroy()
    {
        StopPreview();
        if (_tex != null) DestroyImmediate(_tex);
    }
}
