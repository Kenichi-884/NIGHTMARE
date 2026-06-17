using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class DoorData
{
    public DoorID id;
    public string displayName;
    public float drainPerSecond;
    public FacilityLocation blocksDestination; // 閉めると、この場所への移動を遮断する
}

public class DoorManager : MonoBehaviour
{
    public static DoorManager Instance { get; private set; }

    [SerializeField] private List<DoorData> doorDataList = new List<DoorData>();

    private readonly Dictionary<DoorID, bool>     states   = new Dictionary<DoorID, bool>();
    private readonly Dictionary<DoorID, DoorData> dataDict = new Dictionary<DoorID, DoorData>();

    // 緊急封鎖の残り秒数 (0 = 非アクティブ)
    public float EmergencyCountdown { get; private set; }

    public event Action<DoorID, bool> OnDoorChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (doorDataList.Count == 0)
        {
            doorDataList = new List<DoorData>
            {
                new DoorData { id = DoorID.Gate,           displayName = "外壁ゲート",     drainPerSecond = 3f/60f, blocksDestination = FacilityLocation.Outside_Top  },
                new DoorData { id = DoorID.Entrance,       displayName = "地上入口ドア",   drainPerSecond = 2f/60f, blocksDestination = FacilityLocation.Lobby_Main   },
                new DoorData { id = DoorID.BasementStairs, displayName = "地下階段ドア",   drainPerSecond = 4f/60f, blocksDestination = FacilityLocation.B1_Corridor  },
                new DoorData { id = DoorID.B1Corridor,     displayName = "B1廊下ドア",     drainPerSecond = 5f/60f, blocksDestination = FacilityLocation.B1_DoorFront },
            };
        }

        foreach (var d in doorDataList)
        {
            dataDict[d.id] = d;
            states[d.id]   = false;
        }
    }

    private void Start()
    {
        GameManager.Instance.OnDayStarted += _ => OpenAll();
        PowerManager.Instance.OnPowerOut  += OpenAll;
    }

    private void Update()
    {
        if (EmergencyCountdown > 0f)
            EmergencyCountdown = Mathf.Max(0f, EmergencyCountdown - Time.deltaTime);
    }

    // ─── パブリック操作 ────────────────────────────────────────────

    public void Toggle(DoorID id)
    {
        if (PowerManager.Instance.IsPowerOut && !states[id]) return;
        SetDoor(id, !states[id]);
    }

    public void SetDoor(DoorID id, bool closed)
    {
        if (!dataDict.ContainsKey(id)) return;

        bool prev = states[id];
        states[id] = closed;

        if (closed && !prev)
        {
            PowerManager.Instance.AddDrain($"door_{id}", dataDict[id].drainPerSecond);
            AudioManager.Instance?.Play("door_close");
        }
        else if (!closed && prev)
        {
            PowerManager.Instance.RemoveDrain($"door_{id}");
            AudioManager.Instance?.Play("door_open");
        }

        OnDoorChanged?.Invoke(id, closed);
    }

    public void EmergencyLockdown()
    {
        if (!PowerManager.Instance.Consume(20f)) return;
        AudioManager.Instance?.Play("power_flicker");
        EmergencyCountdown = 10f;
        foreach (var id in dataDict.Keys) SetDoor(id, true);
        StartCoroutine(OpenAllAfter(10f));
    }

    public void OpenAll()
    {
        EmergencyCountdown = 0f;
        foreach (var id in dataDict.Keys) SetDoor(id, false);
    }

    // ─── クエリ ────────────────────────────────────────────────────

    public bool IsClosed(DoorID id) => states.TryGetValue(id, out bool s) && s;

    public bool IsBlocked(FacilityLocation destination)
    {
        foreach (var kvp in states)
            if (kvp.Value && dataDict[kvp.Key].blocksDestination == destination)
                return true;
        return false;
    }

    public DoorData GetData(DoorID id) => dataDict.TryGetValue(id, out var d) ? d : null;

    // ─── 内部 ──────────────────────────────────────────────────────

    private IEnumerator OpenAllAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        OpenAll();
    }
}
