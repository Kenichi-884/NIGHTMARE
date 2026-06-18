using UnityEngine;
using UnityEngine.UI;
using System.Collections;

// メインメニューを管理する
// 「開始する」でステージ選択パネルを開き、解放済みの日を選んで GameManager.StartNight(day) を呼ぶ
// ステージ解放状況は StageProgressManager (PlayerPrefs) で管理する
public class MainMenuManager : MonoBehaviour
{
    public static MainMenuManager Instance { get; private set; }

    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject stageSelectPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject lorePanel;

    [Header("Main Menu Buttons")]
    [SerializeField] private Button btnStart;
    [SerializeField] private Button btnSettings;
    [SerializeField] private Button btnLore;
    [SerializeField] private Button btnQuit;

    [Header("Stage Select")]
    [SerializeField] private Button btnStageSelectBack;
    private readonly Button[] dayButtons  = new Button[7];
    private readonly Text[]   dayBtnTexts = new Text[7];

    [Header("Settings")]
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Button btnSettingsBack;

    [Header("Lore")]
    [SerializeField] private Text   loreText;
    [SerializeField] private Button btnLoreBack;

    [Header("Game Screen")]
    [SerializeField] private GameObject gameScreen;

    [Header("Intro Fade")]
    [SerializeField] private Image fadeOverlay;

    [Header("Title")]
    [SerializeField] private Text titleText;  // メインメニューのタイトル文字列

    private TitleSceneDirector _titleDirector;

    // パネルスライドアニメーション幅 (px)
    private const float SLIDE_DIST    = 80f;
    private const float SLIDE_DUR     = 0.18f;

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
        if (mainMenuPanel == null) AutoFindChildren();
        _titleDirector = GetComponent<TitleSceneDirector>();
    }

    private void Start()
    {
        SetupButtons();
        SetupDayButtons();
        ShowMainMenu();

        // 起動演出
        StartCoroutine(IntroSequence());

        AudioManager.Instance?.PlayTitleBGM();
        AudioManager.Instance?.Play("menu_ambience");
    }

    // ── 起動演出: フェードイン + タイトルフリッカー ─────────────────────
    private IEnumerator IntroSequence()
    {
        yield return Fade(1f, 0f, 0.9f);

        // タイトルテキストが存在すれば CRT 点灯フリッカー
        if (titleText != null) yield return TitleFlicker();
    }

    private IEnumerator TitleFlicker()
    {
        Color orig = titleText.color;
        float[] alphas = { 0f, 0.9f, 0.2f, 1f, 0.5f, 0f, 1f };
        float[] waits  = { 0.04f, 0.06f, 0.04f, 0.10f, 0.05f, 0.06f, 0f };

        for (int i = 0; i < alphas.Length; i++)
        {
            var c = orig; c.a = alphas[i]; titleText.color = c;
            yield return new WaitForSeconds(waits[i]);
        }
        titleText.color = orig;
    }

    private void SetupButtons()
    {
        btnStart?.onClick.AddListener(() => { AudioManager.Instance?.Play("button_click"); ShowStageSelect(); });
        btnSettings?.onClick.AddListener(() => { AudioManager.Instance?.Play("button_click"); SlideToPanel(settingsPanel); });
        btnLore?.onClick.AddListener(() =>
        {
            AudioManager.Instance?.Play("button_click");
            if (loreText) loreText.text = LORE_TEXT;
            SlideToPanel(lorePanel);
        });
        btnQuit?.onClick.AddListener(() => Application.Quit());
        btnSettingsBack?.onClick.AddListener(() => { AudioManager.Instance?.Play("button_click"); SlideToMainMenu(); });
        btnLoreBack?.onClick.AddListener(()     => { AudioManager.Instance?.Play("button_click"); SlideToMainMenu(); });
        btnStageSelectBack?.onClick.AddListener(() => { AudioManager.Instance?.Play("button_click"); SlideToMainMenu(); });

        bgmSlider?.onValueChanged.AddListener(v => AudioManager.Instance?.SetBGMVolume(v));
        sfxSlider?.onValueChanged.AddListener(v => AudioManager.Instance?.SetSFXVolume(v));

        if (bgmSlider) bgmSlider.value = 0.8f;
        if (sfxSlider) sfxSlider.value = 1.0f;
    }

    // BtnDay1 〜 BtnDay7 を名前で探してクリック時に StartNight を呼ぶ
    private void SetupDayButtons()
    {
        for (int i = 0; i < 7; i++)
        {
            int day = i + 1;
            var btn = FindChild<Button>($"BtnDay{day}");
            dayButtons[i]  = btn;
            dayBtnTexts[i] = btn != null ? btn.GetComponentInChildren<Text>() : null;
            if (btn != null)
            {
                int captured = day;
                btn.onClick.AddListener(() => OnSelectDay(captured));
            }
        }
    }

    // ===== ステージ選択 =====

    private void ShowStageSelect()
    {
        RefreshDayButtons();
        SlideToPanel(stageSelectPanel);
    }

    private void RefreshDayButtons()
    {
        int maxUnlocked = StageProgressManager.Instance != null
            ? StageProgressManager.Instance.MaxUnlockedDay
            : PlayerPrefs.GetInt("MaxUnlockedDay", 1);

        for (int i = 0; i < 7; i++)
        {
            int day = i + 1;
            if (dayButtons[i] == null) continue;
            bool unlocked = day <= maxUnlocked;
            dayButtons[i].interactable = unlocked;
            if (dayBtnTexts[i] != null)
            {
                string weatherStr = WeatherLabel(WeatherManager.GetWeather(day));
                dayBtnTexts[i].text = unlocked
                    ? $"Day {day}  {weatherStr}"
                    : $"Day {day}  [未解放]";
            }
        }
    }

    private void OnSelectDay(int day) => StartCoroutine(StartGameRoutine(day));

    private IEnumerator StartGameRoutine(int day)
    {
        _titleDirector?.StopEffects();
        AudioManager.Instance?.StopLoop();
        yield return Fade(0f, 1f, 0.45f);
        HideAllPanels();
        gameScreen?.SetActive(true);
        yield return Fade(1f, 0f, 0.45f);
        GameManager.Instance.StartNight(day);
    }

    private static string WeatherLabel(WeatherType w) => w switch
    {
        WeatherType.Sunny => "晴れ",
        WeatherType.Rain  => "雨",
        WeatherType.Storm => "嵐",
        _ => ""
    };

    // ===== パネル切替 (スライドアニメーション付き) =====

    private Coroutine _slideRoutine;

    private void ShowMainMenu()
    {
        gameScreen?.SetActive(false);
        mainMenuPanel?.SetActive(true);
        stageSelectPanel?.SetActive(false);
        settingsPanel?.SetActive(false);
        lorePanel?.SetActive(false);
    }

    private void SlideToMainMenu()
    {
        if (_slideRoutine != null) StopCoroutine(_slideRoutine);
        _slideRoutine = StartCoroutine(SlideTransition(null, mainMenuPanel));
    }

    private void SlideToPanel(GameObject next)
    {
        if (_slideRoutine != null) StopCoroutine(_slideRoutine);
        // 現在表示中のパネルを探す
        GameObject current = null;
        foreach (var p in new[] { mainMenuPanel, stageSelectPanel, settingsPanel, lorePanel })
        {
            if (p != null && p.activeSelf) { current = p; break; }
        }
        _slideRoutine = StartCoroutine(SlideTransition(current, next));
    }

    private IEnumerator SlideTransition(GameObject from, GameObject to)
    {
        // from をスライドアウト (右→見えなくなる)
        if (from != null)
        {
            var rt = from.GetComponent<RectTransform>();
            if (rt != null)
            {
                Vector2 origin = rt.anchoredPosition;
                float t = 0f;
                while (t < SLIDE_DUR)
                {
                    t += Time.deltaTime;
                    float p = Mathf.SmoothStep(0f, 1f, t / SLIDE_DUR);
                    rt.anchoredPosition = origin + new Vector2(SLIDE_DIST * p, 0f);
                    yield return null;
                }
                rt.anchoredPosition = origin;
            }
            from.SetActive(false);
        }

        // to をスライドイン (左から右へ)
        if (to != null)
        {
            HideAllPanels();
            to.SetActive(true);
            var rt = to.GetComponent<RectTransform>();
            if (rt != null)
            {
                Vector2 origin = rt.anchoredPosition;
                rt.anchoredPosition = origin + new Vector2(-SLIDE_DIST, 0f);
                float t = 0f;
                while (t < SLIDE_DUR)
                {
                    t += Time.deltaTime;
                    float p = Mathf.SmoothStep(0f, 1f, t / SLIDE_DUR);
                    rt.anchoredPosition = Vector2.Lerp(origin + new Vector2(-SLIDE_DIST, 0f), origin, p);
                    yield return null;
                }
                rt.anchoredPosition = origin;
            }
        }

        _slideRoutine = null;
    }

    private void HideAllPanels()
    {
        gameScreen?.SetActive(false);
        mainMenuPanel?.SetActive(false);
        stageSelectPanel?.SetActive(false);
        settingsPanel?.SetActive(false);
        lorePanel?.SetActive(false);
    }

    // ===== フェード =====

    private IEnumerator Fade(float fromAlpha, float targetAlpha, float duration)
    {
        if (fadeOverlay == null) yield break;
        fadeOverlay.raycastTarget = true;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            var c = fadeOverlay.color;
            c.a = Mathf.Lerp(fromAlpha, targetAlpha, t / duration);
            fadeOverlay.color = c;
            yield return null;
        }
        var fc = fadeOverlay.color; fc.a = targetAlpha; fadeOverlay.color = fc;
        if (targetAlpha == 0f) fadeOverlay.raycastTarget = false;
    }

    // ===== エディタセットアップから呼ぶ =====
    public void AutoFindChildren()
    {
        gameScreen       = FindChildGO("GameScreen");
        mainMenuPanel    = FindChildGO("MainMenuPanel");
        stageSelectPanel = FindChildGO("StageSelectPanel");
        settingsPanel    = FindChildGO("SettingsPanel");
        lorePanel        = FindChildGO("LorePanel");
        btnStart         = FindChild<Button>("BtnStart");
        btnSettings      = FindChild<Button>("BtnSettings");
        btnLore          = FindChild<Button>("BtnLore");
        btnQuit          = FindChild<Button>("BtnQuit");
        btnStageSelectBack = FindChild<Button>("BtnStageSelectBack");
        bgmSlider        = FindChild<Slider>("BGMSlider");
        sfxSlider        = FindChild<Slider>("SFXSlider");
        btnSettingsBack  = FindChild<Button>("BtnSettingsBack");
        loreText         = FindChild<Text>("LoreText");
        btnLoreBack      = FindChild<Button>("BtnLoreBack");
        fadeOverlay      = FindChild<Image>("MenuFadeOverlay");
        titleText        = FindChild<Text>("TitleText");
    }

    private T FindChild<T>(string name) where T : Component => FindIn<T>(transform, name);

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
