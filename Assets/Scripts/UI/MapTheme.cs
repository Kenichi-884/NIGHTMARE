using UnityEngine;

/// <summary>
/// マップ全体の色・サイズ設定を一括管理する ScriptableObject。
/// Assets/Data/MapTheme.asset を Inspector で編集するだけで
/// マップの全ビジュアルをまとめて変更できる。
/// メニュー: Assets > Create > NIGHTMARE > Map Theme
/// </summary>
[CreateAssetMenu(fileName = "MapTheme", menuName = "NIGHTMARE/Map Theme")]
public class MapTheme : ScriptableObject
{
    [Header("── 背景 ──────────────────────────────")]
    public Color mapBg          = new Color(0.04f, 0.06f, 0.10f, 0.97f);
    public Color zoneOutdoor    = new Color(0.06f, 0.10f, 0.18f, 0.55f);
    public Color zone1F         = new Color(0.08f, 0.13f, 0.22f, 0.50f);
    public Color zoneB1         = new Color(0.04f, 0.07f, 0.13f, 0.62f);
    public Color zoneBorder     = new Color(0.18f, 0.28f, 0.46f, 0.50f);

    [Header("── 接続線 ────────────────────────────")]
    public Color lineColor      = new Color(0.22f, 0.34f, 0.54f, 1.00f);
    public Color arrowColor     = new Color(0.32f, 0.52f, 0.74f, 1.00f);
    [Range(1f, 5f)]
    public float lineWidth      = 2f;

    [Header("── ルーム ────────────────────────────")]
    public Color roomBg         = new Color(0.10f, 0.15f, 0.24f, 1.00f);
    public Color roomBgYou      = new Color(0.06f, 0.20f, 0.11f, 1.00f);
    public Color roomBgSide     = new Color(0.08f, 0.12f, 0.20f, 1.00f);
    public Color roomBorder     = new Color(0.28f, 0.50f, 0.76f, 1.00f);
    public Color roomBorderYou  = new Color(0.22f, 0.72f, 0.36f, 1.00f);
    public Color roomLabel      = new Color(0.82f, 0.93f, 1.00f, 1.00f);
    public Color roomDanger     = new Color(0.40f, 0.04f, 0.04f, 1.00f);

    [Header("── ドア ──────────────────────────────")]
    public Color doorOpen       = new Color(0.14f, 0.82f, 0.22f, 1.00f);
    public Color doorClosed     = new Color(0.90f, 0.14f, 0.07f, 1.00f);

    [Header("── カメラ ────────────────────────────")]
    public Color camActive      = new Color(0.18f, 0.90f, 1.00f, 1.00f);
    public Color camNormal      = new Color(0.12f, 0.52f, 0.82f, 1.00f);
    public Color camDead        = new Color(0.32f, 0.32f, 0.35f, 1.00f);

    [Header("── モンスター ──────────────────────────")]
    public Color monCrawler     = new Color(1.00f, 0.28f, 0.08f, 1.00f);
    public Color monRusher      = new Color(1.00f, 0.62f, 0.00f, 1.00f);
    public Color monJammer      = new Color(0.68f, 0.16f, 1.00f, 1.00f);
    public Color monLurker      = new Color(0.50f, 0.52f, 0.60f, 1.00f);
    public Color monMimic       = new Color(0.10f, 0.90f, 0.88f, 1.00f);
    public Color monKnocker     = new Color(1.00f, 0.98f, 0.16f, 1.00f);

    [Header("── テキスト ──────────────────────────")]
    public Color textTitle      = new Color(0.44f, 0.76f, 1.00f, 1.00f);
    public Color textSub        = new Color(0.38f, 0.50f, 0.70f, 0.85f);
    public Color textHint       = new Color(0.28f, 0.36f, 0.50f, 0.80f);
    public Color textDoorLabel  = new Color(0.85f, 0.82f, 0.44f, 0.90f);
    public Color textLegend     = new Color(0.72f, 0.80f, 0.90f, 1.00f);

    // ── モンスターカラー取得 ──────────────────────
    public Color GetMonsterColor(MonsterType t) => t switch
    {
        MonsterType.Crawler => monCrawler,
        MonsterType.Rusher  => monRusher,
        MonsterType.Jammer  => monJammer,
        MonsterType.Lurker  => monLurker,
        MonsterType.Mimic   => monMimic,
        MonsterType.Knocker => monKnocker,
        _                   => Color.white
    };
}
