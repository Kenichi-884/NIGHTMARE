using UnityEngine;

/// <summary>
/// 監視カメラの自動パン（左右往復）コンポーネント。
/// SceneCam_OUT_N 等の Camera GameObject に追加する。
///
/// NIGHTMARE > Camera Pan > Add to All Scene Cameras でシーン内の
/// 全 SceneCam_ オブジェクトにまとめて追加できる。
/// </summary>
[DisallowMultipleComponent]
public class SecurityCameraPan : MonoBehaviour
{
    [Header("水平パン")]
    [Tooltip("中心からの左右最大角度 (度)")]
    [SerializeField] private float panAngle  = 22f;

    [Tooltip("1往復にかかる秒数（大きいほど遅い）")]
    [SerializeField] private float panPeriod = 20f;

    [Header("微小ティルト（上下の揺れ感）")]
    [SerializeField] private float tiltAngle  = 0f;
    [SerializeField] private float tiltPeriod = 8.0f;

    [Header("位相オフセット（複数カメラがバラバラに動くように）")]
    [SerializeField, Range(0f, 1f)] private float phaseOffset = 0f;

    // ─────────────────────────────────────────────────────────────

    private Quaternion _baseRotation;
    private float      _t;

    /// <summary>SecurityCameraSystem から動的に追加されたときの初期値設定。</summary>
    public void SetDefaults(bool isInternal, float phase)
    {
        panAngle    = isInternal ? 14f : 22f;
        panPeriod   = isInternal ? 22f : UnityEngine.Random.Range(16f, 24f);
        tiltAngle   = 0f;
        phaseOffset = phase;
    }

    private void Start()
    {
        _baseRotation = transform.localRotation;
        // 位相オフセット分だけタイマーを進めておく
        _t = phaseOffset * panPeriod;
    }

    private void Update()
    {
        if (GameManager.Instance?.CurrentState != GameState.Night) return;

        _t += Time.deltaTime;

        float yaw   = Mathf.Sin((_t / panPeriod)  * Mathf.PI * 2f) * panAngle;
        float pitch = Mathf.Sin((_t / tiltPeriod) * Mathf.PI * 2f) * tiltAngle;

        transform.localRotation = _baseRotation * Quaternion.Euler(pitch, yaw, 0f);
    }

    // ─── エディタ用ユーティリティ ─────────────────────────────────

#if UNITY_EDITOR
    // パン範囲をシーンビューに弧で表示
    private void OnDrawGizmosSelected()
    {
        UnityEditor.Handles.color = new Color(1f, 0.9f, 0.3f, 0.55f);
        Vector3 leftDir  = Quaternion.Euler(0f, -panAngle, 0f) * transform.forward;
        UnityEditor.Handles.DrawWireArc(
            transform.position, transform.up, leftDir, panAngle * 2f, 3.5f);
    }

    /// <summary>シーン内の全 SceneCam_ に SecurityCameraPan を追加する。</summary>
    [UnityEditor.MenuItem("NIGHTMARE/Camera Pan/Add to All Scene Cameras")]
    static void Editor_AddPanToAll()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        int added  = 0;
        float[] phases =
        {
            0.00f, 0.25f, 0.50f, 0.75f,   // 外部 4 台
            0.13f, 0.38f, 0.63f, 0.88f    // 内部 4 台
        };
        int idx = 0;

        foreach (var root in scene.GetRootGameObjects())
            SearchAndAdd(root.transform, phases, ref idx, ref added);

        Debug.Log($"[CameraPan] {added} 台のカメラに SecurityCameraPan を追加しました。");
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
    }

    static void SearchAndAdd(Transform t, float[] phases, ref int idx, ref int added)
    {
        if (t.name.StartsWith("SceneCam_"))
        {
            if (t.GetComponent<SecurityCameraPan>() == null)
            {
                UnityEditor.Undo.AddComponent<SecurityCameraPan>(t.gameObject);
                var pan = t.GetComponent<SecurityCameraPan>();

                // 外部カメラは広め、内部は狭め
                bool isInternal = t.name.Contains("IN_");
                pan.panAngle  = isInternal ? 14f : 22f;
                pan.panPeriod = isInternal ? 22f : Random.Range(18f, 26f);
                pan.tiltAngle = 0f;

                // フェーズをカメラごとにずらす
                pan.phaseOffset = idx < phases.Length ? phases[idx] : Random.value;
                idx++;
                added++;
            }
        }
        foreach (Transform child in t)
            SearchAndAdd(child, phases, ref idx, ref added);
    }

    [UnityEditor.MenuItem("NIGHTMARE/Camera Pan/Remove from All Scene Cameras")]
    static void Editor_RemovePanFromAll()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        int removed = 0;
        foreach (var root in scene.GetRootGameObjects())
            RemoveRecursive(root.transform, ref removed);
        Debug.Log($"[CameraPan] {removed} 台から SecurityCameraPan を削除しました。");
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
    }

    static void RemoveRecursive(Transform t, ref int removed)
    {
        if (t.name.StartsWith("SceneCam_"))
        {
            var pan = t.GetComponent<SecurityCameraPan>();
            if (pan != null) { UnityEditor.Undo.DestroyObjectImmediate(pan); removed++; }
        }
        foreach (Transform child in t)
            RemoveRecursive(child, ref removed);
    }
#endif
}
