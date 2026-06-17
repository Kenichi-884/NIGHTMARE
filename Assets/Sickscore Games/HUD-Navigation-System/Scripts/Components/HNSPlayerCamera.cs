using UnityEngine;
#if UNITY_EDITOR
#endif

namespace SickscoreGames.HUDNavigationSystem
{
	[AddComponentMenu (HNS.Name + "/HNS Player Camera")]
	public class HNSPlayerCamera : MonoBehaviour
	{
		#region Variables
		#endregion


		#region Main Methods
		void Start ()
		{
			if (HUDNavigationSystem.Instance != null) {
				Camera camera = gameObject.GetComponent<Camera> ();
				HUDNavigationSystem.Instance.ChangePlayerCamera (camera);
			}
		}
		#endregion


		#region Utility Methods
		#endregion
	}


	#region Subclasses
	#endregion
}
