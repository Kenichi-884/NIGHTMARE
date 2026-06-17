using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SickscoreGames.ExampleScene
{
	public class ExampleMouseLook : MonoBehaviour
	{
		#region Variables
		public float sensitivityX = 3f;
		public float sensitivityY = 3f;

		public Vector2 rotationLimitsX = new Vector2 (-360f, 360f);
		public Vector2 rotationLimitsY = new Vector2 (-60f, 60f);
		public float rotationSmooth = 8f;

		private Quaternion _rotationOrigin;
		private float _currentRotationX, _currentRotationY = 0f;
		#endregion


		#region Main Methods
		void Awake ()
		{
			_rotationOrigin = transform.localRotation;
#if ENABLE_INPUT_SYSTEM
			sensitivityX /= 4;
			sensitivityY /= 4;
#endif
		}

		void Update ()
		{
			// get input
			float mouseX = 0f;
			float mouseY = 0f;
#if ENABLE_INPUT_SYSTEM
			if (Mouse.current != null)
			{
			    mouseX = Mouse.current.delta.x.ReadValue();
			    mouseY = Mouse.current.delta.y.ReadValue();
			}
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
			mouseX = Input.GetAxis ("Mouse X");
			mouseY = Input.GetAxis ("Mouse Y");
#endif

			// calculate and apply rotations
			if (axes == RotationAxes.MouseX) {
				_currentRotationX += mouseX * sensitivityX;
				_currentRotationX = this.ClampAngle (_currentRotationX, rotationLimitsX.x, rotationLimitsX.y);
				Quaternion rotationX = Quaternion.AngleAxis (_currentRotationX, Vector3.up);
				transform.localRotation = Quaternion.Lerp (transform.localRotation, _rotationOrigin * rotationX, rotationSmooth * Time.deltaTime);
			} else {
				_currentRotationY += mouseY * sensitivityY;
				_currentRotationY = this.ClampAngle (_currentRotationY, rotationLimitsY.x, rotationLimitsY.y);
				Quaternion rotationY = Quaternion.AngleAxis (-_currentRotationY, Vector3.right);
				transform.localRotation = Quaternion.Lerp (transform.localRotation, _rotationOrigin * rotationY, rotationSmooth * Time.deltaTime);
			}
		}

		#endregion


		#region Utility Methods
		float ClampAngle (float angle, float min, float max)
		{
			angle %= 360f;
			if (angle < -360f)
				angle += 360f;
			if (angle > 360f)
				angle -= 360f;
			
			return Mathf.Clamp (angle, min, max);
		}
		#endregion


		#region Subclasses
		public enum RotationAxes { MouseX, MouseY }
		public RotationAxes axes = RotationAxes.MouseX;
		#endregion
	}
}
