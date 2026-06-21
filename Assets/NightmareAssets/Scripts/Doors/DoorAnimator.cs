using UnityEngine;
using System.Collections;

/// <summary>
/// ドアアニメーションを管理する。
/// shutterMode=true: 上から降りるシャッター (閉じる時にバウンス+微振動)
/// shutterMode=false: openSlideOffset 方向へスライド
/// </summary>
public class DoorAnimator : MonoBehaviour
{
    [Header("設定")]
    [SerializeField] public DoorID    doorID;
    [SerializeField] public Transform doorPanel;
    [SerializeField] public float     animDuration = 0.45f;

    [Tooltip("true = 上から下に閉じるシャッター動作\nfalse = openSlideOffset 方向へスライド")]
    [SerializeField] public bool shutterMode = true;

    [Tooltip("shutterMode=false 時のみ: 開くときにパネルが動く量（ローカル座標）")]
    [SerializeField] public Vector3 openSlideOffset = new Vector3(0f, 3f, 0f);

    [Header("バウンス設定")]
    [Tooltip("閉じた後のオーバーシュート量 (0 = なし, 0.06 = 6%余分に入る)")]
    [SerializeField] private float bounceOvershoot = 0.06f;
    [Tooltip("バウンスの振動回数")]
    [SerializeField] private int   bounceCount = 2;

    private Vector3   _closedLocalPos;
    private Vector3   _closedLocalScale;
    private Coroutine _anim;

    private void Start()
    {
        if (doorPanel != null)
        {
            _closedLocalPos   = doorPanel.localPosition;
            _closedLocalScale = doorPanel.localScale;
        }

        if (DoorManager.Instance == null) return;
        DoorManager.Instance.OnDoorChanged += OnDoorChanged;
        bool closed = DoorManager.Instance.IsClosed(doorID);
        if (doorPanel != null) ApplyInstant(closed);
    }

    private void OnDestroy()
    {
        if (DoorManager.Instance != null)
            DoorManager.Instance.OnDoorChanged -= OnDoorChanged;
    }

    private void ApplyInstant(bool closed)
    {
        if (doorPanel == null) return;
        if (shutterMode)
        {
            doorPanel.localPosition = closed
                ? _closedLocalPos
                : _closedLocalPos + new Vector3(0, _closedLocalScale.y * 0.5f, 0);
            doorPanel.localScale = closed
                ? _closedLocalScale
                : new Vector3(_closedLocalScale.x, 0f, _closedLocalScale.z);
        }
        else
        {
            doorPanel.localPosition = closed
                ? _closedLocalPos
                : _closedLocalPos + openSlideOffset;
        }
    }

    private void OnDoorChanged(DoorID id, bool closing)
    {
        if (id != doorID) return;
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(Animate(closing));
    }

    private IEnumerator Animate(bool closing)
    {
        if (doorPanel == null) yield break;

        if (shutterMode)
        {
            yield return closing ? AnimateShutterClose() : AnimateShutterOpen();
        }
        else
        {
            yield return AnimateSlide(closing);
        }
        _anim = null;
    }

    // ── シャッター閉鎖: SmoothStep でメインアニメ → バウンス → 微振動 ───
    private IEnumerator AnimateShutterClose()
    {
        float h = _closedLocalScale.y;

        Vector3 fromPos   = doorPanel.localPosition;
        Vector3 fromScale = doorPanel.localScale;

        // フェーズ1: メインの閉鎖アニメ (animDuration の 90%)
        float mainDur = animDuration * 0.9f;
        float t = 0f;
        while (t < mainDur)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / mainDur));
            doorPanel.localPosition = Vector3.Lerp(fromPos,   _closedLocalPos,   p);
            doorPanel.localScale    = Vector3.Lerp(fromScale, _closedLocalScale, p);
            yield return null;
        }
        doorPanel.localPosition = _closedLocalPos;
        doorPanel.localScale    = _closedLocalScale;

        // フェーズ2: 衝撃バウンス (スケールをわずかに圧縮してから戻す)
        if (bounceOvershoot > 0f && bounceCount > 0)
        {
            float bounceDur = animDuration * 0.35f;
            float freq      = bounceCount * Mathf.PI / bounceDur;
            t = 0f;
            while (t < bounceDur)
            {
                t += Time.deltaTime;
                float decay     = 1f - (t / bounceDur);
                float scaleY    = _closedLocalScale.y * (1f - bounceOvershoot * Mathf.Sin(freq * t) * decay);
                doorPanel.localScale = new Vector3(
                    _closedLocalScale.x * (1f + bounceOvershoot * 0.3f * Mathf.Sin(freq * t) * decay),
                    scaleY,
                    _closedLocalScale.z
                );
                yield return null;
            }
            doorPanel.localScale = _closedLocalScale;
        }

        // フェーズ3: 微振動 (X軸の小刻み揺れ)
        float vibDur = 0.20f;
        t = 0f;
        while (t < vibDur)
        {
            t += Time.deltaTime;
            float decay  = 1f - (t / vibDur);
            float vibX   = Mathf.Sin(t * 80f) * 0.004f * _closedLocalScale.x * decay;
            doorPanel.localPosition = _closedLocalPos + new Vector3(vibX, 0f, 0f);
            yield return null;
        }
        doorPanel.localPosition = _closedLocalPos;
    }

    // ── シャッター開放: SmoothStep のみ ───────────────────────────────────
    private IEnumerator AnimateShutterOpen()
    {
        float h = _closedLocalScale.y;
        Vector3 fromPos   = doorPanel.localPosition;
        Vector3 fromScale = doorPanel.localScale;
        Vector3 targetPos   = _closedLocalPos + new Vector3(0, h * 0.5f, 0);
        Vector3 targetScale = new Vector3(_closedLocalScale.x, 0f, _closedLocalScale.z);

        float t = 0f;
        while (t < animDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / animDuration));
            doorPanel.localPosition = Vector3.Lerp(fromPos,   targetPos,   p);
            doorPanel.localScale    = Vector3.Lerp(fromScale, targetScale, p);
            yield return null;
        }
        doorPanel.localPosition = targetPos;
        doorPanel.localScale    = targetScale;
    }

    // ── スライドモード ─────────────────────────────────────────────────────
    private IEnumerator AnimateSlide(bool closing)
    {
        Vector3 from   = doorPanel.localPosition;
        Vector3 target = closing ? _closedLocalPos : _closedLocalPos + openSlideOffset;

        float t = 0f;
        while (t < animDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / animDuration));
            doorPanel.localPosition = Vector3.Lerp(from, target, p);
            yield return null;
        }
        doorPanel.localPosition = target;

        // 閉じた後に微振動
        if (closing)
        {
            float vibDur = 0.15f;
            t = 0f;
            while (t < vibDur)
            {
                t += Time.deltaTime;
                float decay = 1f - (t / vibDur);
                float vibX  = Mathf.Sin(t * 80f) * 0.005f * Mathf.Abs(openSlideOffset.magnitude) * decay;
                doorPanel.localPosition = target + new Vector3(vibX, vibX * 0.3f, 0f);
                yield return null;
            }
            doorPanel.localPosition = target;
        }
    }

    public void Setup(DoorID id, Transform panel, Vector3 slideOffset,
                      float duration = 0.45f, bool isShutter = true)
    {
        doorID          = id;
        doorPanel       = panel;
        openSlideOffset = slideOffset;
        animDuration    = duration;
        shutterMode     = isShutter;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (doorPanel == null) return;
        Gizmos.color = new Color(0.9f, 0.3f, 0.1f, 0.6f);
        Gizmos.DrawWireCube(doorPanel.position, doorPanel.lossyScale * 0.98f);

        if (shutterMode)
        {
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.8f);
            Gizmos.DrawLine(doorPanel.position, doorPanel.position + doorPanel.up * 1.5f);
            Gizmos.DrawWireSphere(doorPanel.position + doorPanel.up * 1.5f, 0.12f);
        }
        else
        {
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.8f);
            Vector3 worldOpen = doorPanel.TransformPoint(openSlideOffset);
            Gizmos.DrawLine(doorPanel.position, worldOpen);
            Gizmos.DrawWireSphere(worldOpen, 0.15f);
        }
    }
#endif
}
