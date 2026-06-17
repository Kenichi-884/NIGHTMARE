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

    public virtual void Initialize(FacilityLocation spawnLocation, int day)
    {
        currentLocation = spawnLocation;
        // 日数が上がるほど移動が速くなる (最大50%速く)
        float speedMultiplier = 1f - Mathf.Min((day - 1) * 0.07f, 0.5f);
        // 天気による速度補正（雨=0.85倍、嵐=0.70倍）
        float weatherMult = WeatherManager.Instance != null ? WeatherManager.Instance.MoveIntervalMultiplier : 1f;
        moveInterval = baseMoveInterval * speedMultiplier * weatherMult;
        pathIndex = 0;
        moveTimer = 0f;
        movePath = BuildPath();
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
}
