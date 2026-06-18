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

        // 複数体が同時スポーンしても位置が重ならないようにタイマーをずらす
        moveTimer = Random.Range(0f, moveInterval);

        isActive = true;
    }

    protected abstract List<FacilityLocation> BuildPath();

    protected virtual void Update()
    {
        if (!isActive || GameManager.Instance.CurrentState != GameState.Night) return;
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

    protected virtual void OnMoved(FacilityLocation loc) { }
    protected virtual void OnBlocked(FacilityLocation blockedLoc) { }

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
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 1.3f,
            $"step {pathIndex}/{movePath.Count}  {moveTimer:F1}/{moveInterval:F1}s",
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
