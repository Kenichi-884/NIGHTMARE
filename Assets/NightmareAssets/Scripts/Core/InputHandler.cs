using UnityEngine;

// キーボード入力を監視システム操作にマッピングするシングルトン
// ゲーム中（GameState.Night）のみ受け付ける
//
// ─── 操作一覧 ───────────────────────────────────────────
//  ← →   : カメラ前後サイクル
//  1〜8   : カメラ直接選択（テンキー可）
//  Z      : 外壁ゲート 開閉
//  X      : 地上入口ドア 開閉
//  C      : 地下階段ドア 開閉
//  V      : B1廊下ドア 開閉
//  Space  : 緊急封鎖 (全ドア10秒閉鎖, -20%)
//  R      : 現在のカメラリセット (-3%)
//  E      : Jammer 駆除 (-5%)
//  M      : 施設マップ 表示切替
// ────────────────────────────────────────────────────────
public class InputHandler : MonoBehaviour
{
    public static InputHandler Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.CurrentState != GameState.Night) return;

        HandleCameraInput();
        HandleDoorInput();
        HandleActionInput();
    }

    // ===== カメラ切替 =====
    private void HandleCameraInput()
    {
        var cam = SecurityCameraSystem.Instance;
        if (cam == null) return;

        if (Input.GetKeyDown(KeyCode.LeftArrow))  cam.CycleCamera(-1);
        if (Input.GetKeyDown(KeyCode.RightArrow)) cam.CycleCamera(+1);

        // 1〜8 キーで直接選択（テンキーも対応）
        for (int i = 0; i < 8; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
            {
                cam.SetCameraByIndex(i);
                break;
            }
        }
    }

    // ===== ドア操作 =====
    private void HandleDoorInput()
    {
        var door = DoorManager.Instance;
        if (door == null) return;

        if (Input.GetKeyDown(KeyCode.Z))     door.Toggle(DoorID.Gate);
        if (Input.GetKeyDown(KeyCode.X))     door.Toggle(DoorID.Entrance);
        if (Input.GetKeyDown(KeyCode.C))     door.Toggle(DoorID.BasementStairs);
        if (Input.GetKeyDown(KeyCode.V))     door.Toggle(DoorID.B1Corridor);
        if (Input.GetKeyDown(KeyCode.Space)) door.EmergencyLockdown();
    }

    // ===== その他アクション =====
    private void HandleActionInput()
    {
        var cam = SecurityCameraSystem.Instance;
        if (cam == null) return;

        if (Input.GetKeyDown(KeyCode.R))
            cam.TryResetCamera(cam.ActiveCamera);

        if (Input.GetKeyDown(KeyCode.E))
            JammerAI.TryEliminateVisible();

        if (Input.GetKeyDown(KeyCode.M))
            UIManager.Instance?.ToggleMap();
    }
}
