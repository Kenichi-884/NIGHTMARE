using UnityEngine;
using System.Collections.Generic;

public abstract class MonsterBase : MonoBehaviour
{
    [SerializeField] protected Sprite cameraSprite;
    [SerializeField] protected float baseMoveInterval = 60f;

    protected FacilityLocation currentLocation;
    protected List<FacilityLocation> movePath;
    protected int pathIndex = 0;
    protected float moveTimer = 0f;
    protected bool isActive = false;
    protected float moveInterval;

    // B1_DoorFront に到達後、一定時間待機してから侵入する
    [SerializeField] protected float doorFrontHoldTime = 20f;
    protected bool isAtDoorFront = false;
    private float attackCountdown = 0f;

    public FacilityLocation CurrentLocation => currentLocation;
    public virtual bool IsVisible => true;
    public Sprite CameraSprite => cameraSprite;
    public abstract MonsterType MonsterType { get; }

    // デバッグ用
    public int   PathIndex    => pathIndex;
    public float MoveTimer    => moveTimer;
    public float MoveInterval => moveInterval;
    public int   PathLength   => movePath?.Count ?? 0;

    public virtual void Initialize(FacilityLocation spawnLocation, int day)
    {
        currentLocation = spawnLocation;
        // 日数が上がるほど移動が速くなる (最大50%速く)
        float speedMultiplier = 1f - Mathf.Min((day - 1) * 0.07f, 0.5f);
        // 天気による速度補正（雨=0.85倍、嵐=0.70倍）
        float weatherMult = WeatherManager.Instance != null ? WeatherManager.Instance.MoveIntervalMultiplier : 1f;
        moveInterval = baseMoveInterval * speedMultiplier * weatherMult;
        movePath = BuildPath();

        // パスの先頭がスポーン地点と同じなら1つ飛ばす（最初のステップが no-op になるバグを修正）
        pathIndex = (movePath.Count > 0 && movePath[0] == spawnLocation) ? 1 : 0;

        // スポーン直後は必ず外部に留まり、1インターバル後に最初の移動を行う
        moveTimer = 0f;

        isActive = true;
    }

    protected abstract List<FacilityLocation> BuildPath();

    protected virtual void Update()
    {
        if (!isActive || GameManager.Instance.CurrentState != GameState.Night) return;

        // B1_DoorFront 到達後の侵入カウントダウン
        if (isAtDoorFront)
        {
            // B1廊下ドアが閉じていればカウントを止める（最後の防衛ライン）
            if (!DoorManager.Instance.IsBlocked(FacilityLocation.B1_DoorFront))
            {
                attackCountdown += Time.deltaTime;
                if (attackCountdown >= doorFrontHoldTime)
                {
                    isAtDoorFront = false;
                    currentLocation = FacilityLocation.ManagersRoom;
                    OnReachManagersRoom();
                }
            }
            return;
        }

        moveTimer += Time.deltaTime;
        if (moveTimer >= moveInterval)
        {
            moveTimer = 0f;
            TryAdvance();
        }
    }

    protected virtual void TryAdvance()
    {
        if (pathIndex >= movePath.Count) return;

        FacilityLocation next = movePath[pathIndex];

        if (DoorManager.Instance.IsBlocked(next))
        {
            OnBlocked(next);
            return;
        }

        currentLocation = next;
        pathIndex++;
        OnMoved(currentLocation);

        if (currentLocation == FacilityLocation.ManagersRoom)
            OnReachManagersRoom();
    }

    protected virtual void OnMoved(FacilityLocation loc)
    {
        if (loc == FacilityLocation.B1_DoorFront)
            EnterDoorFrontState();
    }

    protected virtual void OnBlocked(FacilityLocation blockedLoc) { }

    // B1_DoorFront 待機ステートに入る（Lurker のテレポートからも呼ぶ）
    protected void EnterDoorFrontState()
    {
        isAtDoorFront = true;
        attackCountdown = 0f;
        OnArrivedAtDoorFront();
    }

    // 到着演出（サブクラスでオーバーライド可）
    protected virtual void OnArrivedAtDoorFront()
    {
        AudioManager.Instance?.Play("door_pounding");
    }

    protected virtual void OnReachManagersRoom()
    {
        GameManager.Instance.TriggerGameOver(MonsterType);
    }

    public virtual void Remove()
    {
        isActive = false;
        MonsterManager.Instance?.Unregister(this);
        Destroy(gameObject);
    }

    // ===== ギズモ =====
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = GizmoColor();
        Gizmos.DrawWireSphere(transform.position, 0.4f);

        var style = new GUIStyle { fontSize = 10 };
        style.normal.textColor = GizmoColor();
        string label = isActive
            ? $"{MonsterType}\n{currentLocation}"
            : $"{MonsterType}  [待機]";
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.65f, label, style);
    }

    private void OnDrawGizmosSelected()
    {
        if (movePath == null || movePath.Count == 0) return;

        var style = new GUIStyle { fontSize = 9 };
        style.normal.textColor = Color.white;
        string timerInfo = isAtDoorFront
            ? $"[扉前] {attackCountdown:F1}/{doorFrontHoldTime:F1}s"
            : $"step {pathIndex}/{movePath.Count}  {moveTimer:F1}/{moveInterval:F1}s";
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 1.3f,
            timerInfo,
            style);
    }

    private Color GizmoColor() => MonsterType switch
    {
        MonsterType.Crawler => Color.red,
        MonsterType.Jammer  => new Color(1f, 0.55f, 0f),
        MonsterType.Rusher  => Color.yellow,
        MonsterType.Lurker  => new Color(0.6f, 0.1f, 1f),
        MonsterType.Mimic   => new Color(0f, 1f, 1f),
        MonsterType.Knocker => new Color(1f, 0.1f, 0.5f),
        _                   => Color.white,
    };
#endif
}
