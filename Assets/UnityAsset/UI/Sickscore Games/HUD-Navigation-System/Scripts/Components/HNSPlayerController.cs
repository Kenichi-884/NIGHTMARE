using UnityEngine;
#if UNITY_EDITOR
#endif

namespace SickscoreGames.HUDNavigationSystem
{
	[AddComponentMenu (HNS.Name + "/HNS Player Controller")]
	public class HNSPlayerController : MonoBehaviour
	{
		#region Variables
		#endregion


		#region Main Methods
		void Start ()
		{
			if (HUDNavigationSystem.Instance != null)
				HUDNavigationSystem.Instance.ChangePlayerController (this.gameObject.transform);
		}
		#endregion


		#region Utility Methods
		#endregion
	}


	#region Subclasses
	#endregion
}
