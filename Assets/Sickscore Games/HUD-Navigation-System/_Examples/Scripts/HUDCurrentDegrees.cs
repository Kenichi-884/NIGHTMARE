using UnityEngine;

namespace SickscoreGames.HUDNavigationSystem
{
	[RequireComponent(typeof(HNSTextReference))]
	public class HUDCurrentDegrees : MonoBehaviour
	{
		#region Variables
		protected HNSTextReference textRef;
		#endregion


		#region Main Methods
		void Awake ()
		{
			textRef = GetComponent<HNSTextReference> ();
		}

		void Update ()
		{
			if (textRef)
				textRef.SetText(((int)HUDNavigationCanvas.Instance.CompassBarCurrentDegrees).ToString ());
		}
		#endregion
	}
}
