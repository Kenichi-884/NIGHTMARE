using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;

/// <summary>
/// モンスターの 3D 空間音響コントローラー。
///
/// 各 FacilityLocation にワールド座標の AudioSource を割り当て、
/// 閉鎖ドア数に応じた低域通過フィルタ（オクルージョン）を動的に適用する。
/// モンスターが移動するたびに位置づけされた足音 / 気配音を再生し、
/// プレイヤーが音でモンスターの位置を察知できるようにする。
/// </summary>
public class SpatialAudioController : MonoBehaviour
{
    public static SpatialAudioController Instance { get; private set; }

    [Header("Optional: route SFX through AudioMixer group")]
    [SerializeField] private AudioMixerGroup sfxGroup;

    // ─── 施設座標マップ（NightmareMapBuilder 準拠）────────────────
    private static readonly Dictionary<FacilityLocation, Vector3> Positions =
        new Dictionary<FacilityLocation, Vector3>
    {
        { FacilityLocation.Outside_North, new Vector3(  0f,  0f,  32f) },
        { FacilityLocation.Outside_East,  new Vector3( 22f,  0f,  18f) },
        { FacilityLocation.Outside_West,  new Vector3(-22f,  0f,  18f) },
        { FacilityLocation.Outside_Top,   new Vector3(  0f,  0f,  12f) },
        { FacilityLocation.Lobby_Main,    new Vector3(  0f,  0f,   7f) },
        { FacilityLocation.Lobby_Stairs,  new Vector3(  0f,  0f,   3f) },
        { FacilityLocation.B1_Corridor,   new Vector3(  0f, -4f,  -3f) },
        { FacilityLocation.B1_DoorFront,  new Vector3(  0f, -4f,  -7f) },
        { FacilityLocation.ManagersRoom,  new Vector3(  0f, -4f, -12f) },
    };

    // ドア枚数ごとのカットオフ周波数（多いほど遮音）
    private static readonly float[] OcclusionCutoffs = { 22000f, 5000f, 2000f, 900f, 450f };

    // ─── 移動効果音（Inspector でアサイン）───────────────────────
    [Header("Movement SFX")]
    [SerializeField] private AudioClip sfxFootstepDefault;
    [SerializeField] private AudioClip sfxRusherStomp;
    [SerializeField] private AudioClip sfxLurkerScrape;
    [SerializeField] private AudioClip sfxKnockerKnock;

    // ─── ランタイムキャッシュ ─────────────────────────────────────
    private readonly Dictionary<FacilityLocation, AudioSource>        _sources = new();
    private readonly Dictionary<FacilityLocation, AudioLowPassFilter> _lpfs    = new();
    private readonly Dictionary<MonsterBase, FacilityLocation>        _prevLocs = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        UpdateOcclusion();
        PollMonsterMovement();
    }

    // ─── Public API ────────────────────────────────────────────────

    /// <summary>指定ロケーションからクリップを 3D 再生する。</summary>
    public void PlayAt(AudioClip clip, FacilityLocation loc, float volume = 1f)
    {
        if (!clip) return;
        GetOrCreate(loc).PlayOneShot(clip, volume);
    }

    /// <summary>ロケーションのワールド座標を返す（AudioPoolManager との連携用）。</summary>
    public Vector3 GetWorldPos(FacilityLocation loc) =>
        Positions.TryGetValue(loc, out var p) ? p : Vector3.zero;

    // ─── 内部処理 ──────────────────────────────────────────────────

    private AudioSource GetOrCreate(FacilityLocation loc)
    {
        if (_sources.TryGetValue(loc, out var cached)) return cached;

        var go = new GameObject($"Spat_{loc}");
        go.transform.SetParent(transform);
        go.transform.position = Positions.TryGetValue(loc, out var p) ? p : Vector3.zero;

        var src = go.AddComponent<AudioSource>();
        src.spatialBlend = 1f;
        src.rolloffMode  = AudioRolloffMode.Custom;
        src.minDistance  = 1.5f;
        src.maxDistance  = 50f;
        src.dopplerLevel = 0f;
        src.spread       = 25f;
        src.playOnAwake  = false;
        if (sfxGroup) src.outputAudioMixerGroup = sfxGroup;

        var lpf = go.AddComponent<AudioLowPassFilter>();
        lpf.cutoffFrequency   = 22000f;
        lpf.lowpassResonanceQ = 0.75f;

        _sources[loc] = src;
        _lpfs[loc]    = lpf;
        return src;
    }

    private void UpdateOcclusion()
    {
        foreach (var kv in _lpfs)
            kv.Value.cutoffFrequency = ComputeCutoff(kv.Key);
    }

    private float ComputeCutoff(FacilityLocation loc)
    {
        var dm = DoorManager.Instance;
        if (dm == null) return OcclusionCutoffs[0];

        bool gate   = dm.IsClosed(DoorID.Gate);
        bool entr   = dm.IsClosed(DoorID.Entrance);
        bool stairs = dm.IsClosed(DoorID.BasementStairs);
        bool b1c    = dm.IsClosed(DoorID.B1Corridor);

        int closed = loc switch
        {
            FacilityLocation.Outside_North or
            FacilityLocation.Outside_East  or
            FacilityLocation.Outside_West  => (gate ? 1 : 0) + (entr ? 1 : 0) + (stairs ? 1 : 0) + (b1c ? 1 : 0),

            FacilityLocation.Outside_Top   => (entr ? 1 : 0) + (stairs ? 1 : 0) + (b1c ? 1 : 0),

            FacilityLocation.Lobby_Main    or
            FacilityLocation.Lobby_Stairs  => (stairs ? 1 : 0) + (b1c ? 1 : 0),

            FacilityLocation.B1_Corridor   => (b1c ? 1 : 0),

            _ => 0
        };

        return OcclusionCutoffs[Mathf.Clamp(closed, 0, OcclusionCutoffs.Length - 1)];
    }

    // ── モンスター移動のポーリング ─────────────────────────────────

    private void PollMonsterMovement()
    {
        var mm = MonsterManager.Instance;
        if (mm == null) return;

        foreach (var m in mm.ActiveMonsters)
        {
            var loc = m.CurrentLocation;
            if (!_prevLocs.TryGetValue(m, out var prev)) { _prevLocs[m] = loc; continue; }
            if (prev == loc) continue;
            _prevLocs[m] = loc;
            OnMonsterMoved(m, loc);
        }
    }

    private void OnMonsterMoved(MonsterBase m, FacilityLocation loc)
    {
        // モンスター種別ごとに移動音を切替
        AudioClip sfx = m.MonsterType switch
        {
            MonsterType.Rusher  => sfxRusherStomp  ?? sfxFootstepDefault,
            MonsterType.Lurker  => sfxLurkerScrape ?? sfxFootstepDefault,
            MonsterType.Knocker => sfxKnockerKnock ?? sfxFootstepDefault,
            _                   => sfxFootstepDefault
        };
        if (!sfx) return;

        // 近づくほど音量を大きく
        float vol = loc switch
        {
            FacilityLocation.B1_DoorFront  => 1.00f,
            FacilityLocation.B1_Corridor   => 0.85f,
            FacilityLocation.Lobby_Stairs  => 0.60f,
            FacilityLocation.Lobby_Main    => 0.50f,
            FacilityLocation.Outside_Top   => 0.35f,
            _                              => 0.20f
        };

        PlayAt(sfx, loc, vol);
    }
}
