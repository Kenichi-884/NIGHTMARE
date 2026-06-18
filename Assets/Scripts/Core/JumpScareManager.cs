using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// ジャンプスケア演出マネージャー（大幅強化版）。
///
/// ■ 演出シーケンス
///   0. 予兆フェーズ（プレスケア）
///      - 全カメラ最大グリッチ + カメラ砂嵐音
///      - 画面全体に色収差が走り始める
///      - ハートビートが急加速（BPM 増加）
///   1. 赤フラッシュ（予兆）
///      - 赤い薄フラッシュでプレイヤーを驚かせる「予告」
///   2. 暗転
///      - 急速暗転でモンスター顔のセット
///   3. 衝撃フラッシュ＋顔出現
///      - 白フラッシュと同時に顔が小さい状態から急拡大
///      - 最大スケールを超えてから縮む（弾性感）
///      - ジャンプスケア音、スクリーンシェイク、レンズ歪み、色収差が最大
///   4. 複数フラッシュ（余韻）
///      - 2〜3 回の追加フラッシュで神経を逆なで
///   5. 顔持続＋徐々に暗転
///      - 顔が徐々に暗転でフェードアウト
///   6. ゲームオーバーパネル表示
/// </summary>
public class JumpScareManager : MonoBehaviour
{
    public static JumpScareManager Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private Image flashOverlay;
    [SerializeField] private Image scareFaceImage;
    [SerializeField] private Image blackOverlay;

    [Header("Monster Face Sprites")]
    [SerializeField] private Sprite faceCrawler;
    [SerializeField] private Sprite faceRusher;
    [SerializeField] private Sprite faceJammer;
    [SerializeField] private Sprite faceLurker;
    [SerializeField] private Sprite faceMimic;
    [SerializeField] private Sprite faceKnocker;

    [Header("Timing")]
    [SerializeField] private float preStaticDuration = 0.25f;
    [SerializeField] private float blackDuration     = 0.07f;
    [SerializeField] private float flashDuration     = 0.04f;
    [SerializeField] private float faceDuration      = 1.5f;
    [SerializeField] private float fadeOutDuration   = 0.7f;

    [Header("Face Animation")]
    [SerializeField] private float faceScaleIn      = 0.55f;   // 出現時の初期スケール
    [SerializeField] private float faceScaleOverMax = 1.18f;   // バウンス最大スケール
    [SerializeField] private float faceScaleDuration = 0.12f;  // スケールアニメ時間

    // ─── ランタイム ────────────────────────────────────────────────
    private bool      triggered = false;
    private Coroutine heartbeatAccelRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (flashOverlay == null) AutoFindChildren();
    }

    private void Start()
    {
        SetAlpha(flashOverlay,   0f);
        SetAlpha(scareFaceImage, 0f);
        SetAlpha(blackOverlay,   0f);
        if (scareFaceImage) scareFaceImage.transform.localScale = Vector3.one;

        GameManager.Instance.OnGameOver   += OnGameOver;
        GameManager.Instance.OnDayStarted += _ => triggered = false;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance) GameManager.Instance.OnGameOver -= OnGameOver;
    }

    private void OnGameOver()
    {
        if (triggered) return;
        triggered = true;
        StartCoroutine(JumpScareRoutine());
    }

    // ─── メインシーケンス ──────────────────────────────────────────

    private IEnumerator JumpScareRoutine()
    {
        var camEffects = FindObjectsOfType<CameraViewEffect>();

        // ── 0. 予兆：グリッチ加速 + 色収差 ─────────────────────
        foreach (var e in camEffects) e.SetGlitchIntensity(1f);
        AudioManager.Instance?.Play("camera_static");

        // ハートビート急加速コルーチン開始
        if (heartbeatAccelRoutine != null) StopCoroutine(heartbeatAccelRoutine);
        heartbeatAccelRoutine = StartCoroutine(HeartbeatAccelRoutine());

        // 色収差をゆっくり蓄積
        PostProcessChainUI.Instance?.SetChromaticAberration(0.3f);

        yield return new WaitForSeconds(preStaticDuration * 0.5f);

        // 静電気音2回目
        AudioManager.Instance?.Play("camera_static");
        PostProcessChainUI.Instance?.SetChromaticAberration(0.7f);

        yield return new WaitForSeconds(preStaticDuration * 0.5f);

        // ── 1. 赤フラッシュ（予兆）──────────────────────────────
        SetRGB(flashOverlay, new Color(0.85f, 0.02f, 0.02f));
        yield return Fade(flashOverlay, 0f, 0.60f, 0.05f);
        yield return Fade(flashOverlay, 0.60f, 0f, 0.06f);

        yield return new WaitForSeconds(0.05f);

        // ── 2. 暗転 ──────────────────────────────────────────────
        yield return Fade(blackOverlay, 0f, 1f, blackDuration);
        foreach (var e in camEffects) e.SetGlitchIntensity(0f);

        // ── 3. 顔をセット（非表示状態）──────────────────────────
        var faceSprite = GetFaceSprite(GameManager.Instance.LastKillerType);
        if (faceSprite != null)
        {
            scareFaceImage.sprite         = faceSprite;
            scareFaceImage.preserveAspect = true;
        }
        SetAlpha(scareFaceImage, 0f);
        if (scareFaceImage) scareFaceImage.transform.localScale = Vector3.one * faceScaleIn;

        // ── 4. 衝撃フラッシュ＋顔の出現 ────────────────────────
        SetRGB(flashOverlay, Color.white);
        yield return Fade(blackOverlay, 1f, 0f, flashDuration);
        yield return Fade(flashOverlay, 0f, 1f, flashDuration);
        SetAlpha(scareFaceImage, 1f);

        // ジャンプスケア音・シェイク・レンズ歪み・色収差 最大
        AudioManager.Instance?.Play("jumpscare_stinger");
        ScreenShakeEffect.Instance?.Shake(faceDuration + 0.4f, 0.09f);
        PostProcessChainUI.Instance?.PulseLens(faceDuration + 0.4f, 0.075f);
        PostProcessChainUI.Instance?.SetChromaticAberration(1f);

        // 顔スケールアニメ（小→大→バウンス→安定）
        if (scareFaceImage) StartCoroutine(FaceScaleAnimation());

        yield return Fade(flashOverlay, 1f, 0f, 0.12f);

        // 色収差フェードアウト（1.5秒かけて）
        PostProcessChainUI.Instance?.FadeOutChroma(1.5f);

        // ── 5. 追加フラッシュ1（0.14s後）────────────────────────
        yield return new WaitForSeconds(0.14f);
        yield return Fade(flashOverlay, 0f, 0.50f, 0.03f);
        yield return Fade(flashOverlay, 0.50f, 0f, 0.08f);

        // ── 6. 追加フラッシュ2（0.14s後）────────────────────────
        yield return new WaitForSeconds(0.14f);
        yield return Fade(flashOverlay, 0f, 0.30f, 0.04f);
        yield return Fade(flashOverlay, 0.30f, 0f, 0.10f);

        // ── 7. 追加フラッシュ3（微小、0.2s後）───────────────────
        yield return new WaitForSeconds(0.20f);
        yield return Fade(flashOverlay, 0f, 0.14f, 0.04f);
        yield return Fade(flashOverlay, 0.14f, 0f, 0.12f);

        // ── 8. 顔を持続表示 ──────────────────────────────────────
        float remainFace = faceDuration - 0.50f;
        if (remainFace > 0f) yield return new WaitForSeconds(remainFace);

        // ── 9. 顔フェードアウト + 暗転 ──────────────────────────
        float t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.deltaTime;
            float n = t / fadeOutDuration;
            SetAlpha(scareFaceImage, 1f - n);
            SetAlpha(blackOverlay,   n * 0.9f);
            yield return null;
        }
        SetAlpha(scareFaceImage, 0f);

        // ── 10. ゲームオーバーパネル待機 ─────────────────────────
        yield return new WaitForSeconds(0.3f);
        yield return Fade(blackOverlay, 0.9f, 0f, 0.5f);
    }

    // ── 顔スケールアニメーション（弾性バウンス）─────────────────

    private IEnumerator FaceScaleAnimation()
    {
        if (!scareFaceImage) yield break;
        var rt = scareFaceImage.transform;

        // フェーズ1: faceScaleIn → faceScaleOverMax（勢いよく拡大）
        float t = 0f, dur1 = faceScaleDuration;
        while (t < dur1)
        {
            t += Time.unscaledDeltaTime;
            float s = Mathf.Lerp(faceScaleIn, faceScaleOverMax, t / dur1);
            rt.localScale = Vector3.one * s;
            yield return null;
        }

        // フェーズ2: faceScaleOverMax → 1.0（弾性で戻る）
        t = 0f;
        float dur2 = faceScaleDuration * 1.4f;
        while (t < dur2)
        {
            t += Time.unscaledDeltaTime;
            // Ease Out Bounce っぽく
            float n = t / dur2;
            float s = Mathf.Lerp(faceScaleOverMax, 1.0f,
                1f - Mathf.Pow(1f - n, 3f)); // Ease Out Cubic
            rt.localScale = Vector3.one * s;
            yield return null;
        }

        rt.localScale = Vector3.one;
    }

    // ── ハートビート急加速（プレスケア演出）──────────────────────

    private IEnumerator HeartbeatAccelRoutine()
    {
        float interval = 0.8f;
        float minInterval = 0.18f;
        float elapsed = 0f;
        float totalDur = preStaticDuration;

        while (elapsed < totalDur)
        {
            AudioManager.Instance?.Play("heartbeat");
            yield return new WaitForSeconds(interval);
            elapsed += interval;
            interval = Mathf.Lerp(interval, minInterval, elapsed / totalDur);
        }
    }

    // ─── ユーティリティ ────────────────────────────────────────────

    private Sprite GetFaceSprite(MonsterType? type)
    {
        if (type == null) return faceCrawler;
        return type switch
        {
            MonsterType.Crawler => faceCrawler,
            MonsterType.Rusher  => faceRusher,
            MonsterType.Jammer  => faceJammer,
            MonsterType.Lurker  => faceLurker,
            MonsterType.Mimic   => faceMimic,
            MonsterType.Knocker => faceKnocker,
            _                   => faceCrawler
        };
    }

    public void AutoFindChildren()
    {
        flashOverlay   = FindDeep<Image>("JumpScareFlash");
        scareFaceImage = FindDeep<Image>("JumpScareFace");
        blackOverlay   = FindDeep<Image>("JumpScareBlack");
    }

    public void SetFaceSprites(Sprite crawler, Sprite rusher, Sprite jammer,
                               Sprite lurker,  Sprite mimic,  Sprite knocker)
    {
        faceCrawler = crawler; faceRusher  = rusher;
        faceJammer  = jammer;  faceLurker  = lurker;
        faceMimic   = mimic;   faceKnocker = knocker;
    }

    private IEnumerator Fade(Image img, float from, float to, float dur)
    {
        if (img == null) yield break;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            SetAlpha(img, Mathf.Lerp(from, to, t / dur));
            yield return null;
        }
        SetAlpha(img, to);
    }

    private static void SetAlpha(Image img, float a)
    {
        if (!img) return;
        var c = img.color; c.a = a; img.color = c;
    }

    private static void SetRGB(Image img, Color rgb)
    {
        if (!img) return;
        var c = img.color; c.r = rgb.r; c.g = rgb.g; c.b = rgb.b; img.color = c;
    }

    private T FindDeep<T>(string name) where T : Component => FindIn<T>(transform, name);

    private T FindIn<T>(Transform root, string name) where T : Component
    {
        foreach (Transform c in root)
        {
            if (c.name == name) { var r = c.GetComponent<T>(); if (r) return r; }
            var found = FindIn<T>(c, name);
            if (found) return found;
        }
        return null;
    }
}
