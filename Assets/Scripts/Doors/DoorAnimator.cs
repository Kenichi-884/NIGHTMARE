using UnityEngine;
using System.Collections;

/// <summary>
/// ドアパネルのスライドアニメーションを管理する。
/// DoorManager.OnDoorChanged を受けて開閉を演出する。
/// </summary>
public class DoorAnimator : MonoBehaviour
{
    [Header("設定")]
    [SerializeField] public DoorID    doorID;
    [SerializeField] public Transform doorPanel;        // アニメーションするパネル
    [SerializeField] public float     animDuration = 0.45f;

    [Tooltip("開くときにパネルが動く量（ローカル座標）")]
    [SerializeField] public Vector3 openSlideOffset = new Vector3(0f, 3f, 0f);

    // 閉じた状態のローカル座標（= シーン配置時の初期位置）
    private Vector3   _closedLocalPos;
    private Coroutine _anim;

    // ───────────────────────────────────
    private void Start()
    {
        if (doorPanel != null)
            _closedLocalPos = doorPanel.localPosition;

        if (DoorManager.Instance == null) return;

        DoorManager.Instance.OnDoorChanged += OnDoorChanged;

        // 初期状態に即座に合わせる（全ドアはゲーム開始時に開放）
        bool closed = DoorManager.Instance.IsClosed(doorID);
        if (doorPanel != null)
            doorPanel.localPosition = closed ? _closedLocalPos
                                              : _closedLocalPos + openSlideOffset;
    }

    private void OnDestroy()
    {
        if (DoorManager.Instance != null)
            DoorManager.Instance.OnDoorChanged -= OnDoorChanged;
    }

    // ───────────────────────────────────
    private void OnDoorChanged(DoorID id, bool closing)
    {
        if (id != doorID) return;
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(Animate(closing));
    }

    private IEnumerator Animate(bool closing)
    {
        if (doorPanel == null) yield break;

        Vector3 from   = doorPanel.localPosition;
        Vector3 target = closing ? _closedLocalPos : _closedLocalPos + openSlideOffset;

        // SE（クリップ未割り当て時は無音で続行）
        AudioManager.Instance?.Play(closing ? "door_close" : "door_open");

        float t = 0f;
        while (t < animDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / animDuration));
            doorPanel.localPosition = Vector3.Lerp(from, target, p);
            yield return null;
        }

        doorPanel.localPosition = target;
        _anim = null;
    }

    // ───────────────────────────────────
    /// <summary>MapBuilder からエディタ時に呼ぶセットアップ。</summary>
    public void Setup(DoorID id, Transform panel, Vector3 slideOffset, float duration = 0.45f)
    {
        doorID          = id;
        doorPanel       = panel;
        openSlideOffset = slideOffset;
        animDuration    = duration;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (doorPanel == null) return;
        Gizmos.color = new Color(0.9f, 0.3f, 0.1f, 0.6f);
        Gizmos.DrawWireCube(doorPanel.position, doorPanel.lossyScale * 0.98f);

        // 開方向の矢印
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.8f);
        Vector3 worldOpen = doorPanel.TransformPoint(openSlideOffset);
        Gizmos.DrawLine(doorPanel.position, worldOpen);
        Gizmos.DrawWireSphere(worldOpen, 0.15f);
    }
#endif
}
