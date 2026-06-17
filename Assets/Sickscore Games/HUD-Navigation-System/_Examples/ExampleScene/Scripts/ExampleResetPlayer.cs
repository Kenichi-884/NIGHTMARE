using UnityEngine;

namespace SickscoreGames.ExampleScene
{
	public class ExampleResetPlayer : MonoBehaviour
	{
		#region Variables
		public Transform spawnPoint;
		#endregion


		#region Main Methods
		void OnTriggerEnter (Collider other)
		{
			if (other.gameObject.tag == "Player") {
				// reset position
				other.gameObject.transform.position = spawnPoint.position;

				// reset velocity
				Rigidbody rBody = other.gameObject.GetComponent<Rigidbody> ();
				if (rBody != null)
				{
#if UNITY_6000_0_OR_NEWER
					rBody.linearVelocity = other.transform.forward * 5f;;
#else
					rBody.velocity = other.transform.forward * 5f;;
#endif
				}
			}
		}
		#endregion
	}
}
