// デバッグオーバーレイ
// F12 でトグル表示。DEVELOPMENT_BUILD または UNITY_EDITOR でのみ動作。
// アタッチ先: どのゲームオブジェクトでも可（GameSystems 推奨）
// NightmareSceneSetup で自動追加される。

#if UNITY_EDITOR || DEVELOPMENT_BUILD

using UnityEngine;
using System.Collections.Generic;
using System.Text;

[DisallowMultipleComponent]
public class DebugOverlay : MonoBehaviour
{
    // ─── 設定 ───────────────────────────────────────────────
    [SerializeField] private KeyCode toggleKey     = KeyCode.F12;
    [SerializeField] private KeyCode pauseKey      = KeyCode.F10;
    [SerializeField] private bool    showOnStart   = false;
    [SerializeField] private int     panelWidth    = 620;

    // ─── 状態 ───────────────────────────────────────────────
    private bool  _show;  // Awake で showOnStart から初期化
    private bool  _pause = false;

    // セクション開閉フラグ
    private bool _secState   = true;
    private bool _secSpawn   = true;
    private bool _secMon     = true;
    private bool _secDoors   = true;
    private bool _secCam     = true;
    private bool _secCtrl    = true;

    // スクロール
    private Vector2 _scroll;

    // GUIスタイルキャッシュ
    private GUIStyle _boxBg, _header, _foldout, _label, _labelBold,
                     _labelGreen, _labelRed, _labelYellow, _labelCyan,
                     _btnSmall;
    private bool _stylesInit = false;

    // フェーズ順序（GamePhase enum の進行順）
    private static readonly GamePhase[] PhaseOrder =
    {
        GamePhase.Silence, GamePhase.Omen,  GamePhase.Contact,
        GamePhase.Increase, GamePhase.Erosion, GamePhase.Infiltration,
        GamePhase.Siege,  GamePhase.Collapse, GamePhase.Abyss, GamePhase.BeforeDawn
    };

    private static readonly Dictionary<GamePhase, string> PhaseJP = new()
    {
        { GamePhase.Silence,      "Silence (20時)"     },
        { GamePhase.Omen,         "Omen (21時)"        },
        { GamePhase.Contact,      "Contact (22時)"     },
        { GamePhase.Increase,     "Increase (23時)"    },
        { GamePhase.Erosion,      "Erosion (0時)"      },
        { GamePhase.Infiltration, "Infiltration (1時)" },
        { GamePhase.Siege,        "Siege (2時)"        },
        { GamePhase.Collapse,     "Collapse (3時)"     },
        { GamePhase.Abyss,        "Abyss (4時)"        },
        { GamePhase.BeforeDawn,   "BeforeDawn (5時)"   },
    };

    private static readonly Dictionary<MonsterType, string> MonColTag = new()
    {
        { MonsterType.Crawler, "#FF6644" },
        { MonsterType.Rusher,  "#FFAA00" },
        { MonsterType.Jammer,  "#CC44FF" },
        { MonsterType.Lurker,  "#8888AA" },
        { MonsterType.Mimic,   "#22EEEE" },
        { MonsterType.Knocker, "#FFFF44" },
    };

    // ═══════════════════════════════════════════════════════════
    private void Awake() { _show = showOnStart; }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey)) _show = !_show;
        if (Input.GetKeyDown(pauseKey))
        {
            _pause = !_pause;
            Time.timeScale = _pause ? 0f : 1f;
        }
    }

    private void OnGUI()
    {
        if (!_show) return;
        InitStyles();

        float h = Screen.height * 0.96f;
        var rect = new Rect(8, Screen.height * 0.02f, panelWidth, h);

        // 半透明背景
        GUI.color = new Color(0, 0, 0, 0.88f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
        GUI.color = Color.white;

        GUILayout.BeginArea(rect);
        DrawHeader();
        _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Width(panelWidth), GUILayout.Height(h - 36));

        DrawGameState();
        DrawSpawnSchedule();
        DrawActiveMonsters();
        DrawDoors();
        DrawCameras();
        DrawControls();

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    // ─────────────────────────────────────────────────────────
    // ヘッダー
    // ─────────────────────────────────────────────────────────
    private void DrawHeader()
    {
        GUILayout.BeginHorizontal(_boxBg);
        GUILayout.Label($"<b>■ DEBUG</b>   [{toggleKey} で閉じる]", _header, GUILayout.ExpandWidth(true));

        // タイムスケール
        foreach (float ts in new[] { 0.25f, 0.5f, 1f, 2f, 5f })
        {
            bool cur = Mathf.Approximately(Time.timeScale, ts) && !_pause;
            GUI.color = cur ? new Color(0.4f, 1f, 0.5f) : Color.white;
            if (GUILayout.Button($"×{ts}", _btnSmall, GUILayout.Width(48)))
            {
                _pause = false;
                Time.timeScale = ts;
            }
        }
        GUI.color = Color.white;

        // ポーズ
        GUI.color = _pause ? new Color(1f, 0.5f, 0.2f) : Color.white;
        if (GUILayout.Button(_pause ? "▶" : "⏸", _btnSmall, GUILayout.Width(36)))
        {
            _pause = !_pause;
            Time.timeScale = _pause ? 0f : 1f;
        }
        GUI.color = Color.white;

        GUILayout.EndHorizontal();
        Sep();
    }

    // ─────────────────────────────────────────────────────────
    // ゲーム状態
    // ─────────────────────────────────────────────────────────
    private void DrawGameState()
    {
        if (!Foldout(ref _secState, "GAME STATE")) return;

        var gm = GameManager.Instance;
        var pm = PowerManager.Instance;
        if (gm == null) { GUILayout.Label("GameManager が見つかりません", _labelRed); EndFold(); return; }

        // Day / State
        GUILayout.BeginHorizontal();
        GUILayout.Label($"<b>Day {gm.CurrentDay}</b>  /  State: {gm.CurrentState}", _label);
        GUILayout.EndHorizontal();

        // Time / Phase
        string phaseStr = PhaseJP.TryGetValue(gm.CurrentPhase, out var pj) ? pj : gm.CurrentPhase.ToString();
        GUILayout.Label($"時刻: <color=#88FF88>{gm.GetDisplayTime()}</color>   Phase: <color=#AACCFF>{phaseStr}</color>", _label);

        // フェーズ進捗バー + 残り時間
        float prog = gm.PhaseProgress;
        float secsPerHour = 150f;
        float remaining = (1f - prog) * secsPerHour;
        ProgressBar(prog, $"次フェーズまで {remaining:F0}s", new Color(0.3f, 0.55f, 1f));

        // 電力
        if (pm != null)
        {
            float pct = pm.PowerPercent;
            Color barCol = pct > 0.5f ? new Color(0.2f, 0.9f, 0.2f)
                         : pct > 0.25f ? new Color(1f, 0.7f, 0.1f)
                         : new Color(1f, 0.2f, 0.1f);
            string powerLabel = pm.IsPowerOut ? "<color=#FF4444>[停電中]</color>" : $"{pm.CurrentPower:F1}%";
            ProgressBar(pct, $"POWER  {powerLabel}", barCol);

            // ドレイン内訳
            float drainPerMin = pm.TotalDrainPerSecond() * 60f;
            GUILayout.Label($"  消費: <color=#FFAA44>{drainPerMin:F2}%/分</color>", _label);
        }

        EndFold();
    }

    // ─────────────────────────────────────────────────────────
    // スポーン予告
    // ─────────────────────────────────────────────────────────
    private void DrawSpawnSchedule()
    {
        if (!Foldout(ref _secSpawn, "SPAWN SCHEDULE (この夜)")) return;

        var gm = GameManager.Instance;
        var ms = MonsterSpawner.Instance;
        if (gm == null || ms == null || ms.SpawnTable == null) { EndFold(); return; }

        int day = gm.CurrentDay;
        var curPhase = gm.CurrentPhase;
        int curIdx = System.Array.IndexOf(PhaseOrder, curPhase);

        // 全フェーズのスポーンをグループ化
        var byPhase = new Dictionary<GamePhase, List<MonsterSpawner.SpawnEntry>>();
        foreach (var e in ms.SpawnTable)
        {
            if (e.day != day) continue;
            if (!byPhase.ContainsKey(e.phase)) byPhase[e.phase] = new List<MonsterSpawner.SpawnEntry>();
            byPhase[e.phase].Add(e);
        }

        // フェーズ順に表示
        for (int i = 0; i < PhaseOrder.Length; i++)
        {
            var phase = PhaseOrder[i];
            bool isCurrent = (phase == curPhase);
            bool isPast    = (i < curIdx);
            bool hasSpawn  = byPhase.ContainsKey(phase);

            string phaseName = PhaseJP.TryGetValue(phase, out var pj2) ? pj2 : phase.ToString();

            if (!hasSpawn && !isCurrent) continue;  // 空かつ現在でないフェーズは省略

            GUILayout.BeginHorizontal();

            // マーカー
            if (isCurrent)
                GUILayout.Label("▶", _labelCyan, GUILayout.Width(20));
            else if (isPast)
                GUILayout.Label("✓", _labelGreen, GUILayout.Width(20));
            else
                GUILayout.Label("·", _label, GUILayout.Width(20));

            string col = isCurrent ? "#22DDFF" : isPast ? "#888888" : "#DDDDDD";
            GUILayout.Label($"<color={col}><b>{phaseName}</b></color>", _label, GUILayout.Width(220));

            if (hasSpawn)
            {
                var sb = new StringBuilder();
                foreach (var e in byPhase[phase])
                {
                    string mc = MonColTag.TryGetValue(e.type, out var tc) ? tc : "#FFFFFF";
                    sb.Append($"<color={mc}>{e.type}</color>×{e.count} ");
                }
                string style = isPast ? "#666666" : "#FFFFFF";
                GUILayout.Label($"<color={style}>{sb}</color>", _label);
            }
            else
            {
                GUILayout.Label("<color=#444444>なし</color>", _label);
            }

            GUILayout.EndHorizontal();
        }

        // 今夜スポーンしない日
        if (!byPhase.ContainsKey(GamePhase.Contact) && byPhase.Count == 0)
            GUILayout.Label("<color=#888888>  今夜のスポーンなし</color>", _label);

        EndFold();
    }

    // ─────────────────────────────────────────────────────────
    // アクティブモンスター
    // ─────────────────────────────────────────────────────────
    private void DrawActiveMonsters()
    {
        var mm = MonsterManager.Instance;
        int count = mm?.ActiveMonsters?.Count ?? 0;
        if (!Foldout(ref _secMon, $"ACTIVE MONSTERS ({count})")) return;

        if (mm == null || count == 0)
        {
            GUILayout.Label("  <color=#888888>アクティブなモンスターなし</color>", _label);
            EndFold(); return;
        }

        foreach (var m in mm.ActiveMonsters)
        {
            string mc = MonColTag.TryGetValue(m.MonsterType, out var tc) ? tc : "#FFFFFF";
            string locShort = m.CurrentLocation.ToString().Replace("Outside_", "Out/")
                                                          .Replace("Lobby_", "Lby/")
                                                          .Replace("B1_", "B1/");

            float timerPct = m.MoveInterval > 0 ? m.MoveTimer / m.MoveInterval : 0f;
            string timerStr = $"{m.MoveTimer:F1}/{m.MoveInterval:F1}s";

            GUILayout.BeginHorizontal();
            GUILayout.Label($"<color={mc}><b>{m.MonsterType,-8}</b></color>", _label, GUILayout.Width(100));
            GUILayout.Label($"@{locShort,-18}", _label, GUILayout.Width(180));
            GUILayout.Label($"[{m.PathIndex}/{m.PathLength}]", _label, GUILayout.Width(68));

            // 移動タイマーバー
            Rect r = GUILayoutUtility.GetRect(110, 20);
            GUI.color = new Color(0.15f, 0.15f, 0.15f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = timerPct > 0.85f ? new Color(1f, 0.3f, 0.1f) : new Color(0.2f, 0.8f, 0.4f);
            var fill = new Rect(r.x, r.y, r.width * timerPct, r.height);
            GUI.DrawTexture(fill, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(r, $" {timerStr}", _label);

            GUILayout.EndHorizontal();
        }

        EndFold();
    }

    // ─────────────────────────────────────────────────────────
    // ドア
    // ─────────────────────────────────────────────────────────
    private void DrawDoors()
    {
        if (!Foldout(ref _secDoors, "DOORS")) return;

        var dm = DoorManager.Instance;
        if (dm == null) { EndFold(); return; }

        var ids = new[] { DoorID.Gate, DoorID.Entrance, DoorID.BasementStairs, DoorID.B1Corridor };
        var names = new[] { "Gate", "入口", "地下階段", "B1廊下" };

        GUILayout.BeginHorizontal();
        for (int i = 0; i < ids.Length; i++)
        {
            bool closed = dm.IsClosed(ids[i]);
            string col = closed ? "#FF3322" : "#22FF44";
            string icon = closed ? "■" : "□";
            GUILayout.Label($"<color={col}>{icon} {names[i]}</color>", _label, GUILayout.Width(120));
        }
        GUILayout.EndHorizontal();

        // 電力ドレインも表示
        GUILayout.BeginHorizontal();
        for (int i = 0; i < ids.Length; i++)
        {
            bool closed = dm.IsClosed(ids[i]);
            if (closed)
            {
                var data = dm.GetData(ids[i]);
                if (data != null)
                    GUILayout.Label($"  {data.drainPerSecond * 60f:F1}%/分", _labelYellow, GUILayout.Width(120));
                else
                    GUILayout.Label("", _label, GUILayout.Width(120));
            }
            else
            {
                GUILayout.Label("  --", _label, GUILayout.Width(120));
            }
        }
        GUILayout.EndHorizontal();

        // 緊急封鎖カウントダウン
        if (dm.EmergencyCountdown > 0f)
        {
            float t = dm.EmergencyCountdown / 10f;
            ProgressBar(t, $"緊急封鎖解除まで {dm.EmergencyCountdown:F1}s", new Color(1f, 0.3f, 0.1f));
        }

        EndFold();
    }

    // ─────────────────────────────────────────────────────────
    // カメラ
    // ─────────────────────────────────────────────────────────
    private void DrawCameras()
    {
        if (!Foldout(ref _secCam, "CAMERAS")) return;

        var cs = SecurityCameraSystem.Instance;
        if (cs == null) { EndFold(); return; }

        GUILayout.Label($"  選択中: <color=#22DDFF>{cs.ActiveCamera}</color>", _label);

        // 死亡カメラ
        var dead = new List<string>();
        var allCams = System.Enum.GetValues(typeof(CameraID));
        foreach (CameraID cid in allCams)
        {
            if (cs.IsCameraDead(cid)) dead.Add($"<color=#666666>{cid}</color>");
        }
        if (dead.Count > 0)
            GUILayout.Label($"  Dead: {string.Join(", ", dead)}", _label);
        else
            GUILayout.Label("  Dead: <color=#444444>なし</color>", _label);

        // Mimic
        foreach (CameraID cid in allCams)
        {
            if (cs.IsCameraMimicked(cid))
                GUILayout.Label($"  <color=#22EEEE>Mimic 乗っ取り中: {cid}</color>", _label);
        }

        EndFold();
    }

    // ─────────────────────────────────────────────────────────
    // デバッグコントロール
    // ─────────────────────────────────────────────────────────
    private void DrawControls()
    {
        if (!Foldout(ref _secCtrl, "DEBUG CONTROLS")) return;

        var gm = GameManager.Instance;
        var pm = PowerManager.Instance;
        var dm = DoorManager.Instance;
        var mm = MonsterManager.Instance;

        // ── Day 選択 ──
        GUILayout.Label("<b>■ Day 選択（即時切り替え）</b>", _labelBold);
        GUILayout.BeginHorizontal();
        for (int d = 1; d <= 7; d++)
        {
            bool isCur = gm != null && gm.CurrentDay == d;
            GUI.color = isCur ? new Color(0.4f, 1f, 0.5f) : Color.white;
            if (GUILayout.Button($"Day{d}", _btnSmall))
            {
                mm?.ClearAll();
                gm?.StartNight(d);
            }
        }
        GUI.color = Color.white;
        GUILayout.EndHorizontal();

        Sep();

        // ── フェーズ / 時間操作 ──
        GUILayout.Label("<b>■ 時間操作</b>", _labelBold);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("次フェーズ +", _btnSmall))   ForceNextPhase();
        if (GUILayout.Button("夜明け", _btnSmall))          ForceNightClear();
        if (GUILayout.Button("GAME OVER", _btnSmall))
        {
            gm?.TriggerGameOver(null);
        }
        GUILayout.EndHorizontal();

        Sep();

        // ── 電力操作 ──
        GUILayout.Label("<b>■ 電力</b>", _labelBold);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+10%",  _btnSmall) && pm != null) pm.AddPower(10f);
        if (GUILayout.Button("-10%",  _btnSmall) && pm != null) pm.ApplyFluctuation(10f);
        if (GUILayout.Button("-30%",  _btnSmall) && pm != null) pm.ApplyFluctuation(30f);
        if (GUILayout.Button("満タン", _btnSmall) && pm != null) pm.AddPower(100f);
        GUILayout.EndHorizontal();

        Sep();

        // ── モンスタースポーン ──
        GUILayout.Label("<b>■ スポーン (N=北/E=東/W=西)</b>", _labelBold);
        GUILayout.BeginHorizontal();
        foreach (MonsterType mt in System.Enum.GetValues(typeof(MonsterType)))
        {
            string mc = MonColTag.TryGetValue(mt, out var tc2) ? tc2 : "#FFFFFF";
            if (GUILayout.Button($"<color={mc}>{mt}</color>", _btnSmall))
            {
                // Night状態でなければ自動起動
                if (gm?.CurrentState != GameState.Night) gm?.StartNight();
                FacilityLocation spawnLoc = mt switch
                {
                    MonsterType.Rusher => FacilityLocation.Outside_East,
                    MonsterType.Jammer => FacilityLocation.Outside_East,
                    MonsterType.Lurker => FacilityLocation.Outside_West,
                    _ => FacilityLocation.Outside_North
                };
                mm?.Spawn(mt, spawnLoc);
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("全員追放", _btnSmall)) mm?.ClearAll();
        if (GUILayout.Button("ドア全開", _btnSmall)) dm?.OpenAll();
        if (GUILayout.Button("緊急封鎖", _btnSmall)) dm?.EmergencyLockdown();
        GUILayout.EndHorizontal();

        Sep();

        // ── カメラリセット ──
        GUILayout.Label("<b>■ カメラ</b>", _labelBold);
        var camIds = (CameraID[])System.Enum.GetValues(typeof(CameraID));
        // 屋外 (前半4台)
        GUILayout.BeginHorizontal();
        for (int i = 0; i < camIds.Length / 2; i++)
        {
            string lbl = camIds[i].ToString().Replace("OUT_", "O-").Replace("IN_", "I-");
            if (GUILayout.Button($"Kill {lbl}", _btnSmall))
                SecurityCameraSystem.Instance?.KillCamera(camIds[i]);
        }
        GUILayout.EndHorizontal();
        // 屋内 (後半4台)
        GUILayout.BeginHorizontal();
        for (int i = camIds.Length / 2; i < camIds.Length; i++)
        {
            string lbl = camIds[i].ToString().Replace("OUT_", "O-").Replace("IN_", "I-");
            if (GUILayout.Button($"Kill {lbl}", _btnSmall))
                SecurityCameraSystem.Instance?.KillCamera(camIds[i]);
        }
        GUILayout.EndHorizontal();
        if (GUILayout.Button("全カメラ復旧", _btnSmall, GUILayout.Height(30)))
        {
            foreach (CameraID cid in System.Enum.GetValues(typeof(CameraID)))
                SecurityCameraSystem.Instance?.TryResetCamera(cid);
        }

        EndFold();
    }

    // ─────────────────────────────────────────────────────────
    // フェーズ強制スキップ (GameManager の内部タイマーを操作)
    // ─────────────────────────────────────────────────────────
    private void ForceNextPhase()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        var timerField = gm.GetType().GetField("phaseTimer",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var hourField  = gm.GetType().GetField("realSecondsPerGameHour",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (timerField != null && hourField != null)
        {
            float perHour = (float)hourField.GetValue(gm);
            timerField.SetValue(gm, perHour + 1f); // 次フレームで進行
        }
    }

    private void ForceNightClear()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        // GameTimeInHours = 29f (次の +1 で 30 に到達 → TriggerNightClear)
        var bf = gm.GetType().GetField("<GameTimeInHours>k__BackingField", flags);
        bf?.SetValue(gm, 29f);

        // phaseTimer を超過させて即時ティックを起こす
        var timerField   = gm.GetType().GetField("phaseTimer", flags);
        var perHourField = gm.GetType().GetField("realSecondsPerGameHour", flags);
        if (timerField != null && perHourField != null)
            timerField.SetValue(gm, (float)perHourField.GetValue(gm) + 1f);
    }

    // ─────────────────────────────────────────────────────────
    // UI ヘルパー
    // ─────────────────────────────────────────────────────────
    private bool Foldout(ref bool state, string title)
    {
        GUILayout.BeginHorizontal();
        string arrow = state ? "▼" : "▶";
        if (GUILayout.Button($"{arrow} {title}", _foldout))
            state = !state;
        GUILayout.EndHorizontal();
        return state;
    }

    private void EndFold() => Sep();

    private void Sep()
        => GUILayout.Box("", GUILayout.Height(1), GUILayout.ExpandWidth(true));

    private void ProgressBar(float t, string label, Color col)
    {
        t = Mathf.Clamp01(t);
        Rect r = GUILayoutUtility.GetRect(panelWidth - 16, 20);
        GUI.color = new Color(0.12f, 0.12f, 0.14f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = col;
        GUI.DrawTexture(new Rect(r.x, r.y, r.width * t, r.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(r, $"  {label}", _label);
    }

    // ─────────────────────────────────────────────────────────
    // スタイル初期化
    // ─────────────────────────────────────────────────────────
    private void InitStyles()
    {
        if (_stylesInit) return;
        _stylesInit = true;

        _boxBg = new GUIStyle(GUI.skin.box)
        {
            normal  = { background = Texture2D.whiteTexture },
            padding = new RectOffset(4, 4, 2, 2),
            margin  = new RectOffset(0, 0, 0, 0),
        };

        _header = new GUIStyle(GUI.skin.label)
        {
            fontSize        = 15,
            fontStyle       = FontStyle.Bold,
            richText        = true,
            normal          = { textColor = new Color(0.9f, 0.95f, 1f) },
        };

        _foldout = new GUIStyle(GUI.skin.button)
        {
            fontSize        = 14,
            fontStyle       = FontStyle.Bold,
            richText        = true,
            alignment       = TextAnchor.MiddleLeft,
            normal          = { textColor = new Color(0.7f, 0.85f, 1f), background = MakeTex(new Color(0.14f, 0.18f, 0.28f)) },
            hover           = { textColor = Color.white,                 background = MakeTex(new Color(0.20f, 0.26f, 0.40f)) },
            padding         = new RectOffset(10, 4, 5, 5),
            margin          = new RectOffset(0, 0, 1, 1),
        };

        _label = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 14,
            richText  = true,
            wordWrap  = false,
            normal    = { textColor = new Color(0.85f, 0.88f, 0.92f) },
        };

        _labelBold = new GUIStyle(_label) { fontStyle = FontStyle.Bold };

        _labelGreen  = new GUIStyle(_label) { normal = { textColor = new Color(0.3f, 1f, 0.5f) } };
        _labelRed    = new GUIStyle(_label) { normal = { textColor = new Color(1f, 0.3f, 0.2f) } };
        _labelYellow = new GUIStyle(_label) { normal = { textColor = new Color(1f, 0.85f, 0.2f) } };
        _labelCyan   = new GUIStyle(_label) { normal = { textColor = new Color(0.2f, 0.9f, 1f) } };

        _btnSmall = new GUIStyle(GUI.skin.button)
        {
            fontSize  = 13,
            richText  = true,
            padding   = new RectOffset(8, 8, 5, 5),
            margin    = new RectOffset(2, 2, 2, 2),
            normal    = { textColor = new Color(0.85f, 0.9f, 1f), background = MakeTex(new Color(0.16f, 0.22f, 0.35f)) },
            hover     = { textColor = Color.white,                  background = MakeTex(new Color(0.25f, 0.35f, 0.52f)) },
        };
    }

    private static Texture2D MakeTex(Color col)
    {
        var t = new Texture2D(1, 1);
        t.SetPixel(0, 0, col);
        t.Apply();
        return t;
    }
}

#else

// リリースビルドでは何もしない（クラス定義のみ残す）
public class DebugOverlay : UnityEngine.MonoBehaviour { }

#endif
