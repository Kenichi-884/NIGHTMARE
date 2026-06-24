using UnityEngine;
using UnityEngine.Audio;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class PhoneDayConfig
{
    [Tooltip("対象Day (1-7)")]
    public int day = 1;
    [Tooltip("このDayに電話を鳴らすか")]
    public bool enabled = true;
    [Tooltip("Day開始から着信が始まるまでの遅延(秒)")]
    public float startDelay = 2f;
    [Tooltip("着信音が鳴り続ける最大秒数")]
    public float ringDuration = 15f;
    [Tooltip("電話に出るキー")]
    public KeyCode answerKey = KeyCode.F;
    [Tooltip("着信音クリップ (ループ再生)。nullの場合は着信音なし")]
    public AudioClip ringClip;
    [Tooltip("電話に出た後に流れる音声。nullの場合は無音で着信のみ")]
    public AudioClip voiceClip;
}

public class PhoneCallSystem : MonoBehaviour
{
    public static PhoneCallSystem Instance { get; private set; }

    [Header("Day別 電話設定")]
    [Tooltip("Day番号に対応した着信設定。リストに存在しないDayは電話なし。")]
    [SerializeField] private List<PhoneDayConfig> dayConfigs = new List<PhoneDayConfig>();

    [Header("共通 Audio")]
    [Tooltip("電話を取った瞬間に鳴らすSFX")]
    [SerializeField] private AudioClip pickupClip;
    [SerializeField, Range(0f, 1f)] private float ringVolume   = 0.85f;
    [SerializeField, Range(0f, 1f)] private float pickupVolume = 1.0f;
    [SerializeField, Range(0f, 1f)] private float voiceVolume  = 1.0f;
    [Tooltip("通話音声にかけるAudio Mixer Group（電話エフェクト用）。nullなら素の音声をそのまま再生。")]
    [SerializeField] private AudioMixerGroup phoneMixerGroup;

    public bool IsRinging { get; private set; }
    public bool IsInCall  { get; private set; }

    private PhoneDayConfig  _current;
    private AudioSource     _ringSource;
    private AudioSource     _voiceSource;
    private Coroutine       _sequence;
    private Coroutine       _voiceRoutine;
    private System.Action<int> _onNightCleared;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _ringSource              = gameObject.AddComponent<AudioSource>();
        _ringSource.loop         = true;
        _ringSource.playOnAwake  = false;
        _ringSource.spatialBlend = 0f;

        _voiceSource              = gameObject.AddComponent<AudioSource>();
        _voiceSource.loop         = false;
        _voiceSource.playOnAwake  = false;
        _voiceSource.spatialBlend = 0f;
    }

    private void Start()
    {
        // SerializeField は Awake より後に確定するため Start で適用する
        _voiceSource.outputAudioMixerGroup = phoneMixerGroup;
        _ringSource.outputAudioMixerGroup  = phoneMixerGroup;

        var gm = GameManager.Instance;
        if (gm != null)
        {
            _onNightCleared    = _ => ForceStop();
            gm.OnDayStarted   += OnDayStarted;
            gm.OnGameOver     += ForceStop;
            gm.OnTrueEnding   += ForceStop;
            gm.OnNightCleared += _onNightCleared;
        }
    }

    private void OnDestroy()
    {
        var gm = GameManager.Instance;
        if (gm != null)
        {
            gm.OnDayStarted   -= OnDayStarted;
            gm.OnGameOver     -= ForceStop;
            gm.OnTrueEnding   -= ForceStop;
            gm.OnNightCleared -= _onNightCleared;
        }
    }

    private void Update()
    {
        if (!IsRinging || _current == null) return;
        if (GameManager.Instance?.CurrentState != GameState.Night) return;
        if (Input.GetKeyDown(_current.answerKey))
            AnswerPhone();
    }

    // ─── Day開始 ───────────────────────────────────────────────

    private void OnDayStarted(int day)
    {
        ForceStop();
        _current = FindConfig(day);
        if (_current == null || !_current.enabled) return;
        _sequence = StartCoroutine(PhoneSequence(_current));
    }

    private PhoneDayConfig FindConfig(int day)
    {
        foreach (var c in dayConfigs)
            if (c.day == day) return c;
        return null;
    }

    // ─── メインシーケンス ───────────────────────────────────────

    private IEnumerator PhoneSequence(PhoneDayConfig cfg)
    {
        if (cfg.startDelay > 0f)
            yield return new WaitForSeconds(cfg.startDelay);

        IsRinging = true;
        StartRing(cfg);

        yield return new WaitForSeconds(cfg.ringDuration > 0f ? cfg.ringDuration : 15f);

        if (IsRinging)
            MissedCall();
    }

    // ─── 着信 ──────────────────────────────────────────────────

    private void StartRing(PhoneDayConfig cfg)
    {
        if (cfg.ringClip == null) return;
        _ringSource.clip   = cfg.ringClip;
        _ringSource.volume = ringVolume;
        _ringSource.Play();
    }

    private void StopRing()
    {
        _ringSource.Stop();
        _ringSource.clip = null;
    }

    // ─── 応答 ──────────────────────────────────────────────────

    public void AnswerPhone()
    {
        if (!IsRinging) return;
        IsRinging = false;
        StopRing();

        IsInCall = true;
        _voiceRoutine = StartCoroutine(VoiceRoutine(_current?.voiceClip));
    }

    private IEnumerator VoiceRoutine(AudioClip voice)
    {
        // 受話器を取る音
        if (pickupClip != null)
        {
            _voiceSource.PlayOneShot(pickupClip, pickupVolume);
            yield return new WaitForSeconds(pickupClip.length);
        }

        if (voice != null)
        {
            _voiceSource.clip   = voice;
            _voiceSource.volume = voiceVolume;
            _voiceSource.Play();
            yield return new WaitForSeconds(voice.length);
        }

        EndCall();
    }

    // ─── 不在着信・通話終了 ────────────────────────────────────

    private void MissedCall()
    {
        IsRinging = false;
        StopRing();
    }

    private void EndCall()
    {
        IsInCall = false;
        _voiceSource.Stop();
    }

    public void ForceStop()
    {
        if (_sequence     != null) StopCoroutine(_sequence);
        if (_voiceRoutine != null) StopCoroutine(_voiceRoutine);
        _sequence     = null;
        _voiceRoutine = null;
        IsRinging = false;
        IsInCall  = false;
        if (_ringSource  != null) { _ringSource.Stop();  _ringSource.clip  = null; }
        if (_voiceSource != null) { _voiceSource.Stop(); _voiceSource.clip = null; }
    }
}
