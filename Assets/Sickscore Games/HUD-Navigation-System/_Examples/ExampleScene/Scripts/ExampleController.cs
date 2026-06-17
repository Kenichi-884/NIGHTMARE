using UnityEngine;

namespace SickscoreGames.ExampleScene
{
	[RequireComponent(typeof(Rigidbody))]
	[RequireComponent(typeof(CapsuleCollider))]
	public class ExampleController : MonoBehaviour
	{
		#region Variables
		public float walkSpeed = 8f;
		public float runSpeed = 12f;
		public float jumpHeight = 3.25f;
		public float gravity = 28f;

		private Transform _transform;
		private Rigidbody _rigidbody;
		private bool _isGrounded;
		#endregion


		#region Main Methods
		void Awake ()
		{
			// assign components
			_transform = this.transform;
			_rigidbody = GetComponent<Rigidbody> ();

			// setup rigidbody
			_rigidbody.freezeRotation = true;
			_rigidbody.useGravity = false;
		}


		void FixedUpdate ()
		{
			// check if grounded
			if (_isGrounded) {
				// directional input
				Vector3 targetVelocity = new Vector3 (ExampleUniversalInput.GetAxis("Horizontal"), 0f, ExampleUniversalInput.GetAxis ("Vertical"));
				float moveSpeed = (ExampleUniversalInput.GetKey (KeyCode.LeftShift)) ? runSpeed : walkSpeed;
				targetVelocity = _transform.TransformDirection (targetVelocity) * moveSpeed;

				// calculate velocity and max velocity change
#if UNITY_6000_0_OR_NEWER
				Vector3 velocity = _rigidbody.linearVelocity;
#else
				Vector3 velocity = _rigidbody.velocity;
#endif
				Vector3 velocityChange = (targetVelocity - velocity);
				velocityChange.x = Mathf.Clamp (velocityChange.x, -8f, 8f);
				velocityChange.z = Mathf.Clamp (velocityChange.z, -8f, 8f);
				velocityChange.y = 0f;
				_rigidbody.AddForce (velocityChange, ForceMode.VelocityChange);

				// jump input
				if (ExampleUniversalInput.GetKeyDown(KeyCode.Space))
				{
#if UNITY_6000_0_OR_NEWER
					_rigidbody.linearVelocity = new Vector3 (velocity.x, CalculateJumpVerticalSpeed (), velocity.z);
#else
					_rigidbody.velocity = new Vector3 (velocity.x, CalculateJumpVerticalSpeed (), velocity.z);
#endif
				}
			}

			// apply force to rigidbody
			_rigidbody.AddForce (new Vector3 (0f, -gravity * _rigidbody.mass, 0f));

			_isGrounded = false;
		}
		#endregion


		#region Utility Methods
		void OnCollisionStay ()
		{
			_isGrounded = true;    
		}


		float CalculateJumpVerticalSpeed ()
		{
			return Mathf.Sqrt (2f * jumpHeight * gravity);
		}
		#endregion
	}
}
