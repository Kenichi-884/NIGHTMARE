using UnityEngine;
using System.Collections.Generic;

// カメラ映像を過去映像に差し替える心理攻撃タイプ。
// 見破り方: 画面右下の時刻がズレている / ドアの開閉状態と映像が一致しない
public class MimicAI : MonsterBase
{
    private float mimicDuration = 60f;
    private float timer = 0f;
    private CameraID currentTarget;

    public override MonsterType MonsterType => MonsterType.Mimic;
    public override bool IsVisible => false; // カメラには映らない

    public override void Initialize(FacilityLocation spawnLocation, int day)
    {
        baseMoveInterval = 9999f; // 移動しない
        mimicDuration = Mathf.Max(30f, 60f - (day - 1) * 5f); // 日数が増えると乗っ取り時間が長くなる
        base.Initialize(spawnLocation, day);
        PickNewTarget();
    }

    protected override List<FacilityLocation> BuildPath() => new List<FacilityLocation>();

    protected override void Update()
    {
        if (!isActive || GameManager.Instance.CurrentState != GameState.Night) return;

        timer += Time.deltaTime;
        if (timer >= mimicDuration)
        {
            timer = 0f;
            SecurityCameraSystem.Instance.SetMimicTarget(null);
            // 少し間を置いて別のカメラへ
            Invoke(nameof(PickNewTarget), 5f);
        }
    }

    private void PickNewTarget()
    {
        var all = System.Enum.GetValues(typeof(CameraID));
        currentTarget = (CameraID)all.GetValue(Random.Range(0, all.Length));
        SecurityCameraSystem.Instance.SetMimicTarget(currentTarget);
    }

    public override void Remove()
    {
        SecurityCameraSystem.Instance.SetMimicTarget(null);
        CancelInvoke();
        base.Remove();
    }
}
