namespace UnluckSoftware
{
	using UnityEngine;

	public class DC_Camera :MonoBehaviour
	{
		public Transform targetObject;
		public Vector3 targetObjectOffset;
		public float targetDistance = 5f;
		public float targetMaxDistance = 10f;
		public float targetMinDistance = 1f;
		public float xSpeed = 250f;
		public float ySpeed = 250f;
		public int yMinLimit = -80;
		public int yMaxLimit = 80;
		public int zoomRate = 40;
		public float panSpeed = 0.5f;
		public float zoomDampening = 6f;

		private Quaternion cRot;
		private Quaternion dRot;
		private Quaternion rotation;
		private Vector3 position;

		private float cDist;
		private float dDist;
		private float xDeg;
		private float yDeg;

		void Start()
		{
			dDist = cDist = targetDistance;
			position = transform.position;
			rotation = dRot = cRot = transform.rotation;
			xDeg = Vector3.Angle(Vector3.right, transform.right);
			yDeg = Vector3.Angle(Vector3.up, transform.up);
			position = targetObject.position - (rotation * Vector3.forward * cDist + targetObjectOffset);
		}

		void LateUpdate()
		{

			xDeg += Input.GetAxis("Mouse X") * xSpeed * Time.deltaTime;
			yDeg -= Input.GetAxis("Mouse Y") * ySpeed * Time.deltaTime;
			yDeg = ClampAngle(yDeg, yMinLimit, yMaxLimit);
			dRot = Quaternion.Euler(yDeg, xDeg, 0);
			cRot = transform.rotation;
			rotation = Quaternion.Lerp(cRot, dRot, Time.deltaTime * zoomDampening);
			transform.rotation = rotation;
			dDist -= Input.GetAxis("Mouse ScrollWheel") * Time.deltaTime * zoomRate * Mathf.Abs(dDist);


			dDist = Mathf.Clamp(dDist, targetMinDistance, targetMaxDistance);
			cDist = Mathf.Lerp(cDist, dDist, Time.deltaTime * zoomDampening);


			position = targetObject.position - (rotation * Vector3.forward * cDist + targetObjectOffset);
			transform.position = position;
		}

		private static float ClampAngle(float a, float min, float max)
		{
			if (a < -360)
				a += 360;
			if (a > 360)
				a -= 360;
			return Mathf.Clamp(a, min, max);
		}
	}
}