using UnityEngine;
using System.Collections.Generic;

public class MonsterManager : MonoBehaviour
{
    public static MonsterManager Instance { get; private set; }

    [Header("Prefabs")]
    [SerializeField] private CrawlerAI crawlerPrefab;
    [SerializeField] private JammerAI jammerPrefab;
    [SerializeField] private RusherAI rusherPrefab;
    [SerializeField] private LurkerAI lurkerPrefab;
    [SerializeField] private MimicAI mimicPrefab;
    [SerializeField] private KnockerAI knockerPrefab;

    private readonly List<MonsterBase> active = new List<MonsterBase>();
    public IReadOnlyList<MonsterBase> ActiveMonsters => active;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        GameManager.Instance.OnDayStarted += _ => ClearAll();
    }

    public void Spawn(MonsterType type, FacilityLocation spawnAt = FacilityLocation.Outside_North)
    {
        int day = GameManager.Instance.CurrentDay;
        MonsterBase prefab = type switch
        {
            MonsterType.Crawler => crawlerPrefab,
            MonsterType.Jammer  => jammerPrefab,
            MonsterType.Rusher  => rusherPrefab,
            MonsterType.Lurker  => lurkerPrefab,
            MonsterType.Mimic   => mimicPrefab,
            MonsterType.Knocker => knockerPrefab,
            _ => null
        };

        if (prefab == null)
        {
            Debug.LogWarning($"[MonsterManager] Prefab for {type} is not assigned.");
            return;
        }

        var monster = Instantiate(prefab, transform);
        monster.Initialize(spawnAt, day);
        active.Add(monster);
    }

    public void Unregister(MonsterBase monster)
    {
        active.Remove(monster);
    }

    public void ClearAll()
    {
        foreach (var m in new List<MonsterBase>(active))
            Destroy(m.gameObject);
        active.Clear();
    }
}
