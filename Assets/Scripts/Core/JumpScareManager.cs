using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

// モンスターが管理人室に到達したとき、ジャンプスケアを演出する
// GameManager.OnGameOver → JumpScare → GameOverPanel の順に表示
public class JumpScareManager : MonoBehaviour
{
    public static JumpScareManager Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private Image flashOverlay;        // 全画面フラッシュ (白/赤)
    [SerializeField] private Image scareFaceImage;      // モンスターの顔
    [SerializeField] private Image blackOverlay;        // 暗転用

    [Header("Monster Face Sprites")]
    [SerializeField] private Sprite faceCrawler;
    [SerializeField] private Sprite faceRusher;
    [SerializeField] private Sprite faceJammer;
    [SerializeField] private Sprite faceLurker;
    [SerializeField] private Sprite faceMimic;
    [SerializeField] private Sprite faceKnocker;

    [Header("Timing")]
    [SerializeField] private float preStaticDuration = 0.20f;  // カメラ砂嵐の長さ
    [SerializeField] private float blackDuration     = 0.08f;
    [SerializeField] private float flashDuration     = 0.05f;
    [SerializeField] private float faceDuration      = 1.4f;
    [SerializeField] private float fadeOutDuration   = 0.6f;

    private bool triggered = false;

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

    private IEnumerator JumpScareRoutine()
    {
        // 0) 前触れ: 全カメラを最大グリッチ + 砂嵐音
        var camEffects = FindObjectsOfType<CameraViewEffect>();
        foreach (var e in camEffects) e.SetGlitchIntensity(1f);
        AudioManager.Instance?.Play("camera_static");
        yield return new WaitForSeconds(preStaticDuration);

        // 1) 赤フラッシュ（予兆）
        SetRGB(flashOverlay, new Color(0.9f, 0.05f, 0.05f));
        yield return Fade(flashOverlay, 0f, 0.65f, 0.06f);
        yield return Fade(flashOverlay, 0.65f, 0f, 0.05f);

        // 2) 暗転
        yield return Fade(blackOverlay, 0f, 1f, blackDuration);
        foreach (var e in camEffects) e.SetGlitchIntensity(0f);

        // 3) 顔セット
        var faceSprite = GetFaceSprite(GameManager.Instance.LastKillerType);
        if (faceSprite != null)
        {
            scareFaceImage.sprite        = faceSprite;
            scareFaceImage.preserveAspect = true;
        }
        SetAlpha(scareFaceImage, 0f);

        // 4) 第1フラッシュ: 白フラッシュ + 顔の出現
        SetRGB(flashOverlay, Color.white);
        yield return Fade(blackOverlay, 1f, 0f, flashDuration);
        yield return Fade(flashOverlay, 0f, 1f, flashDuration);
        SetAlpha(scareFaceImage, 1f);

        AudioManager.Instance?.Play("jumpscare_stinger");
        ScreenShakeEffect.Instance?.Shake(faceDuration + 0.3f, 0.07f);

        yield return Fade(flashOverlay, 1f, 0f, 0.10f);

        // 5) 第2フラッシュ (0.12s後)
        yield return new WaitForSeconds(0.12f);
        yield return Fade(flashOverlay, 0f, 0.55f, 0.03f);
        yield return Fade(flashOverlay, 0.55f, 0f, 0.07f);

        // 6) 第3フラッシュ (さらに0.12s後)
        yield return new WaitForSeconds(0.12f);
        yield return Fade(flashOverlay, 0f, 0.28f, 0.04f);
        yield return Fade(flashOverlay, 0.28f, 0f, 0.10f);

        // 7) 顔を表示し続ける（合計 faceDuration）
        float remainFace = faceDuration - 0.35f;
        yield return new WaitForSeconds(remainFace);

        // 8) 顔フェードアウト + 暗転
        float t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.deltaTime;
            float n = t / fadeOutDuration;
            SetAlpha(scareFaceImage, 1f - n);
            SetAlpha(blackOverlay,   n);
            yield return null;
        }
        SetAlpha(scareFaceImage, 0f);

        // 9) UIManager が GameOverPanel を表示するのを待つ
        yield return new WaitForSeconds(0.3f);
        yield return Fade(blackOverlay, 1f, 0f, 0.4f);
    }

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
        if (img == null) return;
        var c = img.color; c.a = a; img.color = c;
    }

    private static void SetRGB(Image img, Color rgb)
    {
        if (img == null) return;
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
