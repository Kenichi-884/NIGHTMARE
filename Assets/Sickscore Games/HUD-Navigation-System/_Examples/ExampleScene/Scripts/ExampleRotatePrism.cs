using SickscoreGames.HUDNavigationSystem;
using UnityEngine;

namespace SickscoreGames.ExampleScene
{
	public class ExampleRotatePrism : MonoBehaviour
	{
		#region Variables
		[Range(0f, 100f)]
		public float rotationSpeed = 75f;
		public Color32 initialColor = new Color(1f, .8f, 0f, .95f);
		#endregion


		#region Main Methods
		void Update ()
		{
			// rotate prism
			if (rotationSpeed > 0f)
				transform.Rotate (0f, rotationSpeed * Time.deltaTime, 0f);
		}
		#endregion

		#region Helper Methods
		public void SetInitialPrismColor (HUDNavigationElement element)
		{
			// change radar color
			if (element.Radar != null)
				element.Radar.ChangeIconColor(initialColor);

			// change compass bar color
			if (element.CompassBar != null)
				element.CompassBar.ChangeIconColor(initialColor);

			// change indicator colors
			if (element.Indicator != null)
			{
				element.Indicator.ChangeIconColor(initialColor);
				element.Indicator.ChangeOffscreenIconColor(initialColor);
			}

			// change minimap color
			if (element.Minimap != null)
				element.Minimap.ChangeIconColor(initialColor);

			// change prism material color
			Renderer prismRenderer = this.GetComponent<Renderer>();
			if (prismRenderer != null)
				prismRenderer.material.color = initialColor;
		}
		#endregion
	}
}
