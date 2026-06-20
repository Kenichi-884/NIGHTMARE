using UnityEngine;
using UnityEngine.UI;

public class TVModeController : MonoBehaviour
{
    [Header("有効化スイッチ")]
    [Tooltip("ONにしないと何も変換しない。TVのTransformをセットし終わったら有効にする。")]
    [SerializeField] private bool enableTVMode = false;

    [Header("Canvas参照")]
    [Tooltip("HUD要素が入ったCanvas（TimeText, Power, ドア, カメラボタン等）")]
    [SerializeField] private Canvas hudCanvas;

    [Tooltip("プレイヤーが見るメインカメラ")]
    [SerializeField] private Camera mainCamera;

    [Header("TV画面の配置")]
    [Tooltip("TVモニターのスクリーン面のTransform（中心・法線方向がforwardになるよう配置）")]
    [SerializeField] private Transform tvScreenTransform;

    [Tooltip("TVスクリーンの横幅（m単位、実際のメッシュサイズに合わせる）")]
    [SerializeField] private float tvWidth  = 1.4f;

    [Tooltip("スクリーン面から手前にずらす量（重なり防止）")]
    [SerializeField] private float zOffset = 0.005f;

    [Header("Canvas基準解像度")]
    [Tooltip("HUD CanvasのReference Resolution幅（Canvas Scalerの設定値に合わせる）")]
    [SerializeField] private float referenceWidth  = 1920f;
    [SerializeField] private float referenceHeight = 1080f;

    void Start()
    {
        if (!enableTVMode) return;

        if (hudCanvas == null || tvScreenTransform == null)
        {
            Debug.LogWarning("[TVModeController] enableTVMode=ON ですが Canvas または TV Transform が未設定です。");
            return;
        }

        ApplyWorldSpaceMode();
    }

    void ApplyWorldSpaceMode()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        hudCanvas.renderMode  = RenderMode.WorldSpace;
        hudCanvas.worldCamera = mainCamera;

        var scaler = hudCanvas.GetComponent<CanvasScaler>();
        if (scaler != null) scaler.enabled = false;

        hudCanvas.GetComponent<RectTransform>().sizeDelta = new Vector2(referenceWidth, referenceHeight);

        hudCanvas.transform.position = tvScreenTransform.position + tvScreenTransform.forward * zOffset;
        hudCanvas.transform.rotation = tvScreenTransform.rotation;

        float scale = tvWidth / referenceWidth;
        hudCanvas.transform.localScale = Vector3.one * scale;

        Debug.Log($"[TVModeController] World Space 配置完了 scale={scale:F5} pos={hudCanvas.transform.position}");
    }

#if UNITY_EDITOR
    [ContextMenu("Apply TV Mode Now")]
    void Editor_Apply() => ApplyWorldSpaceMode();

    [ContextMenu("★ Revert Canvas to Screen Space - Overlay")]
    void Editor_Revert()
    {
        if (hudCanvas == null) return;
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.transform.localScale = Vector3.one;

        var scaler = hudCanvas.GetComponent<CanvasScaler>();
        if (scaler != null) scaler.enabled = true;

        Debug.Log("[TVModeController] Screen Space - Overlay に戻しました。");
    }
#endif
}
