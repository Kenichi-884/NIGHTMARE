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

    [SerializeField] private List<DoorData> doorDataList = new List<DoorData>
    {
        // デフォルト値（エディタ上で上書き可能）
    };

    private readonly Dictionary<DoorID, bool> states = new Dictionary<DoorID, bool>();
    private readonly Dictionary<DoorID, DoorData> dataDict = new Dictionary<DoorID, DoorData>();

    public event Action<DoorID, bool> OnDoorChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // デフォルトデータ（エディタで設定していない場合のフォールバック）
        if (doorDataList.Count == 0)
        {
            doorDataList = new List<DoorData>
            {
                new DoorData { id = DoorID.Gate,           displayName = "外壁ゲート",     drainPerSecond = 3f/60f, blocksDestination = FacilityLocation.Outside_Top },
                new DoorData { id = DoorID.Entrance,       displayName = "地上入口ドア",   drainPerSecond = 2f/60f, blocksDestination = FacilityLocation.Lobby_Main },
                new DoorData { id = DoorID.BasementStairs, displayName = "地下階段ドア",   drainPerSecond = 4f/60f, blocksDestination = FacilityLocation.B1_Corridor },
                new DoorData { id = DoorID.B1Corridor,     displayName = "B1廊下ドア",     drainPerSecond = 5f/60f, blocksDestination = FacilityLocation.B1_DoorFront },
            };
        }

        foreach (var d in doorDataList)
        {
            dataDict[d.id] = d;
            states[d.id] = false; // 全て開放状態で開始
        }
    }

    private void Start()
    {
        GameManager.Instance.OnDayStarted += _ => OpenAll();
        PowerManager.Instance.OnPowerOut += OpenAll;
    }

    public void Toggle(DoorID id)
    {
        if (PowerManager.Instance.IsPowerOut && !states[id]) return; // 停電中は閉められない
        SetDoor(id, !states[id]);
    }

    public void SetDoor(DoorID id, bool closed)
    {
        if (!dataDict.ContainsKey(id)) return;

        bool prev = states[id];
        states[id] = closed;

        if (closed && !prev)
            PowerManager.Instance.AddDrain($"door_{id}", dataDict[id].drainPerSecond);
        else if (!closed && prev)
            PowerManager.Instance.RemoveDrain($"door_{id}");

        OnDoorChanged?.Invoke(id, closed);
    }

    public bool IsClosed(DoorID id) => states.TryGetValue(id, out bool s) && s;

    // モンスターが次の場所へ移動しようとしたとき、ブロックされるかチェック
    public bool IsBlocked(FacilityLocation destination)
    {
        foreach (var kvp in states)
        {
            if (kvp.Value && dataDict[kvp.Key].blocksDestination == destination)
                return true;
        }
        return false;
    }

    public void EmergencyLockdown()
    {
        if (!PowerManager.Instance.Consume(20f)) return;
        foreach (var id in dataDict.Keys) SetDoor(id, true);
        StartCoroutine(OpenAllAfter(10f));
    }

    private IEnumerator OpenAllAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        OpenAll();
    }

    public void OpenAll()
    {
        foreach (var id in dataDict.Keys) SetDoor(id, false);
    }

    public DoorData GetData(DoorID id) => dataDict.TryGetValue(id, out var d) ? d : null;
}
