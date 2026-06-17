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

    [Header("Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("BGM per Phase")]
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

    [Header("SFX")]
    [SerializeField] private List<SoundEntry> sfxList = new List<SoundEntry>();

    private readonly Dictionary<string, SoundEntry> sfxDict = new Dictionary<string, SoundEntry>();
    private int currentDay = 1;
    private Coroutine crossfadeRoutine;
    private float bgmVolume = 0.8f;

    // すべてのSFXキー (インスペクタで AudioClip を割り当てる)
    private static readonly string[] AllSfxKeys =
    {
        "heartbeat",
        "jumpscare_stinger",
        "camera_destroyed",
        "camera_static",
        "knock_regular",
        "knock_irregular",
        "lurker_appear",
        "rusher_stomp",
        "power_out",
        "power_restore",
        "power_flicker",
        "fake_footstep",
        "time_warp",
        "ghost_signal",
        "blackout",
        "menu_ambience",
        "door_close",
        "door_open",
        "button_click",
    };

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // AudioSourceが未割り当ての場合、GetComponentsで自動取得（セットアップ後にInspector割当が不要）
        var sources = GetComponents<AudioSource>();
        if (bgmSource == null && sources.Length > 0) bgmSource = sources[0];
        if (sfxSource == null && sources.Length > 1) sfxSource = sources[1];

        // デフォルトエントリ補完（インスペクタ未設定のキーを埋める）
        var existingKeys = new HashSet<string>();
        foreach (var s in sfxList) existingKeys.Add(s.key);

        foreach (var key in AllSfxKeys)
        {
            if (!existingKeys.Contains(key))
                sfxList.Add(new SoundEntry { key = key, volume = 1f });
        }

        foreach (var s in sfxList) sfxDict[s.key] = s;
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
        GameManager.Instance.OnGameOver      += () => CrossfadeBGM(bgmGameOver, 0.8f);
        GameManager.Instance.OnTrueEnding    += () => CrossfadeBGM(bgmClear, 1.5f);
        GameManager.Instance.OnNightCleared  += _ => CrossfadeBGM(bgmClear, 1.5f);
    }

    private void OnPhaseChanged(GamePhase phase)
    {
        AudioClip target;

        if (currentDay == 7 && phase >= GamePhase.Contact)
        {
            target = bgmFinalNight;
        }
        else
        {
            target = phase switch
            {
                GamePhase.Silence       => bgmSilence,
                GamePhase.Omen          => bgmOmen,
                GamePhase.Contact       => bgmContact,
                GamePhase.Increase      => bgmContact,
                GamePhase.Erosion       => bgmIntense,
                GamePhase.Infiltration  => bgmIntense,
                GamePhase.Siege         => bgmIntense,
                GamePhase.Collapse      => bgmCritical,
                GamePhase.Abyss         => bgmCritical,
                GamePhase.BeforeDawn    => bgmCritical,
                _ => null
            };
        }

        if (target != null) CrossfadeBGM(target, crossfadeDuration);
    }

    // クロスフェードでBGM切替
    private void CrossfadeBGM(AudioClip next, float fadeDur)
    {
        if (next == null || bgmSource.clip == next) return;
        if (crossfadeRoutine != null) StopCoroutine(crossfadeRoutine);
        crossfadeRoutine = StartCoroutine(CrossfadeRoutine(next, fadeDur));
    }

    private IEnumerator CrossfadeRoutine(AudioClip next, float dur)
    {
        float half = dur * 0.5f;

        // フェードアウト
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
        bgmSource.clip = next;
        bgmSource.loop = true;
        bgmSource.volume = 0f;
        bgmSource.Play();

        // フェードイン
        float t2 = 0f;
        while (t2 < half)
        {
            t2 += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(0f, bgmVolume, t2 / half);
            yield return null;
        }
        bgmSource.volume = bgmVolume;
    }

    public void Play(string key)
    {
        if (sfxDict.TryGetValue(key, out var entry) && entry.clip != null)
            sfxSource.PlayOneShot(entry.clip, entry.volume);
    }

    public void SetBGMVolume(float v)
    {
        bgmVolume = v;
        if (bgmSource) bgmSource.volume = v;
    }

    public void SetSFXVolume(float v) { if (sfxSource) sfxSource.volume = v; }

    // 環境音などで特定のSFXをループ再生
    public void PlayLoop(string key)
    {
        if (sfxDict.TryGetValue(key, out var entry) && entry.clip != null)
        {
            sfxSource.clip = entry.clip;
            sfxSource.loop = true;
            sfxSource.Play();
        }
    }

    public void StopLoop() => sfxSource.Stop();
}
