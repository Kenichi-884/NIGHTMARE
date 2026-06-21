#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// NIGHTMARE > Environment > Forest Placer Window
/// ・地面Raycast吸着
/// ・傾斜法線に合わせた回転
/// ・建物Colliderとの重なり回避
/// ・Inspector風EditorWindowでパラメータ調整＆Sceneビューリングプレビュー
/// </summary>
public class ForestEnvironmentWindow : EditorWindow
{
    // ── アセットパス ─────────────────────────────────────────────────
    private const string ROOT =
        "Assets/UnityAsset/Model/Map/Environment/NatureManufacture Assets/" +
        "Forest Environment Dynamic Nature/";

    // ── プリセット定義 ───────────────────────────────────────────────
    private static readonly PrefabEntry[] RING1_ENTRIES =
    {
        new(ROOT+"Rocks/Prefabs/prefab_beech_forest_stones_01_1.prefab",  3, 0.8f,1.6f),
        new(ROOT+"Rocks/Prefabs/prefab_beech_forest_stones_01_3.prefab",  3, 0.7f,1.4f),
        new(ROOT+"Rocks/Prefabs/prefab_beech_forest_stones_01_5.prefab",  4, 0.6f,1.2f),
        new(ROOT+"Rocks/Prefabs/prefab_beech_forest_stones_01_8.prefab",  3, 0.8f,1.5f),
        new(ROOT+"Rocks/Prefabs/prefab_beech_forest_stones_01_12.prefab", 2, 1.0f,2.0f),
        new(ROOT+"Foliage and Grass/Prefabs/prefab_fern_01_1.prefab",     8, 0.8f,1.4f),
        new(ROOT+"Foliage and Grass/Prefabs/prefab_fern_01_2.prefab",     7, 0.9f,1.5f),
        new(ROOT+"Foliage and Grass/Prefabs/prefab_fern_01_3.prefab",     6, 0.7f,1.2f),
        new(ROOT+"Foliage and Grass/Prefabs/prefab_plant_01_1.prefab",    6, 0.8f,1.3f),
        new(ROOT+"Foliage and Grass/Prefabs/prefab_plant_02_1.prefab",    5, 0.9f,1.4f),
        new(ROOT+"Foliage and Grass/Prefabs/prefab_plant_03_1.prefab",    4, 0.8f,1.2f),
        new(ROOT+"Mushrooms/Prefabs/prefab_Beech_mushroom_01.prefab",     4, 0.7f,1.2f),
        new(ROOT+"Mushrooms/Prefabs/prefab_Beech_mushroom_02A.prefab",    3, 0.8f,1.3f),
        new(ROOT+"Mushrooms/Prefabs/prefab_Mushroom_Armillaria_01.prefab",3, 0.8f,1.2f),
        new(ROOT+"Bushes/Prefabs/Prefab_Forest_black_cherry_02.prefab",   5, 0.8f,1.3f),
        new(ROOT+"Bushes/Prefabs/Prefab_Forest_black_cherry_04.prefab",   4, 0.9f,1.4f),
    };

    private static readonly PrefabEntry[] RING2_ENTRIES =
    {
        new(ROOT+"Beech Trees/Prefabs/prefab_beech_plant_00.prefab",      6, 0.9f,1.3f),
        new(ROOT+"Beech Trees/Prefabs/prefab_beech_plant_01.prefab",      5, 1.0f,1.5f),
        new(ROOT+"Beech Trees/Prefabs/prefab_beech_plant_02.prefab",      5, 0.8f,1.2f),
        new(ROOT+"Beech Trees/Prefabs/prefab_beech_tree_05.prefab",       4, 0.7f,1.0f),
        new(ROOT+"Beech Trees/Prefabs/prefab_beech_tree_07.prefab",       4, 0.7f,1.0f),
        new(ROOT+"Bushes/Prefabs/Prefab_Forest_black_cherry_00.prefab",   6, 0.9f,1.5f),
        new(ROOT+"Bushes/Prefabs/Prefab_Forest_black_cherry_01.prefab",   5, 1.0f,1.6f),
        new(ROOT+"Bushes/Prefabs/Prefab_Forest_black_cherry_03.prefab",   5, 0.8f,1.4f),
        new(ROOT+"Bushes/Prefabs/Prefab_Forest_black_cherry_05.prefab",   4, 0.9f,1.5f),
        new(ROOT+"Stumps Roots and Branches/Prefabs/prefab_beech_forest_stump_01_1.prefab", 3,0.9f,1.4f),
        new(ROOT+"Stumps Roots and Branches/Prefabs/prefab_beech_forest_stump_02_01.prefab",2,1.0f,1.5f),
        new(ROOT+"Stumps Roots and Branches/Prefabs/prefab_dead_log_01.prefab", 3, 0.8f,1.2f),
        new(ROOT+"Stumps Roots and Branches/Prefabs/prefab_dead_log_03.prefab", 3, 0.9f,1.4f),
        new(ROOT+"Stumps Roots and Branches/Prefabs/prefab_dead_log_05.prefab", 2, 1.0f,1.5f),
        new(ROOT+"Rocks/Prefabs/prefab_beech_forest_stones_01_6.prefab",  3, 1.0f,2.0f),
        new(ROOT+"Rocks/Prefabs/prefab_beech_forest_stones_01_10.prefab", 3, 1.2f,2.5f),
        new(ROOT+"Stumps Roots and Branches/Prefabs/prefab_beech_old_roots_01.prefab",2,0.9f,1.3f),
    };

    private static readonly PrefabEntry[] RING3_ENTRIES =
    {
        new(ROOT+"Beech Trees/Prefabs/prefab_beech_tree_00_A.prefab", 12, 0.9f,1.4f),
        new(ROOT+"Beech Trees/Prefabs/prefab_beech_tree_00_B.prefab", 10, 1.0f,1.5f),
        new(ROOT+"Beech Trees/Prefabs/prefab_beech_tree_00_C.prefab",  9, 0.8f,1.3f),
        new(ROOT+"Beech Trees/Prefabs/prefab_beech_tree_00_D.prefab",  8, 1.0f,1.6f),
        new(ROOT+"Beech Trees/Prefabs/prefab_beech_tree_01.prefab",   10, 0.9f,1.4f),
        new(ROOT+"Beech Trees/Prefabs/prefab_beech_tree_02.prefab",    9, 0.8f,1.3f),
        new(ROOT+"Beech Trees/Prefabs/prefab_beech_tree_03.prefab",    8, 1.0f,1.5f),
        new(ROOT+"Beech Trees/Prefabs/prefab_beech_tree_04.prefab",    7, 0.9f,1.4f),
        new(ROOT+"Beech Trees/Prefabs/prefab_beech_tree_06.prefab",    8, 1.0f,1.5f),
        new(ROOT+"Beech Trees/Prefabs/prefab_beech_tree_08.prefab",    7, 0.8f,1.3f),
        new(ROOT+"Beech Trees/Prefabs/prefab_beech_tree_09.prefab",    6, 1.0f,1.6f),
        new(ROOT+"Foliage and Grass/Prefabs/prefab_fern_01_4.prefab",  8, 0.9f,1.5f),
        new(ROOT+"Bushes/Prefabs/Prefab_Forest_black_cherry_00.prefab",8, 1.0f,1.8f),
        new(ROOT+"Bushes/Prefabs/Prefab_Forest_black_cherry_01.prefab",7, 0.9f,1.6f),
        new(ROOT+"Stumps Roots and Branches/Prefabs/prefab_dead_log_02.prefab",4,1.0f,1.5f),
        new(ROOT+"Stumps Roots and Branches/Prefabs/prefab_dead_log_04.prefab",3,1.0f,1.5f),
        new(ROOT+"Rocks/Prefabs/prefab_beech_forest_stones_01_2.prefab",  4,1.0f,2.2f),
        new(ROOT+"Rocks/Prefabs/prefab_beech_forest_stones_01_14.prefab", 3,1.2f,2.5f),
    };

    // ── フィールド ────────────────────────────────────────────────────
    // 基本
    private bool   _autoFindCenter = true;
    private Vector3 _center        = Vector3.zero;
    private float  _densityMult    = 1f;
    private int    _seed           = 42;

    // リング半径
    private float _r1In  = 14f,  _r1Out = 38f;
    private float _r2In  = 32f,  _r2Out = 65f;
    private float _r3In  = 55f,  _r3Out = 130f;

    // 地面吸着
    private bool      _snapToGround   = true;
    private LayerMask _groundMask     = ~0;       // デフォルト: 全レイヤー
    private float     _rayOriginY     = 200f;     // Rayを飛ばす高さ
    private float     _groundFallback = 0f;       // Rayが当たらない場合のY

    // 傾斜回転
    private bool _alignToSlope = true;

    // 建物回避
    private bool      _avoidBuilding    = true;
    private LayerMask _buildingMask     = ~0;
    private float     _buildingClearR   = 14f;    // 距離ベースのクリアゾーン
    private float     _buildingCheckR   = 1.0f;   // Collider重複チェック球半径

    // UI状態
    private Vector2 _scroll;
    private bool    _foldBasic   = true;
    private bool    _foldGround  = true;
    private bool    _foldBuild   = true;
    private bool    _foldRings   = true;
    private bool    _foldPreview = true;
    private bool    _showPreview = true;

    // ── メニュー ─────────────────────────────────────────────────────
    [MenuItem("NIGHTMARE/Environment/Forest Placer Window")]
    static void Open() => GetWindow<ForestEnvironmentWindow>("Forest Placer");

    // 旧メニューも維持（後方互換）
    [MenuItem("NIGHTMARE/Environment/Place Mountain Forest")]
    static void PlaceFromMenu() => GetWindow<ForestEnvironmentWindow>("Forest Placer");

    [MenuItem("NIGHTMARE/Environment/Clear Forest")]
    static void ClearFromMenu()
    {
        var go = GameObject.Find("ForestEnvironment");
        if (go == null) { Debug.LogWarning("[ForestPlacer] ForestEnvironment が見つかりません。"); return; }
        Undo.DestroyObjectImmediate(go);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("[ForestPlacer] ForestEnvironment を削除しました。");
    }

    // ── ライフサイクル ────────────────────────────────────────────────
    private void OnEnable()  => SceneView.duringSceneGui += OnSceneGUI;
    private void OnDisable() => SceneView.duringSceneGui -= OnSceneGUI;

    // ── GUI ──────────────────────────────────────────────────────────
    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        // ── 基本設定 ──────────────────────────────────────────────
        _foldBasic = EditorGUILayout.BeginFoldoutHeaderGroup(_foldBasic, "基本設定");
        if (_foldBasic)
        {
            EditorGUI.indentLevel++;
            _autoFindCenter = EditorGUILayout.Toggle("建物を自動検索", _autoFindCenter);
            using (new EditorGUI.DisabledScope(_autoFindCenter))
                _center = EditorGUILayout.Vector3Field("森の中心座標", _center);

            EditorGUILayout.Space(4);
            _densityMult = EditorGUILayout.Slider("密度", _densityMult, 0.1f, 3f);
            _seed        = EditorGUILayout.IntField("乱数シード（再現用）", _seed);
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(4);

        // ── 地面吸着 ──────────────────────────────────────────────
        _foldGround = EditorGUILayout.BeginFoldoutHeaderGroup(_foldGround, "地面への吸着");
        if (_foldGround)
        {
            EditorGUI.indentLevel++;
            _snapToGround = EditorGUILayout.Toggle("地面に吸着する", _snapToGround);
            using (new EditorGUI.DisabledScope(!_snapToGround))
            {
                _groundMask   = LayerMaskField("地面レイヤー", _groundMask);
                _rayOriginY   = EditorGUILayout.FloatField("Rayの開始高さ", _rayOriginY);
                _groundFallback = EditorGUILayout.FloatField("吸着失敗時のY", _groundFallback);
                _alignToSlope = EditorGUILayout.Toggle("傾斜に合わせて回転", _alignToSlope);
            }
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(4);

        // ── 建物回避 ──────────────────────────────────────────────
        _foldBuild = EditorGUILayout.BeginFoldoutHeaderGroup(_foldBuild, "建物との重なり回避");
        if (_foldBuild)
        {
            EditorGUI.indentLevel++;
            _avoidBuilding = EditorGUILayout.Toggle("建物と重ならない", _avoidBuilding);
            using (new EditorGUI.DisabledScope(!_avoidBuilding))
            {
                _buildingClearR = EditorGUILayout.Slider("クリアゾーン半径 (m)", _buildingClearR, 0f, 50f);
                _buildingMask   = LayerMaskField("建物レイヤー", _buildingMask);
                _buildingCheckR = EditorGUILayout.Slider("衝突チェック球半径 (m)", _buildingCheckR, 0.1f, 5f);
            }
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(4);

        // ── リング設定 ────────────────────────────────────────────
        _foldRings = EditorGUILayout.BeginFoldoutHeaderGroup(_foldRings, "リング半径");
        if (_foldRings)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("① 近景（岩・シダ・低木・キノコ）", EditorStyles.boldLabel);
            RingField(ref _r1In, ref _r1Out);

            EditorGUILayout.LabelField("② 中景（若木・切り株・丸太・大岩）", EditorStyles.boldLabel);
            RingField(ref _r2In, ref _r2Out);

            EditorGUILayout.LabelField("③ 遠景（ブナ大木・密林）", EditorStyles.boldLabel);
            RingField(ref _r3In, ref _r3Out);
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(4);

        // ── プレビュー ────────────────────────────────────────────
        _foldPreview = EditorGUILayout.BeginFoldoutHeaderGroup(_foldPreview, "Sceneプレビュー");
        if (_foldPreview)
        {
            EditorGUI.indentLevel++;
            _showPreview = EditorGUILayout.Toggle("リング範囲を表示", _showPreview);
            if (_showPreview) SceneView.RepaintAll();
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(12);

        // ── ボタン ────────────────────────────────────────────────
        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.backgroundColor = new Color(0.4f, 0.9f, 0.5f);
            if (GUILayout.Button("Place Forest", GUILayout.Height(36)))
                DoPlaceForest();

            GUI.backgroundColor = new Color(0.9f, 0.4f, 0.4f);
            if (GUILayout.Button("Clear Forest", GUILayout.Height(36)))
                DoClearForest();

            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(
            "Place Forest: 既存の ForestEnvironment があれば確認ダイアログを出します。\n" +
            "Clear Forest: ForestEnvironment を Undo 可能な形で削除します。\n" +
            "建物レイヤーを正しく設定すると Collider 重複を自動回避できます。",
            MessageType.Info);

        EditorGUILayout.EndScrollView();
    }

    // ── Sceneビューへのリングプレビュー ──────────────────────────────
    private void OnSceneGUI(SceneView sv)
    {
        if (!_showPreview) return;

        var c = _autoFindCenter ? FindBuildingCenter() : _center;

        DrawWireCircleXZ(c, _buildingClearR, new Color(1f, 0.3f, 0.3f, 0.8f), "クリア");
        DrawWireCircleXZ(c, _r1In,  new Color(0.8f, 1f, 0.4f, 0.5f));
        DrawWireCircleXZ(c, _r1Out, new Color(0.8f, 1f, 0.4f, 0.9f), "近景");
        DrawWireCircleXZ(c, _r2Out, new Color(0.4f, 0.9f, 0.4f, 0.9f), "中景");
        DrawWireCircleXZ(c, _r3Out, new Color(0.2f, 0.7f, 0.2f, 0.9f), "遠景");

        Handles.color = new Color(1f,1f,1f,0.4f);
        Handles.DrawWireDisc(c, Vector3.up, _r3Out);
    }

    // ── 配置ロジック ─────────────────────────────────────────────────
    private void DoPlaceForest()
    {
        // ── 安全確認ダイアログ ────────────────────────────────────
        // PrefabUtility.InstantiatePrefab を大量実行すると EditMode でも
        // [ExecuteInEditMode] スクリプトの Awake/Start が走ることがある。
        // タイトルルームや Map の初期化が誘発されないよう事前に警告する。
        if (!EditorUtility.DisplayDialog(
            "Forest Placer — 配置前確認",
            "植物プレハブを大量配置します。\n\n" +
            "【注意】シーン内に [ExecuteInEditMode] のスクリプトがある場合、\n" +
            "配置中に Awake/Start が走りタイトルルームや Map が\n" +
            "意図せず初期化されることがあります。\n\n" +
            "配置前に必ずシーンを保存（Ctrl+S）してください。\n\n" +
            "続けますか？",
            "保存済み。配置する", "キャンセル"))
            return;

        var existing = GameObject.Find("ForestEnvironment");
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog(
                "Forest Already Exists",
                "ForestEnvironment が既に存在します。削除して再配置しますか？",
                "再配置する", "キャンセル"))
                return;
            Undo.DestroyObjectImmediate(existing);
        }

        // TitleSceneDirector と MainMenuManager の ExecuteInEditMode を一時無効化
        SuppressEditModeScripts(true);

        var center = _autoFindCenter ? FindBuildingCenter() : _center;
        var root   = new GameObject("ForestEnvironment");
        Undo.RegisterCreatedObjectUndo(root, "Place Mountain Forest");

        Random.InitState(_seed);

        try
        {
            PlaceRing(root.transform, center, _r1In, _r1Out, RING1_ENTRIES);
            PlaceRing(root.transform, center, _r2In, _r2Out, RING2_ENTRIES);
            PlaceRing(root.transform, center, _r3In, _r3Out, RING3_ENTRIES);
        }
        finally
        {
            // 成功・失敗どちらでも必ず元に戻す
            SuppressEditModeScripts(false);
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"[ForestPlacer] 完了: {root.transform.childCount} 個配置 / 中心:{center}");
        Selection.activeGameObject = root;
    }

    // TitleSceneDirector / MainMenuManager / GameManager を EditMode 中だけ無効化して
    // Awake/Start の誤発火によるシーン初期化を防ぐ
    private static void SuppressEditModeScripts(bool disable)
    {
        string[] targets = { "TitleSceneDirector", "MainMenuManager", "GameManager", "UIManager" };
        foreach (var typeName in targets)
        {
            var type = System.Type.GetType(typeName + ", Assembly-CSharp");
            if (type == null) continue;
            foreach (var obj in Object.FindObjectsByType(type,
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (obj is MonoBehaviour mb) mb.enabled = !disable;
            }
        }
        if (disable)
            Debug.Log("[ForestPlacer] TitleSceneDirector 等を配置中のみ一時無効化しました。");
        else
            Debug.Log("[ForestPlacer] TitleSceneDirector 等を再有効化しました。");
    }

    private void DoClearForest()
    {
        var go = GameObject.Find("ForestEnvironment");
        if (go == null) { Debug.LogWarning("[ForestPlacer] ForestEnvironment が見つかりません。"); return; }
        Undo.DestroyObjectImmediate(go);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("[ForestPlacer] ForestEnvironment を削除しました。");
    }

    private void PlaceRing(Transform parent, Vector3 center, float innerR, float outerR, PrefabEntry[] entries)
    {
        foreach (var e in entries)
            PlaceObjects(parent, center, innerR, outerR, e);
    }

    private void PlaceObjects(Transform parent, Vector3 center, float innerR, float outerR, PrefabEntry e)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(e.path);
        if (prefab == null) { Debug.LogWarning($"[ForestPlacer] Prefab not found: {e.path}"); return; }

        int count = Mathf.Max(1, Mathf.RoundToInt(e.count * _densityMult));

        for (int i = 0; i < count; i++)
        {
            // リング内均一分布
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float r     = Mathf.Sqrt(Random.Range(innerR * innerR, outerR * outerR));
            var   xz    = center + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);

            // ① 距離ベースのクリアゾーン
            if (_avoidBuilding)
            {
                float distToCenter = Vector2.Distance(new Vector2(xz.x, xz.z),
                                                      new Vector2(center.x, center.z));
                if (distToCenter < _buildingClearR) continue;
            }

            // ② 地面吸着 + 法線取得
            Vector3 finalPos = xz;
            Vector3 normal   = Vector3.up;

            if (_snapToGround)
            {
                var origin = new Vector3(xz.x, center.y + _rayOriginY, xz.z);
                if (Physics.Raycast(origin, Vector3.down, out var hit, _rayOriginY + 500f, _groundMask))
                {
                    finalPos = hit.point;
                    normal   = hit.normal;
                }
                else
                {
                    finalPos = new Vector3(xz.x, center.y + _groundFallback, xz.z);
                }
            }

            // ③ 建物Collider重複チェック
            if (_avoidBuilding)
            {
                if (Physics.CheckSphere(finalPos + Vector3.up * 0.5f, _buildingCheckR, _buildingMask))
                    continue;
            }

            // ④ 配置
            float   rotY  = Random.Range(0f, 360f);
            float   scale = Random.Range(e.scaleMin, e.scaleMax);
            var     go    = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);

            go.transform.position   = finalPos;
            go.transform.localScale = Vector3.one * scale;

            // 傾斜回転: 法線方向 × ランダムY回転
            if (_alignToSlope && _snapToGround)
                go.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal)
                                      * Quaternion.Euler(0f, rotY, 0f);
            else
                go.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
        }
    }

    // ── ユーティリティ ────────────────────────────────────────────────
    private static Vector3 FindBuildingCenter()
    {
        string[] candidates = { "FacilityRoot", "Map", "Building", "Facility" };
        foreach (var name in candidates)
        {
            var go = GameObject.Find(name);
            if (go != null)
            {
                var p = go.transform.position;
                Debug.Log($"[ForestPlacer] 中心: 「{go.name}」({p})");
                return p;
            }
        }
        Debug.Log("[ForestPlacer] 建物が見つからないため原点を使用します。");
        return Vector3.zero;
    }

    private static void RingField(ref float inner, ref float outer)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel("内半径 / 外半径");
            inner = EditorGUILayout.FloatField(inner, GUILayout.Width(52));
            EditorGUILayout.LabelField("〜", GUILayout.Width(16));
            outer = EditorGUILayout.FloatField(outer, GUILayout.Width(52));
            EditorGUILayout.LabelField("m");
        }
        if (inner >= outer) inner = outer - 1f;
    }

    // LayerMaskのポップアップ
    private static LayerMask LayerMaskField(string label, LayerMask mask)
    {
        var layers = new List<string>();
        var values = new List<int>();
        for (int i = 0; i < 32; i++)
        {
            string n = LayerMask.LayerToName(i);
            if (!string.IsNullOrEmpty(n)) { layers.Add(n); values.Add(i); }
        }
        int current = 0;
        for (int i = 0; i < values.Count; i++)
            if ((mask & (1 << values[i])) != 0) current |= (1 << i);

        int next = EditorGUILayout.MaskField(label, current, layers.ToArray());
        int result = 0;
        for (int i = 0; i < values.Count; i++)
            if ((next & (1 << i)) != 0) result |= (1 << values[i]);
        return result;
    }

    private static void DrawWireCircleXZ(Vector3 center, float radius, Color color, string label = null)
    {
        Handles.color = color;
        Handles.DrawWireDisc(center, Vector3.up, radius);
        if (label != null)
        {
            var labelPos = center + new Vector3(radius, 0f, 0f);
            Handles.Label(labelPos, $"{label} ({radius:0}m)");
        }
    }

    // ── データ型 ──────────────────────────────────────────────────────
    private readonly struct PrefabEntry
    {
        public readonly string path;
        public readonly int    count;
        public readonly float  scaleMin, scaleMax;
        public PrefabEntry(string p, int c, float sMin, float sMax)
            { path = p; count = c; scaleMin = sMin; scaleMax = sMax; }
    }
}
#endif
