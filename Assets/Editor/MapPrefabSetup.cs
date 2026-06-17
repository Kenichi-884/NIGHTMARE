// マップ関連のプレハブ・アセットをセットアップするエディタユーティリティ
// メニュー: NIGHTMARE > Setup Map Prefabs / Extract Map Sections

using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Collections.Generic;

public static class MapPrefabSetup
{
    private const string ThemePath    = "Assets/Data/MapTheme.asset";
    private const string AssemblyPath = "Assets/Data/MapAssembly.asset";
    private const string PrefabDir    = "Assets/Prefabs/Map";
    private const string SectionDir   = "Assets/Prefabs/Map/Sections";
    private const string PrefabPath   = "Assets/Prefabs/Map/MapPanel.prefab";

    // ─────────────────────────────────────────────────────────────────────
    // メニュー: テーマ作成 + MapPanel プレハブ保存
    // ─────────────────────────────────────────────────────────────────────
    [MenuItem("NIGHTMARE/Setup Map Prefabs", priority = 10)]
    public static void SetupMapPrefabs()
    {
        var theme    = EnsureMapTheme();
        var assembly = EnsureMapAssembly();
        var mapUI    = WireThemeAndAssemblyToScene(theme, assembly);

        if (mapUI != null)
            SaveMapPanelPrefab(mapUI.gameObject);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("NIGHTMARE",
            "セットアップ完了！\n\n" +
            "● Assets/Data/MapTheme.asset\n  UIマップの色を一括変更できます。\n\n" +
            "● Assets/Data/MapAssembly.asset\n  3Dマップ各パーツのプレハブ管理。\n\n" +
            "● Assets/Prefabs/Map/MapPanel.prefab\n  UIマップパネルのプレハブ。\n\n" +
            "次のステップ:\n" +
            "  NIGHTMARE > Build Temp 3D Map → 3Dマップ生成\n" +
            "  NIGHTMARE > Extract Map Sections → 各パーツをプレハブ化",
            "OK");
    }

    // ─────────────────────────────────────────────────────────────────────
    // メニュー: 3Dマップの各セクションを個別プレハブに切り出す
    // ─────────────────────────────────────────────────────────────────────
    [MenuItem("NIGHTMARE/Extract Map Sections to Prefabs", priority = 13)]
    public static void ExtractMapSections()
    {
        var mapRoot = GameObject.Find("[NIGHTMARE Map]");
        if (mapRoot == null)
        {
            EditorUtility.DisplayDialog("NIGHTMARE",
                "[NIGHTMARE Map] がシーンに見つかりません。\n" +
                "先に NIGHTMARE > Build Temp 3D Map を実行してください。", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("Extract Map Sections",
            "シーン内の [NIGHTMARE Map] から各セクションを\n" +
            "Assets/Prefabs/Map/Sections/ にプレハブ保存します。\n\n" +
            "既存のプレハブは上書きされます。続けますか？",
            "実行", "キャンセル")) return;

        EnsureFolder("Assets/Prefabs");
        EnsureFolder("Assets/Prefabs/Map");
        EnsureFolder(SectionDir);

        var assembly = EnsureMapAssembly();
        var so       = new SerializedObject(assembly);

        int saved = 0;

        // セクション名 → MapAssembly プロパティ名 のマッピング
        var sectionMap = new Dictionary<string, string>
        {
            { "Exterior",    "areaExterior"   },
            { "1F",          "area1F"         },
            { "Staircase",   "areaStaircase"  },
            { "B1",          "areaB1"         },
            { "Props",       "areaProps"      },
            { "Roof",        "areaRoof"       },
            { "Signage",     "areaSignage"    },
            { "CCTVMounts",  "areaCCTV"       },
        };

        // ドア名のプレフィックス → プロパティ名
        var doorMap = new Dictionary<string, string>
        {
            { "Door_Gate",           "doorGate"            },
            { "Door_Entrance",       "doorEntrance"        },
            { "Door_BasementStairs", "doorBasementStairs"  },
            { "Door_B1Corridor",     "doorB1Corridor"      },
        };

        foreach (Transform child in mapRoot.transform)
        {
            string cName = child.name;

            // ── 通常セクション ──
            if (sectionMap.TryGetValue(cName, out string propName))
            {
                var prefab = SaveSection(child.gameObject, cName);
                so.FindProperty(propName).objectReferenceValue = prefab;
                saved++;
                continue;
            }

            // ── ドアセクション（名前がプレフィックス一致）──
            foreach (var (prefix, dProp) in doorMap)
            {
                if (cName.StartsWith(prefix))
                {
                    var prefab = SaveSection(child.gameObject, prefix);
                    so.FindProperty(dProp).objectReferenceValue = prefab;
                    saved++;
                    break;
                }
            }
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(assembly);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorGUIUtility.PingObject(assembly);
        Debug.Log($"[NIGHTMARE] {saved} セクションをプレハブ保存 → MapAssembly に登録完了");

        EditorUtility.DisplayDialog("NIGHTMARE",
            $"{saved} 個のセクションをプレハブ化しました。\n\n" +
            "Assets/Prefabs/Map/Sections/ に保存されています。\n\n" +
            "【差し替え手順】\n" +
            "1. 対象の .prefab を Prefab Mode で開く\n" +
            "2. キューブを削除して実際のモデルを配置\n" +
            "3. NIGHTMARE > Build Temp 3D Map で再生成\n" +
            "   → プレハブが使用される（Prefab Mode の変更が反映）\n\n" +
            "【プロシージャルに戻す】\n" +
            "  MapAssembly.asset の対象スロットを None にクリアする",
            "OK");
    }

    // ─────────────────────────────────────────────────────────────────────
    // メニュー: テーマだけ作成
    // ─────────────────────────────────────────────────────────────────────
    [MenuItem("NIGHTMARE/Create Map Theme Asset", priority = 11)]
    public static MapTheme CreateMapThemeOnly()
    {
        var theme = EnsureMapTheme();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorGUIUtility.PingObject(theme);
        return theme;
    }

    // ─────────────────────────────────────────────────────────────────────
    // 既存テーマのリセット (色を初期値に戻す)
    // ─────────────────────────────────────────────────────────────────────
    [MenuItem("NIGHTMARE/Reset Map Theme to Default", priority = 12)]
    public static void ResetMapTheme()
    {
        if (!EditorUtility.DisplayDialog("MapTheme リセット",
            "MapTheme.asset の色設定をデフォルトに戻しますか？\n現在の設定は失われます。",
            "リセット", "キャンセル")) return;

        var theme = AssetDatabase.LoadAssetAtPath<MapTheme>(ThemePath);
        if (theme == null)
        {
            Debug.LogWarning("[NIGHTMARE] MapTheme.asset が見つかりません。先に Setup Map Prefabs を実行してください。");
            return;
        }

        // ScriptableObject を再作成して上書き
        var fresh = ScriptableObject.CreateInstance<MapTheme>();
        EditorUtility.CopySerialized(fresh, theme);
        Object.DestroyImmediate(fresh);
        EditorUtility.SetDirty(theme);
        AssetDatabase.SaveAssets();
        Debug.Log("[NIGHTMARE] MapTheme をデフォルトにリセットしました。");
    }

    // ─────────────────────────────────────────────────────────────────────
    // 内部処理
    // ─────────────────────────────────────────────────────────────────────
    private static MapTheme EnsureMapTheme()
    {
        // Assets/Data フォルダを作成
        if (!AssetDatabase.IsValidFolder("Assets/Data"))
            AssetDatabase.CreateFolder("Assets", "Data");

        var existing = AssetDatabase.LoadAssetAtPath<MapTheme>(ThemePath);
        if (existing != null) return existing;

        var theme = ScriptableObject.CreateInstance<MapTheme>();
        AssetDatabase.CreateAsset(theme, ThemePath);
        Debug.Log($"[NIGHTMARE] MapTheme 作成: {ThemePath}");
        return theme;
    }

    private static MapAssembly EnsureMapAssembly()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Data"))
            AssetDatabase.CreateFolder("Assets", "Data");

        var existing = AssetDatabase.LoadAssetAtPath<MapAssembly>(AssemblyPath);
        if (existing != null) return existing;

        var assembly = ScriptableObject.CreateInstance<MapAssembly>();
        AssetDatabase.CreateAsset(assembly, AssemblyPath);
        Debug.Log($"[NIGHTMARE] MapAssembly 作成: {AssemblyPath}");
        return assembly;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath)) return;

        // 親フォルダを再帰的に確認
        int lastSlash = folderPath.LastIndexOf('/');
        string parent = folderPath.Substring(0, lastSlash);
        string name   = folderPath.Substring(lastSlash + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static GameObject SaveSection(GameObject sectionGO, string sectionName)
    {
        string path = $"{SectionDir}/{sectionName}.prefab";

        // Prefab インスタンスの子は直接保存できないので一時コピーを作成
        var temp = Object.Instantiate(sectionGO);
        temp.name = sectionGO.name;

        bool success;
        var prefab = PrefabUtility.SaveAsPrefabAsset(temp, path, out success);
        Object.DestroyImmediate(temp);

        if (success)
            Debug.Log($"[NIGHTMARE] セクション保存: {path}");
        else
            Debug.LogError($"[NIGHTMARE] セクション保存失敗: {path}");

        return prefab;
    }

    private static FacilityMapUI WireThemeAndAssemblyToScene(MapTheme theme, MapAssembly assembly)
    {
        var mapUI = Object.FindObjectOfType<FacilityMapUI>();
        if (mapUI == null)
        {
            Debug.LogWarning("[NIGHTMARE] シーン内に FacilityMapUI が見つかりません。" +
                             "先に NIGHTMARE > Setup Full Scene を実行してください。");
            return null;
        }

        var so = new SerializedObject(mapUI);
        so.FindProperty("theme").objectReferenceValue    = theme;
        so.FindProperty("assembly").objectReferenceValue = assembly;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(mapUI);
        return mapUI;
    }

    private static void SaveMapPanelPrefab(GameObject mapPanelGO)
    {
        // Assets/Prefabs/Map フォルダを作成
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder(PrefabDir))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Map");

        bool success;
        PrefabUtility.SaveAsPrefabAssetAndConnect(mapPanelGO, PrefabPath,
            InteractionMode.AutomatedAction, out success);

        if (success)
            Debug.Log($"[NIGHTMARE] MapPanel プレハブ保存: {PrefabPath}");
        else
            Debug.LogError($"[NIGHTMARE] MapPanel プレハブの保存に失敗しました: {PrefabPath}");
    }
}
