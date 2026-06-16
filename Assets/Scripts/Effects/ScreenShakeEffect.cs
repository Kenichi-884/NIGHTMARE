using UnityEngine;
using System.Collections;

// ScreenSpaceOverlay Canvas ではトランスフォーム移動が効かないため
// Canvas のスケールを素早くパルスさせることでインパクト演出を行う
// JumpScareManager からジャンプスケア時に呼ぶ
public class ScreenShakeEffect : MonoBehaviour
{
    public static ScreenShakeEffect Instance { get; private set; }

    [SerializeField] private float pulseMagnitude = 0.025f; // スケール変化量 (例: 1.0 → 1.025)
    [SerializeField] private int   pulseCount     = 4;       // 往復回数

    private Vector3 originScale;
    private Coroutine shakeRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        originScale = transform.localScale;
    }

    // duration: トータル時間, magnitude: スケールの最大変化量
    public void Shake(float duration = 0.45f, float magnitude = -1f)
    {
        if (magnitude < 0f) magnitude = pulseMagnitude;
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(ShakeRoutine(duration, magnitude));
    }

    private IEnumerator ShakeRoutine(float dur, float mag)
    {
        float elapsed = 0f;
        float freq = pulseCount * Mathf.PI / dur; // sine 周波数

        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float decay = 1f - (elapsed / dur);
            float scale = 1f + mag * Mathf.Sin(freq * elapsed) * decay;
            transform.localScale = originScale * scale;
            yield return null;
        }

        transform.localScale = originScale;
        shakeRoutine = null;
    }
}
