// NIGHTMARE > Add Audio & Visual Systems
// 既存シーンを壊さずに、新しいオーディオ・ビジュアルシステムを追加するセットアップ。
// AudioManager / Canvas / カメラ設定はそのまま維持します。

using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class NightmareAVSetup
{
    [MenuItem("NIGHTMARE/Add Audio & Visual Systems", priority = 10)]
    public static void Run()
    {
        if (!EditorUtility.DisplayDialog("NIGHTMARE AV Setup",
            "新しいオーディオ・ビジュアルシステムを既存シーンに追加します。\n" +
            "既存の設定・マップ・AudioManager は維持されます。\n\n" +
            "追加されるもの:\n" +
            "  ・AudioPoolManager（多重SFX）\n" +
            "  ・SpatialAudioController（3D音声）\n" +
            "  ・AtmosphericAudioController（環境音）\n" +
            "  ・DynamicMixerController（動的ミキシング）\n" +
            "  ・PostProcessChainUI（色収差・グレイン等）\n" +
            "  ・PlayerAudioListener（3D音声用リスナー）",
            "追加する", "キャンセル")) return;

        int added = 0;

        added += SetupAudioSystems();
        added += SetupPostProcess();
        added += SetupAudioListener();

        MarkSceneDirty();
        EditorUtility.DisplayDialog("完了",
            $"{added} 個のコンポーネントを追加しました。\n\n" +
            "【次のステップ】\n" +
            "1. AudioPoolManager / SpatialAudioController / AtmosphericAudioController の\n" +
            "   Inspector でオーディオクリップをアサインしてください。\n\n" +
            "2. PostProcessChainUI の Inspector で\n" +
            "   Lens Target に HUD の RectTransform を接続してください。\n\n" +
            "3. 3D音声を使う場合は PlayerListener の位置を\n" +
            "   管理人室のワールド座標（0, -4, -10）付近に合わせてください。\n\n" +
            "Ctrl+S で保存してください。",
            "OK");
    }

    // ─────────────────────────────────────────────────────────────────
    // Audio Systems（Managers / AudioSystems に追加）
    // ─────────────────────────────────────────────────────────────────
    static int SetupAudioSystems()
    {
        int added = 0;

        // AudioSystems 親オブジェクトを探すか新規作成
        var audioSysGO = GameObject.Find("AudioSystems");
        if (audioSysGO == null)
        {
            // [NIGHTMARE Root] / Managers に入れる
            var managers = GameObject.Find("Managers");
            var root     = GameObject.Find("[NIGHTMARE Root]");
            var parent   = managers != null ? managers
                         : root     != null ? root
                         : null;

            audioSysGO = new GameObject("AudioSystems");
            if (parent != null)
                audioSysGO.transform.SetParent(parent.transform, false);
        }

        added += AddIfMissing<AudioPoolManager>(audioSysGO);
        added += AddIfMissing<SpatialAudioController>(audioSysGO);
        added += AddIfMissing<AtmosphericAudioController>(audioSysGO);
        added += AddIfMissing<DynamicMixerController>(audioSysGO);

        Debug.Log($"[NIGHTMARE AV] AudioSystems を '{audioSysGO.transform.parent?.name ?? "Root"}' 配下に設定しました。");
        return added;
    }

    // ─────────────────────────────────────────────────────────────────
    // PostProcessChainUI（Canvas の最前面に追加）
    // ─────────────────────────────────────────────────────────────────
    static int SetupPostProcess()
    {
        // 既に存在する場合はスキップ
        if (Object.FindObjectOfType<PostProcessChainUI>() != null)
        {
            Debug.Log("[NIGHTMARE AV] PostProcessChainUI は既に存在します。スキップしました。");
            return 0;
        }

        // Canvas を探す（名前 "Canvas" か、Screen Space Overlay Canvas を優先）
        Canvas targetCanvas = null;
        foreach (var c in Object.FindObjectsOfType<Canvas>())
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay)
            { targetCanvas = c; break; }
        }
        if (targetCanvas == null)
        {
            Debug.LogWarning("[NIGHTMARE AV] Screen Space Overlay Canvas が見つかりません。PostProcessChainUI の追加をスキップしました。");
            return 0;
        }

        // Canvas の一番最後の子として追加（最前面に描画）
        var ppGO = new GameObject("PostProcessChainUI", typeof(RectTransform));
        ppGO.transform.SetParent(targetCanvas.transform, false);
        ppGO.transform.SetAsLastSibling(); // 最前面

        var rt = ppGO.GetComponent<RectTransform>();
        rt.anchorMin = rt.offsetMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMax = Vector2.zero;

        ppGO.AddComponent<PostProcessChainUI>();

        // lensTarget に Canvas 自身の RectTransform を仮接続
        // （後で HUD の RectTransform に変更推奨）
        var so = new SerializedObject(ppGO.GetComponent<PostProcessChainUI>());
        var lp = so.FindProperty("lensTarget");
        if (lp != null)
        {
            // HUD を探して自動接続を試みる
            var hud = targetCanvas.transform.Find("GameScreen/HUD");
            if (hud == null) hud = GameObject.Find("HUD")?.transform;
            lp.objectReferenceValue = hud != null
                ? hud.GetComponent<RectTransform>()
                : targetCanvas.GetComponent<RectTransform>();
            so.ApplyModifiedProperties();
        }

        Debug.Log($"[NIGHTMARE AV] PostProcessChainUI を Canvas '{targetCanvas.name}' の最前面に追加しました。");
        return 1;
    }

    // ─────────────────────────────────────────────────────────────────
    // PlayerAudioListener（3D音声の基準点）
    // ─────────────────────────────────────────────────────────────────
    static int SetupAudioListener()
    {
        // 既に PlayerListener がいればスキップ
        if (GameObject.Find("PlayerListener") != null)
        {
            Debug.Log("[NIGHTMARE AV] PlayerListener は既に存在します。スキップしました。");
            return 0;
        }

        // Main Camera の AudioListener を無効化して専用リスナーを作成する
        var mainCam = Camera.main;
        if (mainCam != null)
        {
            var camListener = mainCam.GetComponent<AudioListener>();
            if (camListener != null)
            {
                camListener.enabled = false;
                Debug.Log("[NIGHTMARE AV] Main Camera の AudioListener を無効化しました。");
            }
        }

        // 管理人室のワールド座標に配置（NightmareMapBuilder 準拠）
        var listenerGO = new GameObject("PlayerListener");
        listenerGO.transform.position = new Vector3(0f, -4f, -10f);
        listenerGO.AddComponent<AudioListener>();

        // [NIGHTMARE Root] 配下に入れる
        var root = GameObject.Find("[NIGHTMARE Root]");
        if (root != null) listenerGO.transform.SetParent(root.transform, true);

        Debug.Log("[NIGHTMARE AV] PlayerListener を (0, -4, -10) に配置しました。" +
                  "3D マップを使用している場合は管理人室の実座標に合わせてください。");
        return 1;
    }

    // ─────────────────────────────────────────────────────────────────
    // ユーティリティ
    // ─────────────────────────────────────────────────────────────────

    /// <summary>T が既になければ追加して 1、既にあれば 0 を返す。</summary>
    static int AddIfMissing<T>(GameObject go) where T : Component
    {
        if (go.GetComponent<T>() != null)
        {
            Debug.Log($"[NIGHTMARE AV] {typeof(T).Name} は既に存在します。スキップしました。");
            return 0;
        }
        go.AddComponent<T>();
        Debug.Log($"[NIGHTMARE AV] {typeof(T).Name} を追加しました。");
        return 1;
    }

    static void MarkSceneDirty()
    {
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }
}
