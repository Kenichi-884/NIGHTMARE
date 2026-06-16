using UnityEngine;
using UnityEngine.UI;
using System.Collections;

// メインメニューを管理する
// ゲーム開始前に表示され、「開始する」で GameManager.StartNight() を呼ぶ
public class MainMenuManager : MonoBehaviour
{
    public static MainMenuManager Instance { get; private set; }

    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject lorePanel;

    [Header("Main Menu Buttons")]
    [SerializeField] private Button btnStart;
    [SerializeField] private Button btnSettings;
    [SerializeField] private Button btnLore;
    [SerializeField] private Button btnQuit;

    [Header("Settings")]
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Button btnSettingsBack;

    [Header("Lore")]
    [SerializeField] private Text loreText;
    [SerializeField] private Button btnLoreBack;

    [Header("Intro Fade")]
    [SerializeField] private Image fadeOverlay;

    private const string LORE_TEXT =
        "<b>202X年 某所</b>\n\n" +
        "地下研究施設「アキラ-7」では、" +
        "非公開の生物実験が続けられていた。\n\n" +
        "ある夜、あなたは夜間管理人として施設に残っていた。\n" +
        "通常の点検作業のはずだった。\n\n" +
        "しかし22時を過ぎた頃、\n" +
        "外部監視カメラに異常な影が映り始めた。\n\n" +
        "電話は繋がらない。\n" +
        "ドアは手動でしか開かない。\n" +
        "あるのは監視モニターと、わずかな電力だけだ。\n\n" +
        "<color=#ff4444>夜明けまで生き延びろ。</color>\n\n" +
        "7日間。";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        SetupButtons();
        ShowMainMenu();
        StartCoroutine(FadeIn());

        // BGM を流す（メニュー用）
        AudioManager.Instance?.Play("menu_ambience");
    }

    private void SetupButtons()
    {
        btnStart?.onClick.AddListener(OnStartGame);
        btnSettings?.onClick.AddListener(() => SwitchPanel(settingsPanel));
        btnLore?.onClick.AddListener(() =>
        {
            if (loreText) loreText.text = LORE_TEXT;
            SwitchPanel(lorePanel);
        });
        btnQuit?.onClick.AddListener(() => Application.Quit());
        btnSettingsBack?.onClick.AddListener(ShowMainMenu);
        btnLoreBack?.onClick.AddListener(ShowMainMenu);

        bgmSlider?.onValueChanged.AddListener(v => AudioManager.Instance?.SetBGMVolume(v));
        sfxSlider?.onValueChanged.AddListener(v => AudioManager.Instance?.SetSFXVolume(v));

        if (bgmSlider) bgmSlider.value = 0.8f;
        if (sfxSlider) sfxSlider.value = 1.0f;
    }

    private void OnStartGame()
    {
        StartCoroutine(StartGameRoutine());
    }

    private IEnumerator StartGameRoutine()
    {
        // フェードアウト
        yield return Fade(1f, 0.5f);
        mainMenuPanel?.SetActive(false);
        settingsPanel?.SetActive(false);
        lorePanel?.SetActive(false);
        // フェードイン → ゲーム開始
        yield return Fade(0f, 0.5f);
        GameManager.Instance.StartNight();
    }

    private void ShowMainMenu()
    {
        mainMenuPanel?.SetActive(true);
        settingsPanel?.SetActive(false);
        lorePanel?.SetActive(false);
    }

    private void SwitchPanel(GameObject panel)
    {
        mainMenuPanel?.SetActive(false);
        settingsPanel?.SetActive(false);
        lorePanel?.SetActive(false);
        panel?.SetActive(true);
    }

    private IEnumerator FadeIn()
    {
        if (fadeOverlay == null) yield break;
        yield return Fade(0f, 0.8f);
    }

    private IEnumerator Fade(float targetAlpha, float duration)
    {
        if (fadeOverlay == null) yield break;
        float start = fadeOverlay.color.a;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            var c = fadeOverlay.color;
            c.a = Mathf.Lerp(start, targetAlpha, t / duration);
            fadeOverlay.color = c;
            yield return null;
        }
        var fc = fadeOverlay.color; fc.a = targetAlpha; fadeOverlay.color = fc;
    }

    // エディタセットアップから呼ぶ
    public void AutoFindChildren()
    {
        mainMenuPanel  = FindChildGO("MainMenuPanel");
        settingsPanel  = FindChildGO("SettingsPanel");
        lorePanel      = FindChildGO("LorePanel");
        btnStart       = FindChild<Button>("BtnStart");
        btnSettings    = FindChild<Button>("BtnSettings");
        btnLore        = FindChild<Button>("BtnLore");
        btnQuit        = FindChild<Button>("BtnQuit");
        bgmSlider      = FindChild<Slider>("BGMSlider");
        sfxSlider      = FindChild<Slider>("SFXSlider");
        btnSettingsBack = FindChild<Button>("BtnSettingsBack");
        loreText       = FindChild<Text>("LoreText");
        btnLoreBack    = FindChild<Button>("BtnLoreBack");
        fadeOverlay    = FindChild<Image>("MenuFadeOverlay");
    }

    private T FindChild<T>(string name) where T : Component
        => FindIn<T>(transform, name);

    private GameObject FindChildGO(string name)
        => FindIn<Transform>(transform, name)?.gameObject;

    private T FindIn<T>(Transform root, string name) where T : Component
    {
        foreach (Transform c in root)
        {
            if (c.name == name) { var r = c.GetComponent<T>(); if (r) return r; }
            var f = FindIn<T>(c, name);
            if (f) return f;
        }
        return null;
    }
}
