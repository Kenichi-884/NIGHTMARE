// NIGHTMARE > Map Collider Setup
// マップオブジェクトにコライダーを付与 & Y固定配置ツール
// 使い方:
//   1. NIGHTMARE > Add Map Colliders  → マップ全オブジェクトにMeshCollider追加
//   2. NIGHTMARE > Remove Map Colliders → 追加したコライダーを削除
//   3. プレハブ配置時: Ctrl+Shift+ドラッグ でサーフェスにスナップ配置
//   4. NIGHTMARE > Snap Selection to Floor → 選択オブジェクトをフロアY座標に固定

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public static class MapColliderSetup
{
    const string MapRootName  = "NightmareMap3D";
    const string ColliderTag  = "MapTempCollider"; // コンポーネントに付けるタグ代わりのラベル

    const float Floor1Y = 0.0f;
    const float FloorB1 = -4.0f;
    const float SnapThreshold = 2.5f; // この距離内なら最寄りフロアにスナップ

    // ─────────────────────────────────────────────────────────────
    //  コライダー追加 / 削除
    // ─────────────────────────────────────────────────────────────

    [MenuItem("NIGHTMARE/Map Colliders/Add Map Colliders")]
    static void AddMapColliders()
    {
        var root = FindMapRoot();
        if (root == null) return;

        int added = 0;
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var go = mf.gameObject;
            if (go.GetComponent<MeshCollider>() != null) continue;
            if (mf.sharedMesh == null) continue;

            var col = go.AddComponent<MeshCollider>();
            col.sharedMesh = mf.sharedMesh;
            added++;
        }

        Debug.Log($"[MapColliderSetup] MeshCollider を {added} 個追加しました。\nCtrl+Shift+ドラッグ でサーフェス上にプレハブをスナップ配置できます。");
        EditorUtility.SetDirty(root);
    }

    [MenuItem("NIGHTMARE/Map Colliders/Remove Map Colliders")]
    static void RemoveMapColliders()
    {
        var root = FindMapRoot();
        if (root == null) return;

        int removed = 0;
        foreach (var col in root.GetComponentsInChildren<MeshCollider>(true))
        {
            Object.DestroyImmediate(col);
            removed++;
        }

        Debug.Log($"[MapColliderSetup] MeshCollider を {removed} 個削除しました。");
        EditorUtility.SetDirty(root);
    }

    // ─────────────────────────────────────────────────────────────
    //  Y座標スナップ（選択オブジェクトをフロアに固定）
    // ─────────────────────────────────────────────────────────────

    [MenuItem("NIGHTMARE/Map Colliders/Snap Selection to Nearest Floor %#y")]
    static void SnapSelectionToFloor()
    {
        var selection = Selection.gameObjects;
        if (selection.Length == 0)
        {
            Debug.LogWarning("[MapColliderSetup] オブジェクトが選択されていません。");
            return;
        }

        Undo.RecordObjects(selection, "Snap to Floor");

        foreach (var go in selection)
        {
            float y = go.transform.position.y;
            float targetY = NearestFloorY(y);
            var pos = go.transform.position;
            pos.y = targetY;
            go.transform.position = pos;
        }

        Debug.Log($"[MapColliderSetup] {selection.Length} 個のオブジェクトをフロアY座標にスナップしました。");
    }

    [MenuItem("NIGHTMARE/Map Colliders/Snap Selection to 1F (Y=0) %#1")]
    static void SnapTo1F()
    {
        SnapToY(Floor1Y, "1F");
    }

    [MenuItem("NIGHTMARE/Map Colliders/Snap Selection to B1 (Y=-4) %#2")]
    static void SnapToB1()
    {
        SnapToY(FloorB1, "B1");
    }

    // ─────────────────────────────────────────────────────────────
    //  Box Collider 一括追加（軽量版・壁・床のみ）
    // ─────────────────────────────────────────────────────────────

    [MenuItem("NIGHTMARE/Map Colliders/Add Box Colliders (Lightweight)")]
    static void AddBoxColliders()
    {
        var root = FindMapRoot();
        if (root == null) return;

        int added = 0;
        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            var go = renderer.gameObject;
            if (go.GetComponent<Collider>() != null) continue;

            var bounds = renderer.bounds;
            var box = go.AddComponent<BoxCollider>();
            // ローカル座標でサイズ調整
            box.center = go.transform.InverseTransformPoint(bounds.center);
            box.size   = go.transform.InverseTransformVector(bounds.size);
            added++;
        }

        Debug.Log($"[MapColliderSetup] BoxCollider を {added} 個追加しました（軽量版）。");
        EditorUtility.SetDirty(root);
    }

    // ─────────────────────────────────────────────────────────────
    //  内部ユーティリティ
    // ─────────────────────────────────────────────────────────────

    static GameObject FindMapRoot()
    {
        var root = GameObject.Find(MapRootName);
        if (root == null)
        {
            Debug.LogError($"[MapColliderSetup] '{MapRootName}' がシーンに見つかりません。\nNIGHTMARE > Build Temp 3D Map でマップを生成してください。");
        }
        return root;
    }

    static float NearestFloorY(float y)
    {
        float d1 = Mathf.Abs(y - Floor1Y);
        float dB1 = Mathf.Abs(y - FloorB1);
        return d1 <= dB1 ? Floor1Y : FloorB1;
    }

    static void SnapToY(float targetY, string label)
    {
        var selection = Selection.gameObjects;
        if (selection.Length == 0)
        {
            Debug.LogWarning("[MapColliderSetup] オブジェクトが選択されていません。");
            return;
        }

        Undo.RecordObjects(selection, $"Snap to {label}");

        foreach (var go in selection)
        {
            var pos = go.transform.position;
            pos.y = targetY;
            go.transform.position = pos;
        }

        Debug.Log($"[MapColliderSetup] {selection.Length} 個を {label} (Y={targetY}) にスナップしました。");
    }
}
