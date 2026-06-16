using UnityEngine;
using System.Collections.Generic;

// 7日 × 10フェーズのスポーンテーブルを管理する
public class MonsterSpawner : MonoBehaviour
{
    public static MonsterSpawner Instance { get; private set; }

    // スポーンエントリ: 何日の何フェーズに何を何体
    [System.Serializable]
    public class SpawnEntry
    {
        public int day;
        public GamePhase phase;
        public MonsterType type;
        public int count = 1;
        public FacilityLocation spawnAt = FacilityLocation.Outside_North;
    }

    // エディタで追加・編集可能なリスト（デフォルト値はStart()で設定）
    [SerializeField] private List<SpawnEntry> spawnTable = new List<SpawnEntry>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (spawnTable.Count == 0) BuildDefaultTable();
        GameManager.Instance.OnPhaseChanged += OnPhaseChanged;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance) GameManager.Instance.OnPhaseChanged -= OnPhaseChanged;
    }

    private void OnPhaseChanged(GamePhase phase)
    {
        int day = GameManager.Instance.CurrentDay;
        foreach (var entry in spawnTable)
        {
            if (entry.day == day && entry.phase == phase)
            {
                for (int i = 0; i < entry.count; i++)
                    MonsterManager.Instance.Spawn(entry.type, entry.spawnAt);
            }
        }
    }

    // ======= デフォルトスポーンテーブル =======
    // 緊張感の設計方針:
    //   Day1-2: チュートリアル、ゆっくり慣れる
    //   Day3:   Rusher登場、スピード感が変わる
    //   Day4:   Lurker登場、「見えない恐怖」が始まる
    //   Day5:   Mimic登場、情報が信用できなくなる
    //   Day6:   Knocker登場、音での判断が要求される
    //   Day7:   全タイプ最大、早い段階から脅威
    private void BuildDefaultTable()
    {
        spawnTable = new List<SpawnEntry>
        {
            // ===== Day 1: 静かな夜 - Crawlerのみ =====
            S(1, GamePhase.Contact,      MonsterType.Crawler, 1),
            S(1, GamePhase.Increase,     MonsterType.Crawler, 1),
            S(1, GamePhase.Erosion,      MonsterType.Crawler, 1),
            S(1, GamePhase.Siege,        MonsterType.Crawler, 1),

            // ===== Day 2: 監視を妨げる存在 - Jammer初登場 =====
            S(2, GamePhase.Contact,      MonsterType.Crawler, 2),
            S(2, GamePhase.Increase,     MonsterType.Crawler, 1),
            S(2, GamePhase.Erosion,      MonsterType.Jammer,  1, FacilityLocation.Outside_East),
            S(2, GamePhase.Infiltration, MonsterType.Crawler, 1),
            S(2, GamePhase.Siege,        MonsterType.Jammer,  1, FacilityLocation.Outside_West),
            S(2, GamePhase.Collapse,     MonsterType.Crawler, 1),

            // ===== Day 3: スピードが変わる - Rusher初登場 =====
            S(3, GamePhase.Contact,      MonsterType.Crawler, 2),
            S(3, GamePhase.Erosion,      MonsterType.Jammer,  1, FacilityLocation.Outside_East),
            S(3, GamePhase.Infiltration, MonsterType.Rusher,  1, FacilityLocation.Outside_East),
            S(3, GamePhase.Siege,        MonsterType.Crawler, 2),
            S(3, GamePhase.Siege,        MonsterType.Jammer,  1, FacilityLocation.Outside_West),
            S(3, GamePhase.Collapse,     MonsterType.Rusher,  1, FacilityLocation.Outside_East),
            S(3, GamePhase.Abyss,        MonsterType.Crawler, 1),
            S(3, GamePhase.Abyss,        MonsterType.Rusher,  1, FacilityLocation.Outside_East),

            // ===== Day 4: 見えない恐怖 - Lurker初登場 =====
            S(4, GamePhase.Contact,      MonsterType.Crawler, 2),
            S(4, GamePhase.Erosion,      MonsterType.Jammer,  1, FacilityLocation.Outside_East),
            S(4, GamePhase.Erosion,      MonsterType.Rusher,  1, FacilityLocation.Outside_East),
            S(4, GamePhase.Infiltration, MonsterType.Lurker,  1, FacilityLocation.Outside_West),
            S(4, GamePhase.Siege,        MonsterType.Crawler, 2),
            S(4, GamePhase.Collapse,     MonsterType.Lurker,  1, FacilityLocation.Outside_West),
            S(4, GamePhase.Collapse,     MonsterType.Rusher,  1, FacilityLocation.Outside_East),
            S(4, GamePhase.Abyss,        MonsterType.Crawler, 2),
            S(4, GamePhase.Abyss,        MonsterType.Jammer,  1, FacilityLocation.Outside_East),
            S(4, GamePhase.BeforeDawn,   MonsterType.Lurker,  1, FacilityLocation.Outside_West),
            S(4, GamePhase.BeforeDawn,   MonsterType.Rusher,  2, FacilityLocation.Outside_East),

            // ===== Day 5: 情報が信用できない - Mimic初登場 =====
            S(5, GamePhase.Contact,      MonsterType.Crawler, 2),
            S(5, GamePhase.Contact,      MonsterType.Rusher,  1, FacilityLocation.Outside_East),
            S(5, GamePhase.Erosion,      MonsterType.Jammer,  1, FacilityLocation.Outside_East),
            S(5, GamePhase.Infiltration, MonsterType.Lurker,  1, FacilityLocation.Outside_West),
            S(5, GamePhase.Infiltration, MonsterType.Mimic,   1),
            S(5, GamePhase.Siege,        MonsterType.Crawler, 2),
            S(5, GamePhase.Siege,        MonsterType.Rusher,  1, FacilityLocation.Outside_East),
            S(5, GamePhase.Collapse,     MonsterType.Lurker,  1, FacilityLocation.Outside_West),
            S(5, GamePhase.Abyss,        MonsterType.Crawler, 2),
            S(5, GamePhase.Abyss,        MonsterType.Jammer,  1, FacilityLocation.Outside_West),
            S(5, GamePhase.Abyss,        MonsterType.Mimic,   1),
            S(5, GamePhase.BeforeDawn,   MonsterType.Rusher,  2, FacilityLocation.Outside_East),
            S(5, GamePhase.BeforeDawn,   MonsterType.Lurker,  1, FacilityLocation.Outside_West),

            // ===== Day 6: 音の恐怖 - Knocker初登場 =====
            S(6, GamePhase.Contact,      MonsterType.Crawler, 2),
            S(6, GamePhase.Contact,      MonsterType.Rusher,  1, FacilityLocation.Outside_East),
            S(6, GamePhase.Erosion,      MonsterType.Jammer,  1, FacilityLocation.Outside_East),
            S(6, GamePhase.Erosion,      MonsterType.Knocker, 1),
            S(6, GamePhase.Infiltration, MonsterType.Lurker,  1, FacilityLocation.Outside_West),
            S(6, GamePhase.Infiltration, MonsterType.Mimic,   1),
            S(6, GamePhase.Siege,        MonsterType.Crawler, 2),
            S(6, GamePhase.Siege,        MonsterType.Knocker, 1),
            S(6, GamePhase.Collapse,     MonsterType.Rusher,  2, FacilityLocation.Outside_East),
            S(6, GamePhase.Collapse,     MonsterType.Jammer,  1, FacilityLocation.Outside_West),
            S(6, GamePhase.Abyss,        MonsterType.Crawler, 2),
            S(6, GamePhase.Abyss,        MonsterType.Lurker,  1, FacilityLocation.Outside_West),
            S(6, GamePhase.Abyss,        MonsterType.Knocker, 1),
            S(6, GamePhase.BeforeDawn,   MonsterType.Rusher,  2, FacilityLocation.Outside_East),
            S(6, GamePhase.BeforeDawn,   MonsterType.Mimic,   1),
            S(6, GamePhase.BeforeDawn,   MonsterType.Knocker, 1),

            // ===== Day 7: 最終夜 - 序盤から全タイプ、最高密度 =====
            S(7, GamePhase.Omen,         MonsterType.Crawler, 1), // Omenから始まる
            S(7, GamePhase.Contact,      MonsterType.Crawler, 2),
            S(7, GamePhase.Contact,      MonsterType.Rusher,  1, FacilityLocation.Outside_East),
            S(7, GamePhase.Contact,      MonsterType.Jammer,  1, FacilityLocation.Outside_East),
            S(7, GamePhase.Increase,     MonsterType.Lurker,  1, FacilityLocation.Outside_West),
            S(7, GamePhase.Increase,     MonsterType.Knocker, 1),
            S(7, GamePhase.Erosion,      MonsterType.Mimic,   1),
            S(7, GamePhase.Erosion,      MonsterType.Crawler, 2),
            S(7, GamePhase.Erosion,      MonsterType.Jammer,  1, FacilityLocation.Outside_West),
            S(7, GamePhase.Infiltration, MonsterType.Rusher,  2, FacilityLocation.Outside_East),
            S(7, GamePhase.Infiltration, MonsterType.Lurker,  1, FacilityLocation.Outside_West),
            S(7, GamePhase.Siege,        MonsterType.Crawler, 2),
            S(7, GamePhase.Siege,        MonsterType.Knocker, 1),
            S(7, GamePhase.Siege,        MonsterType.Jammer,  1, FacilityLocation.Outside_East),
            S(7, GamePhase.Collapse,     MonsterType.Mimic,   1),
            S(7, GamePhase.Collapse,     MonsterType.Rusher,  2, FacilityLocation.Outside_East),
            S(7, GamePhase.Collapse,     MonsterType.Lurker,  1, FacilityLocation.Outside_West),
            S(7, GamePhase.Abyss,        MonsterType.Crawler, 3),
            S(7, GamePhase.Abyss,        MonsterType.Knocker, 2),
            S(7, GamePhase.Abyss,        MonsterType.Jammer,  1, FacilityLocation.Outside_West),
            S(7, GamePhase.BeforeDawn,   MonsterType.Rusher,  3, FacilityLocation.Outside_East),
            S(7, GamePhase.BeforeDawn,   MonsterType.Lurker,  2, FacilityLocation.Outside_West),
            S(7, GamePhase.BeforeDawn,   MonsterType.Knocker, 2),
        };
    }

    private SpawnEntry S(int day, GamePhase phase, MonsterType type, int count,
        FacilityLocation spawnAt = FacilityLocation.Outside_North)
    {
        return new SpawnEntry { day = day, phase = phase, type = type, count = count, spawnAt = spawnAt };
    }
}
