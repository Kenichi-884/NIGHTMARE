using UnityEngine;
using System.Collections.Generic;

// カメラを破壊する妨害タイプ。外周を徘徊しカメラが映っている間に攻撃。
public class JammerAI : MonsterBase
{
    [SerializeField] private CameraID targetCameraID = CameraID.OUT_E;
    private float attackTimer = 0f;
    private const float ATTACK_TIME = 25f; // 25秒カメラに映り続けると破壊

    public override MonsterType MonsterType => MonsterType.Jammer;

    public override void Initialize(FacilityLocation spawnLocation, int day)
    {
        baseMoveInterval = 30f;
        base.Initialize(spawnLocation, day);
    }

    // MonsterSpawnerからターゲットカメラを指定する
    public void SetTarget(CameraID cameraID) => targetCameraID = cameraID;

    protected override List<FacilityLocation> BuildPath() => new List<FacilityLocation>
    {
        FacilityLocation.Outside_East,
        FacilityLocation.Outside_West,
        FacilityLocation.Outside_East, // ループのように見せる
    };

    protected override void Update()
    {
        base.Update();

        if (!isActive) return;

        bool isBeingWatched = SecurityCameraSystem.Instance.IsLocationVisible(currentLocation);

        if (isBeingWatched)
        {
            attackTimer += Time.deltaTime;
            if (attackTimer >= ATTACK_TIME)
            {
                SecurityCameraSystem.Instance.KillCamera(targetCameraID);
                AudioManager.Instance?.Play("camera_destroyed");
                Remove();
            }
        }
        else
        {
            attackTimer = Mathf.Max(0f, attackTimer - Time.deltaTime * 0.5f); // 見ていない間は回復
        }
    }

    // プレイヤーが駆除ボタンを押したとき（UIManagerから呼ぶ）
    public static void TryEliminateVisible()
    {
        if (!PowerManager.Instance.Consume(5f)) return;

        // Remove() がリストを変更するので先にコピーしてから反復
        var snapshot = new List<MonsterBase>(MonsterManager.Instance.ActiveMonsters);
        foreach (var m in snapshot)
        {
            if (m is not JammerAI j) continue;
            if (SecurityCameraSystem.Instance.IsLocationVisible(j.currentLocation))
            {
                j.Remove();
                return;
            }
        }
    }
}
