using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// マップ上の各部屋ノードを管理するコンポーネント。
/// FacilityMapUI が生成時に AddComponent して Init() で初期化する。
/// 侵入者がいる部屋は自動的にパルスアニメーションを再生する。
///
/// プレハブ対応:
///   MapPanel.prefab のインスタンスを Hierarchy に置いた状態で
///   各 Room_* オブジェクトに自動アタッチされる。
///   MapTheme の差し替えは FacilityMapUI.theme フィールドで行う。
/// </summary>
[DisallowMultipleComponent]
public class MapRoomNode : MonoBehaviour
{
    public FacilityLocation Location { get; private set; }
    public bool             IsPlayer { get; private set; }

    private Image   bg;
    private Outline border;
    private Color   baseBg;
    private Color   dangerBg;
    private Color   baseBorderCol;
    private Color   dangerBorderCol;
    private float   pulse;
    private bool    hasDanger;

    /// <summary>FacilityMapUI から呼ぶ初期化メソッド</summary>
    public void Init(
        FacilityLocation loc,
        bool             isPlayer,
        Image            bgImage,
        Outline          borderOutline,
        Color            baseBgColor,
        Color            dangerBgColor,
        Color            baseBorderColor)
    {
        Location        = loc;
        IsPlayer        = isPlayer;
        bg              = bgImage;
        border          = borderOutline;
        baseBg          = baseBgColor;
        dangerBg        = dangerBgColor;
        baseBorderCol   = baseBorderColor;
        dangerBorderCol = new Color(1f, 0.20f, 0.10f, 1f);
    }

    private void Update()
    {
        if (!hasDanger) return;
        pulse += Time.deltaTime;
        float t = (Mathf.Sin(pulse * 5.5f) + 1f) * 0.5f;

        if (bg)     bg.color             = Color.Lerp(baseBg,         dangerBg,        t * 0.75f);
        if (border) border.effectColor   = Color.Lerp(baseBorderCol,  dangerBorderCol, t * 0.85f);
    }

    /// <summary>侵入者の有無を通知する。true でパルスアニメ開始、false でリセット。</summary>
    public void SetDanger(bool danger)
    {
        if (hasDanger == danger) return;
        hasDanger = danger;
        pulse     = 0f;

        if (!danger)
        {
            if (bg)     bg.color           = baseBg;
            if (border) border.effectColor = baseBorderCol;
        }
    }
}
