using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// 日数ごとに発生する異常現象を管理する
// Day2から段階的に追加され、Day7では複合的に発生する
public class PhenomenaManager : MonoBehaviour
{
    public static PhenomenaManager Instance { get; private set; }

    [System.Serializable]
    public class PhenomenaEntry
    {
        public int fromDay;          // この日以降に発生
        public PhenomenaType type;
        public GamePhase fromPhase;  // このフェーズ以降から発生可能
        [Range(0f, 1f)]
        public float chance = 0.3f;  // 1分ごとの発生確率
        public float cooldown = 120f; // 最低この秒数は間隔を空ける
    }

    [SerializeField] private List<PhenomenaEntry> entries;

    private readonly Dictionary<PhenomenaType, float> lastOccurred = new Dictionary<PhenomenaType, float>();
    private float checkTimer = 0f;
    private const float CHECK_INTERVAL = 60f; // 1分ごとにチェック

    // シーン上の PhenomenaObject が購読するイベント
    public event System.Action<PhenomenaType> OnPhenomenaTriggered;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (entries == null || entries.Count == 0) BuildDefaultEntries();
        GameManager.Instance.OnDayStarted += _ => lastOccurred.Clear();
    }

    private void Update()
    {
        if (GameManager.Instance.CurrentState != GameState.Night) return;

        checkTimer += Time.deltaTime;
        if (checkTimer >= CHECK_INTERVAL)
        {
            checkTimer = 0f;
            TryTriggerPhenomena();
        }
    }

    private void TryTriggerPhenomena()
    {
        int day = GameManager.Instance.CurrentDay;
        GamePhase phase = GameManager.Instance.CurrentPhase;

        foreach (var entry in entries)
        {
            if (day < entry.fromDay) continue;
            if (PhaseIndex(phase) < PhaseIndex(entry.fromPhase)) continue;

            // クールダウンチェック
            if (lastOccurred.TryGetValue(entry.type, out float last))
                if (Time.time - last < entry.cooldown) continue;

            // 日数 + 天気による発生確率上昇
            float weatherMult = WeatherManager.Instance != null ? WeatherManager.Instance.PhenomenaChanceMultiplier : 1f;
            float chance = Mathf.Min((entry.chance + (day - entry.fromDay) * 0.05f) * weatherMult, 0.9f);
            if (Random.value < chance)
            {
                TriggerPhenomena(entry.type, day);
                lastOccurred[entry.type] = Time.time;
            }
        }
    }

    private void TriggerPhenomena(PhenomenaType type, int day)
    {
        switch (type)
        {
            case PhenomenaType.CameraFlicker:
                // ランダムなカメラが 1〜3 秒切れる
                SecurityCameraSystem.Instance.TriggerFlicker(Random.value > 0.5f, Random.Range(1f, 3f));
                AudioManager.Instance?.Play("camera_static");
                break;

            case PhenomenaType.PowerFluctuation:
                // 電力が 5〜15% 急減
                PowerManager.Instance.ApplyFluctuation(Random.Range(5f, 15f));
                AudioManager.Instance?.Play("power_flicker");
                UIManager.Instance?.ShowPhenomenaWarning("電力異常！");
                break;

            case PhenomenaType.LightFailure:
                // 内部カメラ映像が 30 秒暗転
                StartCoroutine(LightFailureRoutine(30f));
                UIManager.Instance?.ShowPhenomenaWarning("照明障害");
                break;

            case PhenomenaType.AudioDistortion:
                // 偽足音 + ランダム方向のカメラをちらつかせて撹乱
                AudioManager.Instance?.Play("fake_footstep");
                SecurityCameraSystem.Instance.TriggerFlicker(Random.value > 0.5f, 0.4f);
                break;

            case PhenomenaType.TimeWarp:
                // ゲーム内時計を 10 秒間 3 倍速にする（時刻表示が急進して混乱させる）
                GameManager.Instance.TriggerTimeWarp(3f, 10f);
                UIManager.Instance?.ShowPhenomenaWarning("時計異常");
                AudioManager.Instance?.Play("time_warp");
                break;

            case PhenomenaType.GhostSignal:
                // 現在のカメラに幽霊スプライトを 2 秒表示
                SecurityCameraSystem.Instance.ShowGhostSignal(2f);
                UIManager.Instance?.ShowGhostSignal(2f);
                AudioManager.Instance?.Play("ghost_signal");
                break;

            case PhenomenaType.Blackout:
                // 全カメラが暗転（Day が上がるほど長くなる）
                float blackDur = Mathf.Max(3f, 5f + (day - 6) * 2f);
                SecurityCameraSystem.Instance.TriggerBlackout(blackDur);
                UIManager.Instance?.ShowPhenomenaWarning("全カメラ停止！");
                AudioManager.Instance?.Play("blackout");
                break;

            // ─── 追加現象 ───────────────────────────────────────────────

            case PhenomenaType.TemperatureDrop:
                // 気温急降下: 寒気 SE + 画面グレイン増加 + 警告テキスト
                StartCoroutine(TemperatureDropRoutine());
                break;

            case PhenomenaType.DoorBang:
                // ランダムなドアへの衝撃音: 実際にはドアを動かさずフェイクで脅す
                StartCoroutine(DoorBangRoutine());
                break;

            case PhenomenaType.StaticBurst:
                // 全カメラに激しいノイズ嵐を数秒間叩き込む
                StartCoroutine(StaticBurstRoutine());
                break;
        }

        OnPhenomenaTriggered?.Invoke(type);
        Debug.Log($"[Phenomena] {type} triggered on Day {day}");
    }

    // ─── コルーチン ───────────────────────────────────────────────────

    private IEnumerator LightFailureRoutine(float duration)
    {
        SecurityCameraSystem.Instance.SetLightsOut(true);
        yield return new WaitForSeconds(duration);
        SecurityCameraSystem.Instance.SetLightsOut(false);
    }

    private IEnumerator TemperatureDropRoutine()
    {
        AudioManager.Instance?.Play("temperature_drop");
        UIManager.Instance?.ShowPhenomenaWarning("気温低下...");
        // PostProcess のグレインを一時的に増やして「寒気」を演出
        PostProcessChainUI.Instance?.SetGrainIntensity(0.18f);
        yield return new WaitForSeconds(15f);
        PostProcessChainUI.Instance?.SetGrainIntensity(0.05f);
    }

    private IEnumerator DoorBangRoutine()
    {
        // 閉まっているドアを優先して選ぶ、なければランダム
        DoorID[] ids = { DoorID.Gate, DoorID.Entrance, DoorID.BasementStairs, DoorID.B1Corridor };
        DoorID target = ids[Random.Range(0, ids.Length)];
        AudioManager.Instance?.Play("door_bang");
        UIManager.Instance?.ShowPhenomenaWarning("ドア異常！");
        // 2 回衝撃を与えて不規則感を演出
        yield return new WaitForSeconds(Random.Range(0.8f, 1.5f));
        AudioManager.Instance?.Play("door_pounding");
    }

    private IEnumerator StaticBurstRoutine()
    {
        AudioManager.Instance?.Play("static_burst");
        UIManager.Instance?.ShowPhenomenaWarning("強電波障害");
        // 全 CameraViewEffect を強グリッチ状態にして 3〜4 秒維持
        var effects = FindObjectsOfType<CameraViewEffect>();
        foreach (var e in effects) e.SetGlitchIntensity(0.85f);
        yield return new WaitForSeconds(Random.Range(3f, 4.5f));
        foreach (var e in effects) e.SetGlitchIntensity(0f);
    }

    private int PhaseIndex(GamePhase phase) => (int)phase;

    private void BuildDefaultEntries()
    {
        entries = new List<PhenomenaEntry>
        {
            // ─ 既存現象 ──────────────────────────────────────────────────
            new PhenomenaEntry { fromDay = 2, type = PhenomenaType.CameraFlicker,    fromPhase = GamePhase.Omen,         chance = 0.25f, cooldown = 90f  },
            new PhenomenaEntry { fromDay = 3, type = PhenomenaType.PowerFluctuation, fromPhase = GamePhase.Erosion,      chance = 0.20f, cooldown = 180f },
            new PhenomenaEntry { fromDay = 4, type = PhenomenaType.LightFailure,     fromPhase = GamePhase.Collapse,     chance = 0.25f, cooldown = 150f },
            new PhenomenaEntry { fromDay = 4, type = PhenomenaType.AudioDistortion,  fromPhase = GamePhase.Infiltration, chance = 0.30f, cooldown = 60f  },
            new PhenomenaEntry { fromDay = 5, type = PhenomenaType.TimeWarp,         fromPhase = GamePhase.Siege,        chance = 0.15f, cooldown = 200f },
            new PhenomenaEntry { fromDay = 6, type = PhenomenaType.GhostSignal,      fromPhase = GamePhase.Abyss,        chance = 0.30f, cooldown = 120f },
            new PhenomenaEntry { fromDay = 7, type = PhenomenaType.Blackout,         fromPhase = GamePhase.Collapse,     chance = 0.60f, cooldown = 180f },

            // ─ 追加現象 ──────────────────────────────────────────────────
            // 気温急降下: Day3 中盤から。視覚的な寒気演出で心理的プレッシャー
            new PhenomenaEntry { fromDay = 3, type = PhenomenaType.TemperatureDrop,  fromPhase = GamePhase.Siege,        chance = 0.25f, cooldown = 150f },
            // ドア衝撃音: Day4 から。ドアを叩くフェイク音で判断を惑わせる
            new PhenomenaEntry { fromDay = 4, type = PhenomenaType.DoorBang,         fromPhase = GamePhase.Erosion,      chance = 0.35f, cooldown = 90f  },
            // 強電波障害: Day6 から。全カメラが数秒間ノイズだらけになる
            new PhenomenaEntry { fromDay = 6, type = PhenomenaType.StaticBurst,      fromPhase = GamePhase.Infiltration, chance = 0.25f, cooldown = 240f },
        };
    }
}
