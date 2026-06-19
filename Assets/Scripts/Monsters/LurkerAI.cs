using UnityEngine;
using System.Collections.Generic;

// 死角を使って移動する。カメラに映っている間は止まる。見失うと管理人室前に突然現れる。
public class LurkerAI : MonsterBase
{
    private bool isSeen = false;
    private float lostTimer = 0f;
    private const float LOST_TELEPORT_TIME = 20f; // 20秒見失うと管理人室前へテレポート

    public override MonsterType MonsterType => MonsterType.Lurker;
    public override bool IsVisible => isSeen; // カメラを切り替えた瞬間だけ映る

    public override void Initialize(FacilityLocation spawnLocation, int day)
    {
        baseMoveInterval = 45f;
        base.Initialize(spawnLocation, day);
    }

    protected override List<FacilityLocation> BuildPath() => new List<FacilityLocation>
    {
        FacilityLocation.Outside_West,
        FacilityLocation.Lobby_Main,
        FacilityLocation.B1_Corridor,   // 階段を飛ばす（死角を利用）
        FacilityLocation.B1_DoorFront,
        FacilityLocation.ManagersRoom
    };

    protected override void Update()
    {
        if (!isActive || GameManager.Instance.CurrentState != GameState.Night) return;

        // 扉前待機中はベースクラスの攻撃カウントダウンに委譲
        if (isAtDoorFront) { base.Update(); return; }

        bool currentlyVisible = SecurityCameraSystem.Instance.IsLocationVisible(currentLocation);

        if (currentlyVisible)
        {
            isSeen = true;
            lostTimer = 0f;
            moveTimer = 0f; // 見られている間は動かない
        }
        else
        {
            isSeen = false;
            lostTimer += Time.deltaTime;

            if (lostTimer >= LOST_TELEPORT_TIME)
            {
                lostTimer = 0f;
                TeleportToDoor();
            }
            else
            {
                base.Update();
            }
        }
    }

    private void TeleportToDoor()
    {
        if (isAtDoorFront) return; // 既に扉前にいる場合は再テレポート不要
        currentLocation = FacilityLocation.B1_DoorFront;
        pathIndex = movePath.Count; // パスを終端に設定（通常移動を停止）
        AudioManager.Instance?.Play("lurker_appear");
        EnterDoorFrontState();
    }
}
