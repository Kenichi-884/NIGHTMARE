using UnityEngine;

/// <summary>
/// 3Dマップの各セクション・UIマップのテンプレートをまとめる ScriptableObject。
///
/// ─ 使い方 ─────────────────────────────────────────────────────────────
/// 【初回セットアップ】
///   1. NIGHTMARE > Build Temp 3D Map       → プロシージャルで全生成
///   2. NIGHTMARE > Extract Map Sections    → 各セクションを個別 .prefab に保存
///      → Assets/Prefabs/Map/Sections/ に保存され、このアセットに自動登録される
///
/// 【パーツ差し替え】
///   3. 対象の .prefab を Prefab Mode で開く
///   4. キューブを削除して実際のモデルを配置する
///   5. NIGHTMARE > Build Temp 3D Map       → 差し替えたプレハブが使われる
///
/// 【元に戻す（プロシージャルに戻す）】
///   6. 対象スロットを None (null) にクリアする → 次のビルドでキューブ再生成
///
/// ─ null スロット = プロシージャル生成にフォールバック ──────────────────
/// </summary>
[CreateAssetMenu(fileName = "MapAssembly", menuName = "NIGHTMARE/Map Assembly")]
public class MapAssembly : ScriptableObject
{
    // ────────────────────────────────────────────────────────────────
    //  3D マップ ── 構造セクション
    // ────────────────────────────────────────────────────────────────

    [Header("── 外部エリア ─────────────────────────────────────")]
    [Tooltip("null = プロシージャル生成 / 設定済み = このプレハブをインスタンス化")]
    public GameObject areaExterior;

    [Header("── 1F ロビー ──────────────────────────────────────")]
    public GameObject area1F;

    [Header("── 階段 ─────────────────────────────────────────")]
    public GameObject areaStaircase;

    [Header("── B1 地下 ──────────────────────────────────────")]
    public GameObject areaB1;

    // ────────────────────────────────────────────────────────────────
    //  3D マップ ── ドア（個別差し替え可）
    // ────────────────────────────────────────────────────────────────

    [Header("── ドア ─────────────────────────────────────────")]
    [Tooltip("DoorAnimator コンポーネントを持つ GameObject が最低限必要。\n" +
             "doorID は自動的に上書きされる。")]
    public GameObject doorGate;
    public GameObject doorEntrance;
    public GameObject doorBasementStairs;
    public GameObject doorB1Corridor;

    // ────────────────────────────────────────────────────────────────
    //  3D マップ ── 環境・装飾
    // ────────────────────────────────────────────────────────────────

    [Header("── プロップ ─────────────────────────────────────")]
    public GameObject areaProps;

    [Header("── 屋根 ──────────────────────────────────────────")]
    public GameObject areaRoof;

    [Header("── サイン・サイネージ ──────────────────────────────")]
    public GameObject areaSignage;

    [Header("── CCTV マウント ──────────────────────────────────")]
    public GameObject areaCCTV;

    // ────────────────────────────────────────────────────────────────
    //  UI マップ ── テンプレート
    // ────────────────────────────────────────────────────────────────

    [Header("── UI マップ ─────────────────────────────────────")]
    [Tooltip("FacilityMapUI が各部屋ノード生成時に使う UI テンプレート。\n" +
             "null = コードで直接生成にフォールバック。\n" +
             "テンプレートには Image・Outline・Text・MapRoomNode が必要。")]
    public GameObject uiRoomNodeTemplate;

    [Tooltip("接続線のテンプレート。null = コード生成。")]
    public GameObject uiConnectionTemplate;
}
