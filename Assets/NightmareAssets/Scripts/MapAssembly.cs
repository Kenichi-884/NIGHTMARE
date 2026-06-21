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

    // ────────────────────────────────────────────────────────────────
    //  監視カメラ ── ビジュアルプレハブ
    // ────────────────────────────────────────────────────────────────

    [Header("── 監視カメラ（共通モデル） ───────────────────────────")]
    [Tooltip("全カメラに使う既定の CCTV 外観プレハブ。\n" +
             "NIGHTMARE > Generate CCTV Prefab で自動生成。\n" +
             "このプレハブを Prefab Mode で変更すると全台が更新される。\n" +
             "個別スロットに設定すると、そのカメラだけ上書きできる。")]
    public GameObject securityCameraVisualPrefab;

    [Header("── 監視カメラ（個別オーバーライド / null = 共通モデル使用）──")]
    [Tooltip("null のカメラは securityCameraVisualPrefab を使用する。")]
    public GameObject camPrefab_OUT_N;
    public GameObject camPrefab_OUT_E;
    public GameObject camPrefab_OUT_W;
    public GameObject camPrefab_OUT_TOP;
    public GameObject camPrefab_IN_1F_A;
    public GameObject camPrefab_IN_1F_B;
    public GameObject camPrefab_IN_B1_A;
    public GameObject camPrefab_IN_B1_B;

    /// <summary>カメラ名（SceneCam_XXX の XXX 部分）に対応するプレハブを返す。</summary>
    public GameObject GetCamPrefab(string camSuffix)
    {
        switch (camSuffix)
        {
            case "OUT_N":   return camPrefab_OUT_N   ?? securityCameraVisualPrefab;
            case "OUT_E":   return camPrefab_OUT_E   ?? securityCameraVisualPrefab;
            case "OUT_W":   return camPrefab_OUT_W   ?? securityCameraVisualPrefab;
            case "OUT_TOP": return camPrefab_OUT_TOP ?? securityCameraVisualPrefab;
            case "IN_1F_A": return camPrefab_IN_1F_A ?? securityCameraVisualPrefab;
            case "IN_1F_B": return camPrefab_IN_1F_B ?? securityCameraVisualPrefab;
            case "IN_B1_A": return camPrefab_IN_B1_A ?? securityCameraVisualPrefab;
            case "IN_B1_B": return camPrefab_IN_B1_B ?? securityCameraVisualPrefab;
            default:        return securityCameraVisualPrefab;
        }
    }

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

    // ────────────────────────────────────────────────────────────────
    //  3D マップ ── マテリアル差し替え
    //  null = プロシージャル生成、設定済み = そのマテリアルを使用
    // ────────────────────────────────────────────────────────────────

    [Header("── 3Dマップ マテリアル（null = プロシージャル生成）──────────")]
    [Tooltip("外壁・廊下の壁マテリアル。null = 内部生成（wall_dark テクスチャ）")]
    public Material matWall;

    [Tooltip("1F コンクリート床マテリアル。null = 内部生成（concrete テクスチャ）")]
    public Material matConcrete;

    [Tooltip("B1 壁マテリアル。null = matWall と同じプロシージャル生成にフォールバック")]
    public Material matB1Wall;

    [Tooltip("B1 床マテリアル。null = 内部生成（b1_floor テクスチャ）")]
    public Material matB1Floor;

    [Tooltip("ドア枠・レールのマテリアル。null = 内部生成（metal テクスチャ）")]
    public Material matDoorFrame;

    [Tooltip("シャッターパネルのマテリアル。null = 内部生成（metal テクスチャ）")]
    public Material matDoorPanel;

    [Tooltip("管理人室床マテリアル。null = 内部生成（mgr_floor テクスチャ）")]
    public Material matMgrFloor;

    [Tooltip("金属マテリアル（CCTV 本体・手すりなど）。null = 内部生成")]
    public Material matMetal;

    [Tooltip("外装コンクリートマテリアル（歩道など）。null = 内部生成（pavement テクスチャ）")]
    public Material matPavement;
}
