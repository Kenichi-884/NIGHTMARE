using System.Collections.Generic;

// 猛スピードで突撃してくる。カメラに映る時間が短い。
public class RusherAI : MonsterBase
{
    public override MonsterType MonsterType => MonsterType.Rusher;

    public override void Initialize(FacilityLocation spawnLocation, int day)
    {
        baseMoveInterval = 10f; // 基本10秒/ステップ
        base.Initialize(spawnLocation, day);
    }

    protected override List<FacilityLocation> BuildPath() => new List<FacilityLocation>
    {
        FacilityLocation.Outside_East,
        FacilityLocation.Outside_Top,
        FacilityLocation.Lobby_Main,
        FacilityLocation.Lobby_Stairs,
        FacilityLocation.B1_Corridor,
        FacilityLocation.B1_DoorFront,
        FacilityLocation.ManagersRoom
    };

    protected override void OnMoved(FacilityLocation loc)
    {
        AudioManager.Instance?.Play("rusher_stomp");
    }
}
