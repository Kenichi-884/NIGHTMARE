using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Displays")]
    [SerializeField] private Text timeText;
    [SerializeField] private Text dayText;
    [SerializeField] private Slider powerSlider;
    [SerializeField] private Text powerText;
    [SerializeField] private Image powerFill;

    [Header("Door UI")]
    [SerializeField] private Button btnGate;
    [SerializeField] private Button btnEntrance;
    [SerializeField] private Button btnBasement;
    [SerializeField] private Button btnB1;
    [SerializeField] private Button btnEmergency;
    [SerializeField] private Button btnEliminate;
    [SerializeField] private Button btnResetCam;

    [SerializeField] private Image indGate;
    [SerializeField] private Image indEntrance;
    [SerializeField] private Image indBasement;
    [SerializeField] private Image indB1;

    [Header("Camera Buttons - External")]
    [SerializeField] private Button btnCamOutN;
    [SerializeField] private Button btnCamOutE;
    [SerializeField] private Button btnCamOutW;
    [SerializeField] private Button btnCamOutTop;

    [Header("Camera Buttons - Internal")]
    [SerializeField] private Button btnCamIn1FA;
    [SerializeField] private Button btnCamIn1FB;
    [SerializeField] private Button btnCamInB1A;
    [SerializeField] private Button btnCamInB1B;

    [Header("Panels")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject clearPanel;
    [SerializeField] private GameObject dayTransitionPanel;
    [SerializeField] private Text dayTransitionText;
    [SerializeField] private Button nextNightButton;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button backToMenuButton; // GameOverPanel
    [SerializeField] private Button titleButton;      // ClearPanel (真エンディング後)
    [SerializeField] private Text killerLabel;

    [Header("Phenomena UI")]
    [SerializeField] private Text phenomenaWarningText;
    [SerializeField] private Image ghostSignalOverlay;

    [Header("Weather / Power")]
    [SerializeField] private Text weatherStatusText;
    [SerializeField] private Text weatherTimerText;
    [SerializeField] private Text powerWarnText;

    [Header("Map")]
    [SerializeField] private GameObject mapPanel;
    [SerializeField] private Button btnToggleMap;

    [Header("Pause")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private Button btnResume;
    [SerializeField] private Button btnPauseMenu;

    private readonly Color colorOpen   = new Color(0.2f, 0.9f, 0.2f);
    private readonly Color colorClosed = new Color(0.9f, 0.2f, 0.2f);
    private readonly Color colorGreen  = new Color(0.2f, 0.9f, 0.2f);
    private readonly Color colorRed    = new Color(0.9f, 0.2f, 0.2f);
    private readonly Color colorYellow = new Color(0.9f, 0.8f, 0.1f);

    private bool powerPulseActive = false;
    private Coroutine powerPulseRoutine;
    private GamePhase lastPhase;
    private bool _isPaused = false;

    // Mimic 時刻ズレ演出
    private bool wasMimicActive = false;
    private float mimicFakeHourOffset = 0f;

    // キャッシュ — 変化時のみテキストを更新してGCと Canvas dirty を抑制
    private int _lastTimeH = -1, _lastTimeM = -1;
    private string _lastFakeTimeStr = "";
    private bool _dayTextDirty = true;
    private WeatherType _lastWeather = (WeatherType)(-1);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (timeText == null || pausePanel == null) AutoFindChildren();
    }

    private void Start()
    {
        BindDoorButtons();
        BindCameraButtons();

        GameManager.Instance.OnGameOver      += OnGameOver;
        GameManager.Instance.OnNightCleared  += OnNightCleared;
        GameManager.Instance.OnTrueEnding    += OnTrueEnding;
        GameManager.Instance.OnDayStarted    += OnDayStarted;
        GameManager.Instance.OnPhaseChanged  += OnPhaseChanged;

        DoorManager.Instance.OnDoorChanged    += OnDoorChangedAnim;
        PowerManager.Instance.OnPowerChanged  += OnPowerChangedUI;
        PowerManager.Instance.OnPowerOut      += OnPowerOut;
        PowerManager.Instance.OnPowerRestored += OnPowerRestored;

        gameOverPanel?.SetActive(false);
        clearPanel?.SetActive(false);
        dayTransitionPanel?.SetActive(false);
        mapPanel?.SetActive(false);
        if (phenomenaWarningText) phenomenaWarningText.gameObject.SetActive(false);
        if (ghostSignalOverlay)  ghostSignalOverlay.gameObject.SetActive(false);

        Bind(retryButton,      () => GameManager.Instance.RetryCurrentDay());
        Bind(backToMenuButton, ReturnToMenu);
        Bind(titleButton,      ReturnToMenu);
        Bind(btnToggleMap,     ToggleMap);

        pausePanel?.SetActive(false);
        Bind(btnResume,    Resume);
        Bind(btnPauseMenu, ReturnToMenu);
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;

        // ─── キーボードショートカット ─────────────────────────────
        if (Input.GetKeyDown(KeyCode.M))
            ToggleMap();

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            bool nightActive = GameManager.Instance.CurrentState == GameState.Night;
            bool panelOpen   = (gameOverPanel != null && gameOverPanel.activeSelf)
                            || (clearPanel    != null && clearPanel.activeSelf)
                            || (dayTransitionPanel != null && dayTransitionPanel.activeSelf);
            if (nightActive && !panelOpen)
                TogglePause();
        }

        // ─── 時刻表示（ゲーム内分が変わったときだけ更新）─────────
        if (timeText)
        {
            bool mimicActive = SecurityCameraSystem.Instance != null && (
                SecurityCameraSystem.Instance.IsCameraMimicked(SecurityCameraSystem.Instance.ActiveExternal) ||
                SecurityCameraSystem.Instance.IsCameraMimicked(SecurityCameraSystem.Instance.ActiveInternal));
            if (mimicActive && !wasMimicActive)
                mimicFakeHourOffset = Random.Range(-2f, -0.5f);
            wasMimicActive = mimicActive;

            if (mimicActive)
            {
                string fake = GetFakeDisplayTime();
                if (fake != _lastFakeTimeStr) { _lastFakeTimeStr = fake; timeText.text = fake; }
            }
            else
            {
                float gth = GameManager.Instance.GameTimeInHours;
                float displayHour = gth >= 24f ? gth - 24f : gth;
                int h = (int)displayHour;
                int m = (int)(GameManager.Instance.PhaseProgress * 60f);
                if (h != _lastTimeH || m != _lastTimeM)
                {
                    _lastTimeH = h; _lastTimeM = m;
                    timeText.text = $"{h:D2}:{m:D2}";
                }
            }
        }

        // ─── Day/Phase/Weather テキスト（変化時のみ）─────────────
        if (dayText)
        {
            // 天候変化もここで検出（WeatherManager にイベントがないためポーリング）
            var curWeather = WeatherManager.Instance != null
                ? WeatherManager.Instance.CurrentWeather : _lastWeather;
            if (curWeather != _lastWeather) { _lastWeather = curWeather; _dayTextDirty = true; }

            if (_dayTextDirty)
            {
                _dayTextDirty = false;
                string wStr = WeatherManager.Instance != null
                    ? $"  [{WeatherJP(curWeather)}]" : "";
                dayText.text = $"Day {GameManager.Instance.CurrentDay}  ―  {PhaseJP(GameManager.Instance.CurrentPhase)}{wStr}";

                // 天候表示も同タイミングで更新
                RefreshWeatherUI(curWeather);
            }
        }
        // dayText が null でも天候変化だけは検出する
        else if (WeatherManager.Instance != null)
        {
            var curWeather = WeatherManager.Instance.CurrentWeather;
            if (curWeather != _lastWeather) { _lastWeather = curWeather; RefreshWeatherUI(curWeather); }
        }
    }

    // 天候UIをまとめて更新（Update内で毎フレーム呼ぶのではなく変化時のみ）
    private void RefreshWeatherUI(WeatherType weather)
    {
        if (weatherStatusText)
        {
            switch (weather)
            {
                case WeatherType.Sunny:
                    weatherStatusText.text  = "  晴";
                    weatherStatusText.color = new Color(0.8f, 0.9f, 1f);
                    break;
                case WeatherType.Rain:
                    weatherStatusText.text  = "  雨";
                    weatherStatusText.color = new Color(0.4f, 0.7f, 1f);
                    break;
                case WeatherType.Storm:
                    weatherStatusText.text  = "  嵐";
                    weatherStatusText.color = new Color(1f, 0.4f, 0.1f);
                    break;
            }
        }
        if (weatherTimerText)
        {
            weatherTimerText.text = weather == WeatherType.Sunny ? ""
                : weather == WeatherType.Rain  ? "敵+15%速  現象+30%"
                :                                "敵+30%速  現象+60%";
        }
    }

    // 電力UIはOnPowerChangedイベントで更新（Update内ポーリング廃止）
    private void OnPowerChangedUI(float power)
    {
        if (PowerManager.Instance == null) return;
        float pct = PowerManager.Instance.PowerPercent;
        if (powerSlider) powerSlider.value = pct;
        if (powerText) powerText.text = $"{(int)(pct * 100)}%";
        UpdatePowerColor(pct);
        HandlePowerPulse(pct);

        if (powerWarnText)
        {
            if (PowerManager.Instance.IsPowerOut)
            {
                powerWarnText.text  = "!! 停電中 !!";
                powerWarnText.color = new Color(1f, 0.2f, 0.1f);
            }
            else if (pct <= 0.1f)
            {
                powerWarnText.text  = "電力危機";
                powerWarnText.color = new Color(1f, 0.3f, 0.1f);
            }
            else if (pct <= 0.25f)
            {
                powerWarnText.text  = "電力低下";
                powerWarnText.color = new Color(1f, 0.75f, 0.1f);
            }
            else
            {
                powerWarnText.text = "";
            }
        }
    }

    // ===== 電力表示 =====
    private void UpdatePowerColor(float pct)
    {
        if (!powerFill) return;
        if (!powerPulseActive)
            powerFill.color = pct > 0.5f ? colorGreen : pct > 0.25f ? colorYellow : colorRed;
    }

    private void HandlePowerPulse(float pct)
    {
        bool shouldPulse = pct <= 0.25f && !PowerManager.Instance.IsPowerOut;
        if (shouldPulse && !powerPulseActive)
        {
            powerPulseActive = true;
            if (powerPulseRoutine != null) StopCoroutine(powerPulseRoutine);
            powerPulseRoutine = StartCoroutine(PowerPulseRoutine());
        }
        else if (!shouldPulse && powerPulseActive)
        {
            powerPulseActive = false;
            if (powerPulseRoutine != null) StopCoroutine(powerPulseRoutine);
            if (powerFill) powerFill.color = colorYellow;
        }
    }

    private IEnumerator PowerPulseRoutine()
    {
        while (powerPulseActive)
        {
            float t = (Mathf.Sin(Time.time * 4f) + 1f) * 0.5f;
            if (powerFill)
                powerFill.color = Color.Lerp(
                    new Color(0.7f, 0.05f, 0.05f),
                    new Color(1.0f, 0.25f, 0.10f),
                    t);
            yield return null;
        }
    }

    // ===== ユーティリティ =====

    // ボタンにアクションを登録し、button_click SE を自動付与する
    private void Bind(Button btn, System.Action action)
    {
        if (btn == null) return;
        btn.onClick.AddListener(() => { AudioManager.Instance?.Play("button_click"); action(); });
    }

    // ===== ドア =====
    private void BindDoorButtons()
    {
        Bind(btnGate,      () => DoorManager.Instance.Toggle(DoorID.Gate));
        Bind(btnEntrance,  () => DoorManager.Instance.Toggle(DoorID.Entrance));
        Bind(btnBasement,  () => DoorManager.Instance.Toggle(DoorID.BasementStairs));
        Bind(btnB1,        () => DoorManager.Instance.Toggle(DoorID.B1Corridor));
        Bind(btnEmergency, () => DoorManager.Instance.EmergencyLockdown());
        Bind(btnEliminate, () => JammerAI.TryEliminateVisible());
        Bind(btnResetCam,  () => SecurityCameraSystem.Instance.TryResetCamera(SecurityCameraSystem.Instance.ActiveCamera));
    }

    private void OnDoorChangedAnim(DoorID id, bool closed)
    {
        Image ind = id switch
        {
            DoorID.Gate           => indGate,
            DoorID.Entrance       => indEntrance,
            DoorID.BasementStairs => indBasement,
            DoorID.B1Corridor     => indB1,
            _ => null
        };
        if (ind) StartCoroutine(FlashIndicator(ind, closed));
    }

    private IEnumerator FlashIndicator(Image ind, bool closed)
    {
        Color target = closed ? colorClosed : colorOpen;
        Color flash  = Color.white;

        // 閉めた瞬間に白フラッシュ、開いた瞬間にターゲット色へ
        ind.color = flash;
        yield return new WaitForSeconds(0.05f);
        float t = 0f;
        while (t < 0.2f)
        {
            t += Time.deltaTime;
            ind.color = Color.Lerp(flash, target, t / 0.2f);
            yield return null;
        }
        ind.color = target;
    }

    // ===== カメラ =====
    private void BindCameraButtons()
    {
        Bind(btnCamOutN,   () => SecurityCameraSystem.Instance.SwitchExternal(CameraID.OUT_N));
        Bind(btnCamOutE,   () => SecurityCameraSystem.Instance.SwitchExternal(CameraID.OUT_E));
        Bind(btnCamOutW,   () => SecurityCameraSystem.Instance.SwitchExternal(CameraID.OUT_W));
        Bind(btnCamOutTop, () => SecurityCameraSystem.Instance.SwitchExternal(CameraID.OUT_TOP));

        Bind(btnCamIn1FA, () => SecurityCameraSystem.Instance.SwitchInternal(CameraID.IN_1F_A));
        Bind(btnCamIn1FB, () => SecurityCameraSystem.Instance.SwitchInternal(CameraID.IN_1F_B));
        Bind(btnCamInB1A, () => SecurityCameraSystem.Instance.SwitchInternal(CameraID.IN_B1_A));
        Bind(btnCamInB1B, () => SecurityCameraSystem.Instance.SwitchInternal(CameraID.IN_B1_B));
    }

    // ===== フェーズ変化 =====
    private void OnPhaseChanged(GamePhase phase)
    {
        if (phase == lastPhase) return;
        lastPhase = phase;
        _dayTextDirty = true;
        StartCoroutine(PhaseFlash());
    }

    private IEnumerator PhaseFlash()
    {
        if (!dayText) yield break;
        Color original = dayText.color;
        Color highlight = new Color(1f, 0.85f, 0.3f);
        float t = 0f;
        while (t < 0.4f)
        {
            t += Time.deltaTime;
            dayText.color = Color.Lerp(highlight, original, t / 0.4f);
            yield return null;
        }
        dayText.color = original;
    }

    // ===== 異常現象UI =====
    public void ShowPhenomenaWarning(string message)
    {
        if (phenomenaWarningText) StartCoroutine(WarningRoutine(message, 3f));
    }

    private IEnumerator WarningRoutine(string msg, float dur)
    {
        phenomenaWarningText.text = msg;
        phenomenaWarningText.gameObject.SetActive(true);

        // フェードイン
        var t_in = 0f;
        var c = phenomenaWarningText.color;
        while (t_in < 0.25f)
        {
            t_in += Time.deltaTime;
            c.a = Mathf.Lerp(0f, 1f, t_in / 0.25f);
            phenomenaWarningText.color = c;
            yield return null;
        }
        c.a = 1f; phenomenaWarningText.color = c;

        yield return new WaitForSeconds(dur - 0.5f);

        // フェードアウト
        float t_out = 0f;
        while (t_out < 0.25f)
        {
            t_out += Time.deltaTime;
            c.a = Mathf.Lerp(1f, 0f, t_out / 0.25f);
            phenomenaWarningText.color = c;
            yield return null;
        }
        phenomenaWarningText.gameObject.SetActive(false);
    }

    public void ShowGhostSignal(float duration)
    {
        if (ghostSignalOverlay) StartCoroutine(GhostRoutine(duration));
    }

    private IEnumerator GhostRoutine(float dur)
    {
        ghostSignalOverlay.gameObject.SetActive(true);
        yield return new WaitForSeconds(dur);
        ghostSignalOverlay.gameObject.SetActive(false);
    }

    // ===== 停電 =====
    private void OnPowerOut()
    {
        ShowPhenomenaWarning("停電！予備電源を起動中...");
        AudioManager.Instance?.Play("power_out");
        powerPulseActive = false;
    }

    private void OnPowerRestored()
    {
        ShowPhenomenaWarning("予備電源 ON  (電力 10%)");
        AudioManager.Instance?.Play("power_restore");
    }

    // ===== ゲームオーバー =====
    private void OnGameOver()
    {
        Resume(); // ポーズ中でもゲームオーバーは処理する
        powerPulseActive = false;
        if (killerLabel)
        {
            var killer = GameManager.Instance.LastKillerType;
            killerLabel.text = killer.HasValue
                ? $"侵入者: {MonsterNameJP(killer.Value)}"
                : "原因不明";
        }
        ShowPanel(gameOverPanel);
    }

    // ===== 日付遷移 =====
    private void OnDayStarted(int day)
    {
        Resume();
        dayTransitionPanel?.SetActive(false);
        gameOverPanel?.SetActive(false);
        clearPanel?.SetActive(false);
        powerPulseActive = false;
        _dayTextDirty = true;
        _lastTimeH = -1; _lastTimeM = -1; // 時刻表示を強制リフレッシュ
    }

    private void OnNightCleared(int day)
    {
        if (day >= 7) return;
        dayTransitionPanel?.SetActive(true);
        if (dayTransitionText)
        {
            string nextWeather = WeatherJP(WeatherManager.GetWeather(day + 1));
            dayTransitionText.text =
                $"<b>Day {day}  クリア</b>\n\n" +
                $"{DayBriefing(day + 1)}\n\n" +
                $"<size=16>明日の天候: {nextWeather}</size>\n\n" +
                $"<size=18>Day {day + 1}の夜が迫っている...</size>";
        }
        nextNightButton?.onClick.RemoveAllListeners();
        Bind(nextNightButton, () => GameManager.Instance.ProceedToNextDay());
    }

    private void OnTrueEnding() => ShowPanel(clearPanel);

    // ===== ポーズ =====
    public void TogglePause()
    {
        if (_isPaused) Resume(); else Pause();
    }

    private void Pause()
    {
        _isPaused      = true;
        Time.timeScale = 0f;
        pausePanel?.SetActive(true);
    }

    public void Resume()
    {
        if (!_isPaused) return;
        _isPaused      = false;
        Time.timeScale = 1f;
        pausePanel?.SetActive(false);
    }

    // ===== マップ / メニュー =====
    public void ToggleMap()
    {
        if (mapPanel) mapPanel.SetActive(!mapPanel.activeSelf);
    }

    private void ReturnToMenu()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void ShowPanel(GameObject p) { if (p) p.SetActive(true); }

    // ===== 表示テキスト変換 =====
    private string PhaseJP(GamePhase p) => p switch
    {
        GamePhase.Silence      => "静寂",
        GamePhase.Omen         => "予兆",
        GamePhase.Contact      => "接触",
        GamePhase.Increase     => "増加",
        GamePhase.Erosion      => "侵食",
        GamePhase.Infiltration => "浸透",
        GamePhase.Siege        => "包囲",
        GamePhase.Collapse     => "崩壊",
        GamePhase.Abyss        => "深淵",
        GamePhase.BeforeDawn   => "夜明け前",
        GamePhase.Dawn         => "夜明け",
        _ => p.ToString()
    };

    private string MonsterNameJP(MonsterType t) => t switch
    {
        MonsterType.Crawler => "クローラー  ―  床を這うもの",
        MonsterType.Rusher  => "ラッシャー  ―  疾走する影",
        MonsterType.Jammer  => "ジャマー    ―  カメラの破壊者",
        MonsterType.Lurker  => "ラーカー    ―  死角の潜伏者",
        MonsterType.Mimic   => "ミミック    ―  映像の偽造者",
        MonsterType.Knocker => "ノッカー    ―  扉を叩くもの",
        _ => t.ToString()
    };

    // Mimic 時刻ズレ: 実時刻から offset 時間ずらした表示を返す
    private string GetFakeDisplayTime()
    {
        float fakeHour = GameManager.Instance.GameTimeInHours + mimicFakeHourOffset;
        if (fakeHour < 20f) fakeHour += 10f;
        float displayHour = fakeHour >= 24f ? fakeHour - 24f : fakeHour;
        int ih = (int)displayHour;
        int im = (int)(GameManager.Instance.PhaseProgress * 60f);
        return $"{ih:D2}:{im:D2}";
    }

    private string WeatherJP(WeatherType w) => w switch
    {
        WeatherType.Sunny => "晴れ",
        WeatherType.Rain  => "雨",
        WeatherType.Storm => "嵐",
        _ => ""
    };

    private string DayBriefing(int next) => next switch
    {
        2 => "施設外部の電気系統に異常あり。\n監視カメラへの干渉が確認された。",
        3 => "侵入物の一部が高速で移動している。\nカメラの切替を遅らせるな。",
        4 => "カメラに映らない侵入者の存在が疑われる。\nカメラ切替のタイミングに注意。",
        5 => "カメラ映像に不審な乱れが発生している。\n過去の映像が再生されているように見える。",
        6 => "B1廊下ドアへの直接攻撃を確認。\n音に惑わされるな、リズムで判断しろ。",
        7 => "最終報告: すべての異常が同時多発している。\n今夜を乗り越えれば離脱できる。",
        _ => ""
    };

    // ===== AutoFindChildren (エディタセットアップから呼ぶ) =====
    public void AutoFindChildren()
    {
        timeText          = FindChild<Text>("TimeText");
        dayText           = FindChild<Text>("DayText");
        powerSlider       = FindChild<Slider>("PowerSlider");
        powerText         = FindChild<Text>("PowerText");
        powerFill         = FindChild<Image>("PowerFill");
        phenomenaWarningText = FindChild<Text>("PhenomenaWarning");
        ghostSignalOverlay   = FindChild<Image>("GhostSignalOverlay");
        weatherStatusText    = FindChild<Text>("WeatherStatus");
        weatherTimerText     = FindChild<Text>("WeatherTimer");
        powerWarnText        = FindChild<Text>("PowerWarn");
        mapPanel          = FindChildGO("MapPanel");
        btnToggleMap      = FindChild<Button>("BtnMap");
        pausePanel        = FindChildGO("PausePanel");
        btnResume         = FindChildIn<Button>(FindChildGO("PausePanel"), "BtnResume");
        btnPauseMenu      = FindChildIn<Button>(FindChildGO("PausePanel"), "BtnPauseMenu");
        gameOverPanel     = FindChildGO("GameOverPanel");
        clearPanel        = FindChildGO("ClearPanel");
        dayTransitionPanel = FindChildGO("DayTransitionPanel");
        dayTransitionText  = FindChild<Text>("DayTransitionText");
        nextNightButton    = FindChild<Button>("NextNightButton");
        retryButton        = FindChild<Button>("RetryButton");
        backToMenuButton   = FindChildIn<Button>(FindChildGO("GameOverPanel"), "BackToMenuButton");
        titleButton        = FindChildIn<Button>(FindChildGO("ClearPanel"),    "TitleButton");
        killerLabel        = FindChild<Text>("KillerLabel");

        btnGate      = FindChild<Button>("BtnGate");
        btnEntrance  = FindChild<Button>("BtnEntrance");
        btnBasement  = FindChild<Button>("BtnBasement");
        btnB1        = FindChild<Button>("BtnB1");
        btnEmergency = FindChild<Button>("BtnEmergency");
        btnEliminate = FindChild<Button>("BtnEliminate");
        btnResetCam    = FindChild<Button>("BtnResetCam");

        indGate      = FindChild<Image>("IndGate");
        indEntrance  = FindChild<Image>("IndEntrance");
        indBasement  = FindChild<Image>("IndBasement");
        indB1        = FindChild<Image>("IndB1");

        btnCamOutN   = FindChild<Button>("BtnOutN");
        btnCamOutE   = FindChild<Button>("BtnOutE");
        btnCamOutW   = FindChild<Button>("BtnOutW");
        btnCamOutTop = FindChild<Button>("BtnOutTop");
        btnCamIn1FA  = FindChild<Button>("BtnIn1FA");
        btnCamIn1FB  = FindChild<Button>("BtnIn1FB");
        btnCamInB1A  = FindChild<Button>("BtnInB1A");
        btnCamInB1B  = FindChild<Button>("BtnInB1B");
    }

    private T FindChild<T>(string n) where T : Component
    {
        var t = FindDeep(transform, n);
        return t ? t.GetComponent<T>() : null;
    }

    private T FindChildIn<T>(GameObject root, string n) where T : Component
    {
        if (!root) return null;
        var t = FindDeep(root.transform, n);
        return t ? t.GetComponent<T>() : null;
    }

    private GameObject FindChildGO(string n)
    {
        var t = FindDeep(transform, n);
        return t ? t.gameObject : null;
    }

    private Transform FindDeep(Transform root, string name)
    {
        foreach (Transform t in root)
        {
            if (t.name == name) return t;
            var found = FindDeep(t, name);
            if (found) return found;
        }
        return null;
    }
}
