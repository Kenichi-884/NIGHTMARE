using UnityEngine;
namespace UnluckSoftware
{
	[DisallowMultipleComponent]
	public class StylizedWeatherWindZone :MonoBehaviour
	{
		[Header("Interval Settings")]
		public float minInterval = 5f;
		public float maxInterval = 15f;

		[Header("Rotation Settings")]
		public float minYawChange = -90f;
		public float maxYawChange = 90f;
		public float rotationDuration = 2f;

		[Header("Smoothing / Easing")]
		public bool useEasingCurve = true;
		public AnimationCurve easingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

		[Header("Behavior")]
		public bool startImmediate = true;
		public bool intervalAfterRotation = true;

		float nextInterval;
		float intervalTimer;
		float rotTimer;
		bool isRotating;
		float startYaw;
		float targetYaw;
		Quaternion startRot;
		Quaternion targetRot;

		void Start()
		{
			if (minInterval < 0f) minInterval = 0f;
			if (maxInterval < minInterval) maxInterval = minInterval;
			if (rotationDuration <= 0.0001f) rotationDuration = 0.0001f;

			ScheduleNextInterval();

			if (startImmediate)
				PickNewTarget();
		}

		void Update()
		{
			intervalTimer += Time.deltaTime;

			if (!isRotating)
			{
				if (intervalTimer >= nextInterval)
				{
					intervalTimer = 0f;
					PickNewTarget();
				}
			} else
			{
				rotTimer += Time.deltaTime;
				float t = Mathf.Clamp01(rotTimer / rotationDuration);
				if (useEasingCurve && easingCurve != null) t = easingCurve.Evaluate(t);
				transform.rotation = Quaternion.Slerp(startRot, targetRot, t);

				if (rotTimer >= rotationDuration)
				{
					transform.rotation = targetRot;
					isRotating = false;
					if (intervalAfterRotation) ScheduleNextInterval();
					else
					{
						intervalTimer = 0f;
						ScheduleNextInterval();
					}
				}
			}
		}

		void PickNewTarget()
		{
			isRotating = true;
			rotTimer = 0f;
			startRot = transform.rotation;
			startYaw = transform.eulerAngles.y;
			float delta = Random.Range(minYawChange, maxYawChange);
			targetYaw = startYaw + delta;
			Vector3 targetEuler = transform.eulerAngles;
			targetEuler.y = targetYaw;
			targetRot = Quaternion.Euler(targetEuler);
		}

		void ScheduleNextInterval()
		{
			nextInterval = Random.Range(minInterval, maxInterval);
			intervalTimer = 0f;
		}
	}
}