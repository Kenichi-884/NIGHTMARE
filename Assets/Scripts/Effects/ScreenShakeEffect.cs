using UnityEngine;
using System.Collections;

// ScreenSpaceOverlay Canvas ではトランスフォーム移動が効かないため
// スケールパルスで衝撃を表現する。複数周波数を合成してオーガニックな揺れにする。
public class ScreenShakeEffect : MonoBehaviour
{
    public static ScreenShakeEffect Instance { get; private set; }

    [SerializeField] private float pulseMagnitude = 0.025f; // スケール変化量 (1.0 → 1.025)
    [SerializeField] private int   pulseCount     = 4;

    private Vector3   originScale;
    private Coroutine shakeRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance    = this;
        originScale = transform.localScale;
    }

    // magnitude: スケール変化量 (デフォルト = pulseMagnitude)
    // ジャンプスケア時は 0.06〜0.10 程度を渡す
    public void Shake(float duration = 0.45f, float magnitude = -1f)
    {
        if (magnitude < 0f) magnitude = pulseMagnitude;
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(ShakeRoutine(duration, magnitude));
    }

    private IEnumerator ShakeRoutine(float dur, float mag)
    {
        float elapsed = 0f;
        // 3つの不協和周波数を合成してランダムに聞こえる揺れを作る
        float f1 = pulseCount * Mathf.PI / dur;        // 基本周波数
        float f2 = f1 * 2.73f;                         // 不協和倍音
        float f3 = f1 * 0.38f;                         // ゆっくりした揺り

        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float decay = 1f - (elapsed / dur);
            float shake = mag * decay * (
                Mathf.Sin(f1 * elapsed) * 0.55f +
                Mathf.Sin(f2 * elapsed) * 0.28f +
                Mathf.Sin(f3 * elapsed) * 0.17f
            );
            transform.localScale = originScale * (1f + shake);
            yield return null;
        }

        transform.localScale = originScale;
        shakeRoutine = null;
    }
}
