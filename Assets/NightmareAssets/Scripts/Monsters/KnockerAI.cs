using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// B1廊下ドアを直接叩く。本物と偽物がある。音のリズムで判別する。
// 本物: 規則的なリズム / 偽物: 不規則なリズム
public class KnockerAI : MonsterBase
{
    private bool isFake;
    private float timeAtDoor = 0f;
    private float knockTimer = 0f;
    private bool giveUpPending = false;
    private const float KNOCK_INTERVAL = 3f;
    private const float BREAK_IN_TIME = 30f; // ドアが開いていると30秒で侵入

    public override MonsterType MonsterType => MonsterType.Knocker;

    public override void Initialize(FacilityLocation spawnLocation, int day)
    {
        baseMoveInterval = 9999f;
        base.Initialize(spawnLocation, day);

        // 日数が上がるほど本物が増える (Day1=30%, Day7=80%)
        float realChance = Mathf.Min(0.3f + (day - 1) * 0.08f, 0.8f);
        isFake = Random.value > realChance;

        currentLocation = FacilityLocation.B1_DoorFront;
        AudioManager.Instance?.Play(isFake ? "knock_irregular" : "knock_regular");
    }

    protected override List<FacilityLocation> BuildPath() => new List<FacilityLocation>();

    protected override void Update()
    {
        if (!isActive || GameManager.Instance.CurrentState != GameState.Night) return;

        knockTimer += Time.deltaTime;
        if (knockTimer >= KNOCK_INTERVAL)
        {
            knockTimer = 0f;
            AudioManager.Instance?.Play(isFake ? "knock_irregular" : "knock_regular");
        }

        if (!isFake)
        {
            bool doorClosed = DoorManager.Instance.IsClosed(DoorID.B1Corridor);
            if (!doorClosed)
            {
                timeAtDoor += Time.deltaTime;
                giveUpPending = false; // ドアが開いたらフラグをリセット
                if (timeAtDoor >= BREAK_IN_TIME)
                    OnReachManagersRoom();
            }
            else
            {
                timeAtDoor = 0f;
                if (!giveUpPending)
                {
                    giveUpPending = true;
                    StartCoroutine(CheckGiveUp());
                }
            }
        }
        else
        {
            // フェイクは一定時間後に自然消滅
            timeAtDoor += Time.deltaTime;
            if (timeAtDoor >= 60f) Remove();
        }
    }

    private IEnumerator CheckGiveUp()
    {
        yield return new WaitForSeconds(30f);
        if (isActive && DoorManager.Instance.IsClosed(DoorID.B1Corridor))
            Remove();
    }
}
