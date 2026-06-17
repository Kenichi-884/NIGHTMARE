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
    // BtnDay1 〜 BtnDay7 という名前のボタンを自動取得する
    private readonly Button[] dayButtons  = new Button[7];
    private readonly Text[]   dayBtnTexts = new Text[7];

    [Header("Settings")]
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Button btnSettingsBack;

    [Header("Lore")]
    [SerializeField] private Text loreText;
    [SerializeField] private Button btnLoreBack;

    [Header("Game Screen")]
    [SerializeField] private GameObject gameScreen;

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
        if (mainMenuPanel == null) AutoFindChildren();
    }

    private void Start()
    {
        SetupButtons();
        SetupDayButtons();
        ShowMainMenu();
        StartCoroutine(FadeIn());

        AudioManager.Instance?.Play("menu_ambience");
    }

    private void SetupButtons()
    {
        btnStart?.onClick.AddListener(ShowStageSelect);
        btnSettings?.onClick.AddListener(() => SwitchPanel(settingsPanel));
        btnLore?.onClick.AddListener(() =>
        {
            if (loreText) loreText.text = LORE_TEXT;
            SwitchPanel(lorePanel);
        });
        btnQuit?.onClick.AddListener(() => Application.Quit());
        btnSettingsBack?.onClick.AddListener(ShowMainMenu);
        btnLoreBack?.onClick.AddListener(ShowMainMenu);
        btnStageSelectBack?.onClick.AddListener(ShowMainMenu);

        bgmSlider?.onValueChanged.AddListener(v => AudioManager.Instance?.SetBGMVolume(v));
        sfxSlider?.onValueChanged.AddListener(v => AudioManager.Instance?.SetSFXVolume(v));

        if (bgmSlider) bgmSlider.value = 0.8f;
        if (sfxSlider) sfxSlider.value = 1.0f;
    }

    // BtnDay1 〜 BtnDay7 を名前で探し、クリック時に対応する日を開始する
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
        SwitchPanel(stageSelectPanel);
    }

    private void RefreshDayButtons()
    {
        // StageProgressManager が存在しない場合は PlayerPrefs を直接参照
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

    private void OnSelectDay(int day)
    {
        StartCoroutine(StartGameRoutine(day));
    }

    private IEnumerator StartGameRoutine(int day)
    {
        yield return Fade(1f, 0.5f);
        mainMenuPanel?.SetActive(false);
        stageSelectPanel?.SetActive(false);
        settingsPanel?.SetActive(false);
        lorePanel?.SetActive(false);
        gameScreen?.SetActive(true);
        yield return Fade(0f, 0.5f);
        GameManager.Instance.StartNight(day);
    }

    private static string WeatherLabel(WeatherType w) => w switch
    {
        WeatherType.Sunny => "晴れ",
        WeatherType.Rain  => "雨",
        WeatherType.Storm => "嵐",
        _ => ""
    };

    // ===== パネル切替 =====

    private void ShowMainMenu()
    {
        gameScreen?.SetActive(false);
        mainMenuPanel?.SetActive(true);
        stageSelectPanel?.SetActive(false);
        settingsPanel?.SetActive(false);
        lorePanel?.SetActive(false);
    }

    private void SwitchPanel(GameObject panel)
    {
        gameScreen?.SetActive(false);
        mainMenuPanel?.SetActive(false);
        stageSelectPanel?.SetActive(false);
        settingsPanel?.SetActive(false);
        lorePanel?.SetActive(false);
        panel?.SetActive(true);
    }

    // ===== フェード =====

    private IEnumerator FadeIn()
    {
        if (fadeOverlay == null) yield break;
        yield return Fade(0f, 0.8f);
    }

    private IEnumerator Fade(float targetAlpha, float duration)
    {
        if (fadeOverlay == null) yield break;
        fadeOverlay.raycastTarget = true;
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
        // 透明になったらraycastをオフ（ゲームUIのクリックを阻害しないよう）
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
