using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [System.Serializable]
    public class SoundEntry
    {
        public string key;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
    }

    // ─── Manifest (推奨: Inspector でアサイン) ────────────────────
    [Header("Audio Manifest (推奨)")]
    [Tooltip("AudioManifest をアサインすると BGM/SFX を Inspector 側で管理できます。\n" +
             "null の場合は以下のレガシーフィールドを使用します。")]
    [SerializeField] private AudioManifest manifest;

    // ─── AudioSources ────────────────────────────────────────────
    [Header("Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource loopSource;  // 環境音・ループSE専用

    // ─── レガシー BGM フィールド (manifest が null の場合に使用) ──
    [Header("BGM per Phase (manifest がない場合に使用)")]
    [SerializeField] private AudioClip bgmSilence;
    [SerializeField] private AudioClip bgmOmen;
    [SerializeField] private AudioClip bgmContact;
    [SerializeField] private AudioClip bgmIntense;
    [SerializeField] private AudioClip bgmCritical;
    [SerializeField] private AudioClip bgmFinalNight;
    [SerializeField] private AudioClip bgmClear;
    [SerializeField] private AudioClip bgmGameOver;

    [Header("BGM Crossfade")]
    [SerializeField] private float crossfadeDuration = 1.5f;

    // ─── レガシー SFX リスト ────────────────────────────────────
    [Header("SFX (manifest がない場合に使用)")]
    [SerializeField] private List<SoundEntry> sfxList = new List<SoundEntry>();

    private readonly Dictionary<string, SoundEntry> sfxDict = new Dictionary<string, SoundEntry>();
    private int currentDay = 1;
    private Coroutine crossfadeRoutine;
    private float bgmVolume = 0.8f;
    private float sfxVolume = 1.0f;

    private static readonly string[] AllSfxKeys =
    {
        "heartbeat", "jumpscare_stinger", "camera_destroyed", "camera_static",
        "knock_regular", "knock_irregular", "lurker_appear", "rusher_stomp",
        "power_out", "power_restore", "power_flicker", "fake_footstep",
        "time_warp", "ghost_signal", "blackout", "menu_ambience",
        "door_close", "door_open", "button_click",
    };

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        var sources = GetComponents<AudioSource>();
        if (bgmSource  == null && sources.Length > 0) bgmSource  = sources[0];
        if (sfxSource  == null && sources.Length > 1) sfxSource  = sources[1];
        if (loopSource == null && sources.Length > 2) loopSource = sources[2];

        // loopSource が取れなければ追加
        if (loopSource == null)
        {
            loopSource = gameObject.AddComponent<AudioSource>();
            loopSource.loop       = true;
            loopSource.playOnAwake = false;
        }

        // レガシー SFX テーブル補完
        if (manifest == null)
        {
            var existingKeys = new HashSet<string>();
            foreach (var s in sfxList) existingKeys.Add(s.key);
            foreach (var key in AllSfxKeys)
                if (!existingKeys.Contains(key))
                    sfxList.Add(new SoundEntry { key = key, volume = 1f });
            foreach (var s in sfxList) sfxDict[s.key] = s;
        }
    }

    private void Start()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("[AudioManager] GameManager が見つかりません。BGM/SFXイベントのSubscribeをスキップします。");
            return;
        }
        GameManager.Instance.OnPhaseChanged += OnPhaseChanged;
        GameManager.Instance.OnDayStarted   += d => { currentDay = d; };
        GameManager.Instance.OnGameOver      += () => CrossfadeBGM(ResolveBgmForEvent("gameover"), 0.8f);
        GameManager.Instance.OnTrueEnding    += () => CrossfadeBGM(ResolveBgmForEvent("clear"), 1.5f);
        GameManager.Instance.OnNightCleared  += _ => CrossfadeBGM(ResolveBgmForEvent("clear"), 1.5f);
    }

    private void OnPhaseChanged(GamePhase phase)
    {
        AudioClip target = null;
        float fadeDur = manifest?.crossfadeDuration ?? crossfadeDuration;

        // manifest 優先
        if (manifest != null)
        {
            var entry = manifest.GetBgm(phase, currentDay);
            if (entry != null) { target = entry.clip; fadeDur = manifest.crossfadeDuration; }
        }

        // manifest なし → レガシー
        if (target == null)
        {
            if (currentDay == 7 && phase >= GamePhase.Contact)
            {
                target = bgmFinalNight;
            }
            else
            {
                target = phase switch
                {
                    GamePhase.Silence      => bgmSilence,
                    GamePhase.Omen         => bgmOmen,
                    GamePhase.Contact      => bgmContact,
                    GamePhase.Increase     => bgmContact,
                    GamePhase.Erosion      => bgmIntense,
                    GamePhase.Infiltration => bgmIntense,
                    GamePhase.Siege        => bgmIntense,
                    GamePhase.Collapse     => bgmCritical,
                    GamePhase.Abyss        => bgmCritical,
                    GamePhase.BeforeDawn   => bgmCritical,
                    _ => null
                };
            }
        }

        if (target != null) CrossfadeBGM(target, fadeDur);
    }

    // "gameover" / "clear" など汎用イベントBGMを取得
    private AudioClip ResolveBgmForEvent(string eventId)
    {
        if (manifest != null)
        {
            foreach (var e in manifest.bgm)
                if (e.id == eventId && e.clip != null) return e.clip;
        }
        return eventId switch
        {
            "gameover" => bgmGameOver,
            "clear"    => bgmClear,
            _ => null
        };
    }

    private void CrossfadeBGM(AudioClip next, float fadeDur)
    {
        if (next == null || (bgmSource.clip == next && bgmSource.isPlaying)) return;
        if (crossfadeRoutine != null) StopCoroutine(crossfadeRoutine);
        crossfadeRoutine = StartCoroutine(CrossfadeRoutine(next, fadeDur));
    }

    private IEnumerator CrossfadeRoutine(AudioClip next, float dur)
    {
        float half = dur * 0.5f;

        if (bgmSource.isPlaying)
        {
            float start = bgmSource.volume;
            float t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                bgmSource.volume = Mathf.Lerp(start, 0f, t / half);
                yield return null;
            }
        }

        bgmSource.Stop();
        bgmSource.clip   = next;
        bgmSource.loop   = true;
        bgmSource.volume = 0f;
        bgmSource.Play();

        float t2 = 0f;
        while (t2 < half)
        {
            t2 += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(0f, bgmVolume, t2 / half);
            yield return null;
        }
        bgmSource.volume = bgmVolume;
    }

    // ─── 再生 API ────────────────────────────────────────────────
    public void Play(string key)
    {
        AudioClip clip  = null;
        float     vol   = sfxVolume;

        if (manifest != null)
        {
            var e = manifest.GetSfx(key);
            if (e?.clip != null) { clip = e.clip; vol = e.volume * sfxVolume; }
        }

        if (clip == null && sfxDict.TryGetValue(key, out var legacy) && legacy.clip != null)
        {
            clip = legacy.clip;
            vol  = legacy.volume * sfxVolume;
        }

        if (clip != null) sfxSource.PlayOneShot(clip, vol);
    }

    // ループ SE (loopSource 専用 AudioSource を使うため PlayOneShot と競合しない)
    public void PlayLoop(string key)
    {
        AudioClip clip = null;
        float     vol  = sfxVolume;

        if (manifest != null)
        {
            var e = manifest.GetSfx(key);
            if (e?.clip != null) { clip = e.clip; vol = e.volume * sfxVolume; }
        }
        if (clip == null && sfxDict.TryGetValue(key, out var legacy) && legacy.clip != null)
        {
            clip = legacy.clip;
            vol  = legacy.volume * sfxVolume;
        }
        if (clip == null) return;

        loopSource.clip   = clip;
        loopSource.volume = vol;
        loopSource.loop   = true;
        loopSource.Play();
    }

    public void StopLoop()
    {
        loopSource.Stop();
        loopSource.clip = null;
    }

    public void SetBGMVolume(float v)
    {
        bgmVolume = Mathf.Clamp01(v);
        if (bgmSource) bgmSource.volume = bgmVolume;
    }

    public void SetSFXVolume(float v)
    {
        sfxVolume = Mathf.Clamp01(v);
        if (sfxSource)  sfxSource.volume  = sfxVolume;
        if (loopSource) loopSource.volume = sfxVolume;
    }
}
