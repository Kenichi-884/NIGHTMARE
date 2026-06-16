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

            // 日数が増えるほど発生確率上昇
            float chance = Mathf.Min(entry.chance + (day - entry.fromDay) * 0.05f, 0.9f);
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
                // 外部・内部どちらかがランダムで1〜2秒切れる
                bool ext = Random.value > 0.5f;
                float duration = Random.Range(1f, 3f);
                SecurityCameraSystem.Instance.TriggerFlicker(ext, duration);
                AudioManager.Instance?.Play("camera_static");
                break;

            case PhenomenaType.PowerFluctuation:
                // 電力が急激に5〜15%減る
                float loss = Random.Range(5f, 15f);
                PowerManager.Instance.ApplyFluctuation(loss);
                AudioManager.Instance?.Play("power_flicker");
                UIManager.Instance?.ShowPhenomenaWarning("電力異常！");
                break;

            case PhenomenaType.LightFailure:
                // B1カメラ映像が暗くなる (30秒)
                StartCoroutine(LightFailureRoutine(30f));
                UIManager.Instance?.ShowPhenomenaWarning("照明障害");
                break;

            case PhenomenaType.AudioDistortion:
                // フェイクの足音を鳴らす
                AudioManager.Instance?.Play("fake_footstep");
                break;

            case PhenomenaType.TimeWarp:
                // ゲーム内時計が10秒間2倍速になる演出
                UIManager.Instance?.ShowPhenomenaWarning("時計異常");
                AudioManager.Instance?.Play("time_warp");
                break;

            case PhenomenaType.GhostSignal:
                // ランダムなカメラに偽モンスターが2秒映る
                StartCoroutine(GhostSignalRoutine());
                break;

            case PhenomenaType.Blackout:
                // 全カメラが5秒暗転
                float blackDuration = Mathf.Max(3f, 5f + (day - 6) * 2f);
                SecurityCameraSystem.Instance.TriggerBlackout(blackDuration);
                UIManager.Instance?.ShowPhenomenaWarning("全カメラ停止！");
                AudioManager.Instance?.Play("blackout");
                break;
        }

        Debug.Log($"[Phenomena] {type} triggered on Day {day}");
    }

    private IEnumerator LightFailureRoutine(float duration)
    {
        SecurityCameraSystem.Instance.SetLightsOut(true);
        yield return new WaitForSeconds(duration);
        SecurityCameraSystem.Instance.SetLightsOut(false);
    }

    private IEnumerator GhostSignalRoutine()
    {
        // UIManagerにゴースト表示を依頼
        UIManager.Instance?.ShowGhostSignal(2f);
        AudioManager.Instance?.Play("ghost_signal");
        yield return null;
    }

    private int PhaseIndex(GamePhase phase) => (int)phase;

    private void BuildDefaultEntries()
    {
        entries = new List<PhenomenaEntry>
        {
            new PhenomenaEntry { fromDay = 2, type = PhenomenaType.CameraFlicker,    fromPhase = GamePhase.Omen,         chance = 0.25f, cooldown = 90f  },
            new PhenomenaEntry { fromDay = 3, type = PhenomenaType.PowerFluctuation, fromPhase = GamePhase.Erosion,      chance = 0.20f, cooldown = 180f },
            new PhenomenaEntry { fromDay = 4, type = PhenomenaType.LightFailure,     fromPhase = GamePhase.Collapse,     chance = 0.25f, cooldown = 150f },
            new PhenomenaEntry { fromDay = 4, type = PhenomenaType.AudioDistortion,  fromPhase = GamePhase.Infiltration, chance = 0.30f, cooldown = 60f  },
            new PhenomenaEntry { fromDay = 5, type = PhenomenaType.TimeWarp,         fromPhase = GamePhase.Siege,        chance = 0.15f, cooldown = 200f },
            new PhenomenaEntry { fromDay = 6, type = PhenomenaType.GhostSignal,      fromPhase = GamePhase.Abyss,        chance = 0.30f, cooldown = 120f },
            new PhenomenaEntry { fromDay = 7, type = PhenomenaType.Blackout,         fromPhase = GamePhase.BeforeDawn,   chance = 0.60f, cooldown = 180f },
        };
    }
}
