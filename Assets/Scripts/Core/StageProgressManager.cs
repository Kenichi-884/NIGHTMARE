using UnityEngine;

// ステージ進行管理: PlayerPrefs でセーブし、クリアした日の翌日を解放する
// DontDestroyOnLoad でシーンをまたいで保持される
public class StageProgressManager : MonoBehaviour
{
    public static StageProgressManager Instance { get; private set; }

    private const string PREF_KEY  = "MaxUnlockedDay";
    private const int    TOTAL_DAYS = 7;

    public int MaxUnlockedDay => PlayerPrefs.GetInt(PREF_KEY, 1);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        // シングルシーン設計 + PlayerPrefs永続化のため DontDestroyOnLoad 不要
    }

    private void Start()
    {
        GameManager.Instance.OnNightCleared += OnNightCleared;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnNightCleared -= OnNightCleared;
    }

    private void OnNightCleared(int clearedDay)
    {
        if (clearedDay < TOTAL_DAYS && clearedDay >= MaxUnlockedDay)
        {
            PlayerPrefs.SetInt(PREF_KEY, clearedDay + 1);
            PlayerPrefs.Save();
        }
    }

    public bool IsUnlocked(int day) => day <= MaxUnlockedDay;

    // エディタ・デバッグ用
    [ContextMenu("Unlock All Days")]
    public void UnlockAll()
    {
        PlayerPrefs.SetInt(PREF_KEY, TOTAL_DAYS);
        PlayerPrefs.Save();
    }

    [ContextMenu("Reset Progress")]
    public void ResetProgress()
    {
        PlayerPrefs.SetInt(PREF_KEY, 1);
        PlayerPrefs.Save();
    }
}
