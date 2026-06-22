using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Michsky.UI.Dark;

// メインメニューを管理する
// Dark UI (MainPanelManager) の Home/Settings/Story/Help パネルと連携し、
// ESC キーで前のパネルに戻る機能を提供する
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

    [Header("Dark UI Integration")]
    [SerializeField] private MainPanelManager darkPanelManager;

    [Header("Game Screen")]
    [SerializeField] private GameObject gameScreen;
    [SerializeField] private GameObject mainCanvas;

    [Header("Intro Fade")]
    [SerializeField] private Image fadeOverlay;

    [Header("Title")]
    [SerializeField] private Text titleText;  // メインメニューのタイトル文字列

    private TitleSceneDirector _titleDirector;

    // パネルスライドアニメーション幅 (px)
    private const float SLIDE_DIST    = 80f;
    private const float SLIDE_DUR     = 0.18f;

    // パネルの設計時 anchoredPosition を保持（スライド中断時に復元）
    private readonly System.Collections.Generic.Dictionary<GameObject, Vector2> _panelOrigPos
        = new System.Collections.Generic.Dictionary<GameObject, Vector2>();

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
        if (darkPanelManager == null)
        {
            // シーンに複数の MainPanelManager が存在するため、
            // panels[0].panelName == "Home" のルートマネージャを特定する
            foreach (var mgr in FindObjectsByType<MainPanelManager>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mgr.panels.Count > 0 && mgr.panels[0].panelName == "Home")
                {
                    darkPanelManager = mgr;
                    break;
                }
            }
        }
        ReparentSubPanelsIfNeeded();
        CachePanelPositions();
    }

    // SettingsPanel / LorePanel / StageSelectPanel が scale=0 の祖先に入っている場合、
    // TitleCanvas (mainCanvas) 直下に移動して可視にする
    private void ReparentSubPanelsIfNeeded()
    {
        if (mainCanvas == null) return;
        var canvasT = mainCanvas.transform;
        foreach (var p in new[] { stageSelectPanel, settingsPanel, lorePanel })
        {
            if (p == null || p.transform.parent == canvasT) continue;
            var t = p.transform.parent;
            while (t != null && t != canvasT)
            {
                if (t.localScale.sqrMagnitude < 0.01f)
                {
                    p.transform.SetParent(canvasT, false);
                    break;
                }
                t = t.parent;
            }
        }
    }

    private void CachePanelPositions()
    {
        _panelOrigPos.Clear();
        foreach (var p in new[] { mainMenuPanel, stageSelectPanel, settingsPanel, lorePanel })
        {
            if (p == null) continue;
            var rt = p.GetComponent<RectTransform>();
            if (rt != null) _panelOrigPos[p] = rt.anchoredPosition;
        }
    }

    private void RestorePanelPositions()
    {
        foreach (var kv in _panelOrigPos)
        {
            if (kv.Key == null) continue;
            var rt = kv.Key.GetComponent<RectTransform>();
            if (rt != null) rt.anchoredPosition = kv.Value;
        }
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

    private void Update()
    {
        // ESC キーでサブパネルからホームに戻る
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        Debug.Log($"[ESC] dark={(darkPanelManager != null ? $"idx={darkPanelManager.currentPanelIndex} go={darkPanelManager.gameObject.name}" : "NULL")}");

        // Dark UI (MainPanelManager) がアクティブな場合: Home 以外ならホームへ
        if (darkPanelManager != null && darkPanelManager.currentPanelIndex != 0)
        {
            darkPanelManager.OpenFirstTab();
            return;
        }

        // フォールバック: 独自サブパネルが開いている場合
        bool ss = stageSelectPanel != null && stageSelectPanel.activeSelf;
        bool sp = settingsPanel    != null && settingsPanel.activeSelf;
        bool lp = lorePanel        != null && lorePanel.activeSelf;
        if (ss || sp || lp) SlideToMainMenu();
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

    // ボタンにアクションを登録し、button_click SE を自動付与する
    private void Bind(Button btn, System.Action action)
    {
        if (btn == null) return;
        btn.onClick.AddListener(() => { AudioManager.Instance?.Play("button_click"); action(); });
    }

    private void SetupButtons()
    {
        Bind(btnStart,          ShowStageSelect);
        Bind(btnSettings,       () => SlideToPanel(settingsPanel));
        Bind(btnLore,           () => { if (loreText) loreText.text = LORE_TEXT; SlideToPanel(lorePanel); });
        btnQuit?.onClick.AddListener(() => Application.Quit());
        Bind(btnSettingsBack,   SlideToMainMenu);
        Bind(btnLoreBack,       SlideToMainMenu);
        Bind(btnStageSelectBack, SlideToMainMenu);

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
                Bind(btn, () => OnSelectDay(captured));
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
        // スライド中の場合も即座に中断・位置リセット
        if (_slideRoutine != null) { StopCoroutine(_slideRoutine); RestorePanelPositions(); _slideRoutine = null; }
        _titleDirector?.StopEffects();
        AudioManager.Instance?.StopLoop();
        // canvas 無効化よりも前に BGM をフェードアウト開始
        // （canvas 無効化後に StartCoroutine を呼ぶと非アクティブ状態でコルーチンが失敗するため）
        AudioManager.Instance?.FadeOutBGM(0.4f);
        yield return Fade(0f, 1f, 0.45f);
        HideSubPanels();
        mainCanvas?.SetActive(false); // TitleCanvas 全体を非表示（ゲーム画面へ移行）
        gameScreen?.SetActive(true);
        yield return Fade(1f, 0f, 0.45f);
        if (GameManager.Instance == null)
        {
            Debug.LogError("[MainMenuManager] GameManager.Instance が null です。シーンに GameManager を配置してください。");
            yield break;
        }
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
    private GameObject _currentPanel;

    private void ShowMainMenu()
    {
        gameScreen?.SetActive(false);
        // mainMenuPanel は TitleCanvas 全体を指しているため SetActive しない
        HideSubPanels();
    }

    private void SlideToMainMenu()
    {
        if (_slideRoutine != null) { StopCoroutine(_slideRoutine); RestorePanelPositions(); }
        // サブパネルのみスライドアウトし、ホーム（常時表示）に戻す
        GameObject from = null;
        foreach (var p in new[] { stageSelectPanel, settingsPanel, lorePanel })
            if (p != null && p.activeSelf) { from = p; break; }
        _slideRoutine = StartCoroutine(SlideTransition(from, null));
    }

    private void SlideToPanel(GameObject next)
    {
        if (next == null || (next.activeSelf && _slideRoutine == null)) return;
        if (_slideRoutine != null) { StopCoroutine(_slideRoutine); RestorePanelPositions(); }
        // sub-panel 間のみスライド（mainMenuPanel = TitleCanvas は対象外）
        GameObject current = null;
        foreach (var p in new[] { stageSelectPanel, settingsPanel, lorePanel })
            if (p != null && p.activeSelf) { current = p; break; }
        _slideRoutine = StartCoroutine(SlideTransition(current, next));
    }

    private IEnumerator SlideTransition(GameObject from, GameObject to)
    {
        // from をスライドアウト (右へ消える)
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

        // to をスライドイン (左から)。null のときはホームに戻るだけ
        if (to != null)
        {
            HideSubPanels();
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

    // mainMenuPanel(TitleCanvas) には触らず、サブパネルのみ隠す
    private void HideSubPanels()
    {
        stageSelectPanel?.SetActive(false);
        settingsPanel?.SetActive(false);
        lorePanel?.SetActive(false);
    }

    private void HideAllPanels()
    {
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
        if (mainCanvas == null)
            mainCanvas   = FindChildGO("MainCanvas");
    }

    private T FindChild<T>(string name) where T : Component
    {
        foreach (var c in FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c.gameObject.name == name) return c;
        return null;
    }

    private GameObject FindChildGO(string name) => FindChild<Transform>(name)?.gameObject;
}
