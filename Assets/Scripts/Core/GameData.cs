// 共有データ定義 - enum / 定数

public enum GamePhase
{
    Silence,        // 20:00
    Omen,           // 21:00
    Contact,        // 22:00
    Increase,       // 23:00
    Erosion,        // 00:00
    Infiltration,   // 01:00
    Siege,          // 02:00
    Collapse,       // 03:00
    Abyss,          // 04:00
    BeforeDawn,     // 05:00
    Dawn            // 06:00 クリア
}

public enum GameState
{
    MainMenu,
    Night,
    DayTransition,
    GameOver,
    TrueEnding
}

public enum FacilityLocation
{
    Outside_North,
    Outside_East,
    Outside_West,
    Outside_Top,    // 建物直前（地上入口前）
    Lobby_Main,
    Lobby_Stairs,
    B1_Corridor,
    B1_DoorFront,   // 管理人室の直前
    ManagersRoom    // = ゲームオーバー
}

public enum CameraID
{
    OUT_N,      // 正面ゲート・駐車場
    OUT_E,      // 東側非常口
    OUT_W,      // 西側搬入口
    OUT_TOP,    // 地上入口の真上
    IN_1F_A,    // ロビー正面
    IN_1F_B,    // 階段・EV前
    IN_B1_A,    // B1メイン廊下
    IN_B1_B     // 管理人室前廊下
}

public enum DoorID
{
    Gate,           // 外壁ゲート       -3%/分
    Entrance,       // 地上入口ドア     -2%/分
    BasementStairs, // 地下への階段ドア -4%/分
    B1Corridor      // B1廊下ドア       -5%/分
}

public enum MonsterType
{
    Crawler,
    Jammer,
    Rusher,
    Lurker,
    Mimic,
    Knocker
}

public enum PhenomenaType
{
    CameraFlicker,      // カメラが一瞬切れる
    PowerFluctuation,   // 電力が急激に減る
    LightFailure,       // カメラ映像が暗くなる
    AudioDistortion,    // 足音フェイクが鳴る
    TimeWarp,           // ゲーム内時計が加速する
    GhostSignal,        // 偽モンスターがカメラに映る
    Blackout            // 全カメラが5秒暗転する
}
