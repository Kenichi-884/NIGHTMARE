using System.Collections.Generic;

// 標準侵入タイプ。ゆっくり確実に進んでくる。
public class CrawlerAI : MonsterBase
{
    public override MonsterType MonsterType => MonsterType.Crawler;

    protected override List<FacilityLocation> BuildPath() => new List<FacilityLocation>
    {
        FacilityLocation.Outside_North,
        FacilityLocation.Outside_Top,
        FacilityLocation.Lobby_Main,
        FacilityLocation.Lobby_Stairs,
        FacilityLocation.B1_Corridor,
        FacilityLocation.B1_DoorFront,
        FacilityLocation.ManagersRoom
    };
}
