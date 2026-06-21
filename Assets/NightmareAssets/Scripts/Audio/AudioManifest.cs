using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// BGM / SFX をまとめる ScriptableObject。
///
/// ─ 使い方 ─────────────────────────────────────────────────────────
/// 【初回生成】
///   NIGHTMARE > Generate Audio Manifest → Assets/Data/AudioManifest.asset が作られる
///
/// 【BGM の追加・差し替え】
///   bgm リストにエントリを追加し phases / fromDay / clip を設定する。
///   同じフェーズに複数エントリがある場合、day7Only > fromDay が高い方が優先される。
///
/// 【SFX の追加】
///   sfx リストに { key, clip } を追加するだけ。
///   コード側は AudioManager.Instance.Play("新しいkey") で呼べる。
///
/// 【AudioManager への登録】
///   AudioManager Inspector の "Manifest" スロットにこのアセットをアサインする。
/// ─────────────────────────────────────────────────────────────────
/// </summary>
[CreateAssetMenu(fileName = "AudioManifest", menuName = "NIGHTMARE/Audio Manifest")]
public class AudioManifest : ScriptableObject
{
    // ─── BGM エントリ ────────────────────────────────────────────
    [System.Serializable]
    public class BgmEntry
    {
        [Tooltip("ユニーク ID (例: silence / omen / contact / final)")]
        public string id;

        [Tooltip("このBGMが再生されるフェーズ。複数指定可。空=全フェーズ")]
        public GamePhase[] phases;

        [Tooltip("このDay以降に適用する (0 = 全日)")]
        public int fromDay = 0;

        [Tooltip("Day7 専用 BGM。true の場合 Day7 のみ使用され他の条件より優先される")]
        public bool day7Only = false;

        public AudioClip clip;

        [Range(0f, 1f)]
        public float volume = 0.8f;
    }

    // ─── SFX エントリ ────────────────────────────────────────────
    [System.Serializable]
    public class SfxEntry
    {
        [Tooltip("Play(key) や PlayLoop(key) で指定する文字列キー")]
        public string key;

        public AudioClip clip;

        [Range(0f, 1f)]
        public float volume = 1f;
    }

    // ─── フィールド ──────────────────────────────────────────────
    [Header("── BGM ─────────────────────────────────────────────")]
    [Tooltip("フェーズ別の BGM リスト。フェーズが変わると AudioManager が自動でクロスフェードする。")]
    public List<BgmEntry> bgm = new();

    [Header("── SFX ─────────────────────────────────────────────")]
    [Tooltip("SE テーブル。AudioManager.Play(key) で再生できる。")]
    public List<SfxEntry> sfx = new();

    [Header("── クロスフェード設定 ──────────────────────────────────")]
    [Range(0.1f, 5f)]
    [Tooltip("BGM 切り替え時のクロスフェード秒数")]
    public float crossfadeDuration = 1.5f;

    // ─── 実行時ルックアップ（遅延構築）──────────────────────────
    private Dictionary<string, SfxEntry> _sfxDict;
    private bool _built;

    private void Build()
    {
        _sfxDict = new Dictionary<string, SfxEntry>();
        foreach (var e in sfx)
            if (!string.IsNullOrEmpty(e.key)) _sfxDict[e.key] = e;
        _built = true;
    }

    public SfxEntry GetSfx(string key)
    {
        if (!_built) Build();
        return _sfxDict.TryGetValue(key, out var e) ? e : null;
    }

    /// <summary>
    /// フェーズ・日数にマッチする BGM エントリを返す。
    /// 優先度: day7Only > fromDay（高い方） > phases のみ
    /// </summary>
    public BgmEntry GetBgm(GamePhase phase, int day)
    {
        if (bgm == null || bgm.Count == 0) return null;

        BgmEntry best = null;
        int bestScore = -1;

        foreach (var e in bgm)
        {
            if (e.clip == null) continue;
            // phases が空のエントリはタイトル・ゲームオーバー等のオンデマンド用 → スキップ
            if (e.phases == null || e.phases.Length == 0) continue;
            if (e.day7Only && day != 7) continue;
            if (e.fromDay > 0 && day < e.fromDay) continue;

            bool match = false;
            foreach (var p in e.phases) if (p == phase) { match = true; break; }
            if (!match) continue;

            // スコア計算（高いほど優先）
            int score = 0;
            if (e.day7Only) score += 100;
            score += e.fromDay;  // fromDay が大きいほど条件が厳しい → 優先

            if (score > bestScore) { best = e; bestScore = score; }
        }

        return best;
    }

    // ScriptableObject が更新されたときルックアップをリセット
    private void OnValidate() => _built = false;
}
