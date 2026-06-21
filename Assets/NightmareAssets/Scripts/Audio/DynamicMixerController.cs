using UnityEngine;
using UnityEngine.Audio;
using System.Collections;

/// <summary>
/// AudioMixer スナップショット管理と動的 DSP コントローラー。
///
/// ゲームの緊張状態に応じて AudioMixer スナップショットをブレンドし、
/// BGM ピッチ・マスター低域通過を調整する。
///
/// AudioMixer の準備（任意）:
///   Groups:    Master → BGM / SFX / Ambient
///   Snapshots: Calm, Tense, Critical, PowerOut
///   Exposed:   "BGM_Pitch"（Pitch Shifter の Pitch パラメータ）
/// AudioMixer が未アサインの場合はコード側で BGM の LPF を制御するフォールバック動作。
/// </summary>
public class DynamicMixerController : MonoBehaviour
{
    public static DynamicMixerController Instance { get; private set; }

    [Header("AudioMixer (任意)")]
    [SerializeField] private AudioMixer      mixer;
    [SerializeField] private AudioMixerSnapshot snapCalm;
    [SerializeField] private AudioMixerSnapshot snapTense;
    [SerializeField] private AudioMixerSnapshot snapCritical;
    [SerializeField] private AudioMixerSnapshot snapPowerOut;

    [Header("Transition Duration (sec)")]
    [SerializeField, Range(0.1f, 6f)] private float durCalm     = 3.0f;
    [SerializeField, Range(0.1f, 6f)] private float durTense    = 1.5f;
    [SerializeField, Range(0.1f, 6f)] private float durCritical = 0.8f;
    [SerializeField, Range(0.1f, 6f)] private float durPowerOut = 0.4f;

    private enum MixerState { Calm, Tense, Critical, PowerOut }
    private MixerState _state = MixerState.Calm;
    private Coroutine  _lpRoutine;
    private float      _codeLowPass = 22000f;
    private AudioLowPassFilter _fallbackLpf;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        gm.OnPhaseChanged += OnPhaseChanged;
        gm.OnGameOver     += () => TransitionTo(MixerState.Critical, durCritical);
        gm.OnDayStarted   += _ => TransitionTo(MixerState.Calm, durCalm);

        var pm = PowerManager.Instance;
        if (pm != null)
        {
            pm.OnPowerOut      += () => TransitionTo(MixerState.PowerOut, durPowerOut);
            pm.OnPowerRestored += () => TransitionTo(MixerState.Calm, durCalm * 0.5f);
        }
    }

    // ─── フェーズ → スナップショット ──────────────────────────────

    private void OnPhaseChanged(GamePhase phase)
    {
        var next = phase switch
        {
            GamePhase.Silence or GamePhase.Omen                                   => MixerState.Calm,
            GamePhase.Contact or GamePhase.Increase or GamePhase.Erosion
                or GamePhase.Infiltration or GamePhase.Siege                       => MixerState.Tense,
            GamePhase.Collapse or GamePhase.Abyss or GamePhase.BeforeDawn         => MixerState.Critical,
            _ => _state
        };
        if (next != _state) TransitionTo(next, next == MixerState.Critical ? durCritical : durTense);
    }

    private void TransitionTo(MixerState next, float duration)
    {
        _state = next;

        // AudioMixer スナップショット遷移
        if (mixer != null)
        {
            AudioMixerSnapshot snap = next switch
            {
                MixerState.Tense    => snapTense,
                MixerState.Critical => snapCritical,
                MixerState.PowerOut => snapPowerOut,
                _                   => snapCalm
            };
            snap?.TransitionTo(duration);

            // BGM ピッチを若干変動（停電: 下げる、緊急: 上げる）
            float pitch = next switch
            {
                MixerState.Calm     => 0.985f,
                MixerState.Tense    => 1.000f,
                MixerState.Critical => 1.018f,
                MixerState.PowerOut => 0.960f,
                _ => 1f
            };
            mixer.SetFloat("BGM_Pitch", pitch);
        }

        // フォールバック: AudioManager ゲームオブジェクトに LPF を適用
        float targetLp = next == MixerState.PowerOut ? 1000f : 22000f;
        if (_lpRoutine != null) StopCoroutine(_lpRoutine);
        _lpRoutine = StartCoroutine(FadeLowPass(targetLp, duration));
    }

    private IEnumerator FadeLowPass(float target, float dur)
    {
        EnsureFallbackLpf();
        float start = _codeLowPass, t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            _codeLowPass = Mathf.Lerp(start, target, t / dur);
            if (_fallbackLpf) _fallbackLpf.cutoffFrequency = _codeLowPass;
            yield return null;
        }
        _codeLowPass = target;
        if (_fallbackLpf) _fallbackLpf.cutoffFrequency = _codeLowPass;
    }

    private void EnsureFallbackLpf()
    {
        if (_fallbackLpf) return;
        if (mixer != null) return;   // Mixer があれば不要
        var am = AudioManager.Instance;
        if (!am) return;
        _fallbackLpf = am.GetComponent<AudioLowPassFilter>()
                       ?? am.gameObject.AddComponent<AudioLowPassFilter>();
        _fallbackLpf.lowpassResonanceQ = 0.7f;
    }

    // ─── 外部 API ──────────────────────────────────────────────────

    /// <summary>マスター音量を 0〜1 で設定（dB 変換して AudioMixer へ）。</summary>
    public void SetMasterVolume(float normalised)
    {
        if (mixer == null) return;
        mixer.SetFloat("Master_Vol", Mathf.Log10(Mathf.Max(normalised, 0.0001f)) * 20f);
    }
}
