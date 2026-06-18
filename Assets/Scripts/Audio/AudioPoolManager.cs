using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 16スロットのAudioSourceプール。
/// 2D / 3D再生・ピッチ/音量ランダム化・多重再生をサポート。
/// AudioManager.Play() の代替として使用し、同時発音数を正確に管理する。
/// </summary>
public class AudioPoolManager : MonoBehaviour
{
    public static AudioPoolManager Instance { get; private set; }

    [Header("Pool")]
    [SerializeField] private int poolSize = 16;
    [SerializeField] private AudioMixerGroup sfxMixerGroup;

    [Header("Variation")]
    [SerializeField, Range(0f, 0.15f)] private float pitchVariance  = 0.05f;
    [SerializeField, Range(0f, 0.20f)] private float volumeVariance = 0.08f;

    private AudioSource[] _pool;
    private int           _head;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _pool = new AudioSource[poolSize];
        for (int i = 0; i < poolSize; i++)
        {
            var go  = new GameObject($"SFX_{i:00}");
            go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake  = false;
            src.spatialBlend = 0f;
            if (sfxMixerGroup) src.outputAudioMixerGroup = sfxMixerGroup;
            _pool[i] = src;
        }
    }

    // ─── Public API ────────────────────────────────────────────────

    /// <summary>2D ワンショット再生（ピッチ/音量ランダム付き）。</summary>
    public AudioSource Play2D(AudioClip clip, float volume = 1f, float pitchMult = 1f)
    {
        if (!clip) return null;
        var src = Acquire();
        Setup2D(src, clip, volume, pitchMult);
        src.Play();
        return src;
    }

    /// <summary>3D ワールド座標でワンショット再生。</summary>
    public AudioSource Play3D(AudioClip clip, Vector3 worldPos,
                               float volume  = 1f,
                               float minDist = 2f,
                               float maxDist = 35f,
                               float pitchMult = 1f)
    {
        if (!clip) return null;
        var src = Acquire();
        src.transform.position = worldPos;
        Setup3D(src, clip, volume, pitchMult, minDist, maxDist);
        src.Play();
        return src;
    }

    // ─── Internals ─────────────────────────────────────────────────

    private AudioSource Acquire()
    {
        for (int i = 0; i < _pool.Length; i++)
        {
            int idx = (_head + i) % _pool.Length;
            if (!_pool[idx].isPlaying)
            {
                _head = (idx + 1) % _pool.Length;
                return _pool[idx];
            }
        }
        // 全スロット使用中 → 最も古いスロットを強制停止して再利用
        var stolen = _pool[_head];
        stolen.Stop();
        _head = (_head + 1) % _pool.Length;
        return stolen;
    }

    private void Setup2D(AudioSource src, AudioClip clip, float vol, float pitch)
    {
        src.clip         = clip;
        src.loop         = false;
        src.spatialBlend = 0f;
        src.pitch        = pitch * (1f + Random.Range(-pitchVariance, pitchVariance));
        src.volume       = vol   * (1f + Random.Range(-volumeVariance, volumeVariance));
    }

    private void Setup3D(AudioSource src, AudioClip clip, float vol, float pitch,
                          float minD, float maxD)
    {
        src.clip         = clip;
        src.loop         = false;
        src.spatialBlend = 1f;
        src.rolloffMode  = AudioRolloffMode.Custom;
        src.minDistance  = minD;
        src.maxDistance  = maxD;
        src.dopplerLevel = 0f;
        src.spread       = 20f;
        src.pitch        = pitch * (1f + Random.Range(-pitchVariance, pitchVariance));
        src.volume       = vol   * (1f + Random.Range(-volumeVariance, volumeVariance));
    }
}
