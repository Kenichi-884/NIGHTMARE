using SickscoreGames.ExampleScene;
using SickscoreGames.HUDNavigationSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ExampleInteractions : MonoBehaviour
{
	#region Variables
	public LayerMask layerMask = 1 << 0;
	public float interactionDistance = 4f;

	private RaycastHit hit;
	private Transform pickupText;
	private Transform openDoorText;
	private Transform interactionText;
	private HUDNavigationSystem _HUDNavigationSystem;
	#endregion


	#region Main Methods
	void Start ()
	{
		_HUDNavigationSystem = HUDNavigationSystem.Instance;
	}


	void Update ()
	{
		if (_HUDNavigationSystem == null)
			return;

		HandleKeyInput ();

		if (_HUDNavigationSystem.isEnabled)
		{
			HandleItemPickUp ();
			HandleDoorOpening();
			HandlePrismColorChange ();
		}
	}
	#endregion


	#region Utility Methods
	void HandleKeyInput ()
	{
		// update radar zoom / indicator border input
		if (ExampleUniversalInput.GetKey (KeyCode.X) && _HUDNavigationSystem.radarZoom < 5f)
			_HUDNavigationSystem.radarZoom += .0175f;
		else if (ExampleUniversalInput.GetKey (KeyCode.C) && _HUDNavigationSystem.radarZoom > .25f)
			_HUDNavigationSystem.radarZoom -= .0175f;
		else if (ExampleUniversalInput.GetKey (KeyCode.V) && _HUDNavigationSystem.indicatorOffscreenBorder < .7f)
			_HUDNavigationSystem.indicatorOffscreenBorder += .01f;
		else if (ExampleUniversalInput.GetKey (KeyCode.B) && _HUDNavigationSystem.indicatorOffscreenBorder > .07f)
			_HUDNavigationSystem.indicatorOffscreenBorder -= .01f;
		else if (ExampleUniversalInput.GetKey (KeyCode.N) && _HUDNavigationSystem.minimapScale > .06f)
			_HUDNavigationSystem.minimapScale -= .0075f;
		else if (ExampleUniversalInput.GetKey (KeyCode.M) && _HUDNavigationSystem.minimapScale < .35f)
			_HUDNavigationSystem.minimapScale += .0075f;

		// update feature enable / disable input
		if (ExampleUniversalInput.GetKeyDown (KeyCode.H))
			_HUDNavigationSystem.EnableSystem (!_HUDNavigationSystem.isEnabled);
		if (ExampleUniversalInput.GetKeyDown (KeyCode.Alpha1))
			_HUDNavigationSystem.EnableRadar (!_HUDNavigationSystem.useRadar);
		if (ExampleUniversalInput.GetKeyDown (KeyCode.Alpha2))
			_HUDNavigationSystem.EnableCompassBar (!_HUDNavigationSystem.useCompassBar);
		if (ExampleUniversalInput.GetKeyDown (KeyCode.Alpha3))
			_HUDNavigationSystem.EnableIndicators (!_HUDNavigationSystem.useIndicators);
		if (ExampleUniversalInput.GetKeyDown (KeyCode.Alpha4))
			_HUDNavigationSystem.EnableMinimap (!_HUDNavigationSystem.useMinimap);

		// toggle radar / minimap mode
		if (ExampleUniversalInput.GetKeyDown (KeyCode.Alpha5))
			_HUDNavigationSystem.radarMode = (_HUDNavigationSystem.radarMode == RadarModes.RotateRadar) ? RadarModes.RotatePlayer : RadarModes.RotateRadar;
		if (ExampleUniversalInput.GetKeyDown (KeyCode.Alpha6))
			_HUDNavigationSystem.minimapMode = (_HUDNavigationSystem.minimapMode == MinimapModes.RotateMinimap) ? MinimapModes.RotatePlayer : MinimapModes.RotateMinimap;

		// toggle minimap custom layers
		if (ExampleUniversalInput.GetKeyDown (KeyCode.Alpha7) && _HUDNavigationSystem.currentMinimapProfile) {
			GameObject customLayer = _HUDNavigationSystem.currentMinimapProfile.GetCustomLayer ("exampleLayer");
			if (customLayer)
				customLayer.SetActive (!customLayer.activeSelf);
		}
	}


	void HandleItemPickUp ()
	{
		// check for pickup items
		if (Physics.Raycast (transform.position, transform.TransformDirection (Vector3.forward), out hit, interactionDistance, layerMask) && hit.collider.name.Contains ("PickUp")) {
			// get HUD navigation element component
			HUDNavigationElement element = hit.collider.gameObject.GetComponent<HUDNavigationElement> ();
			if (element) {
				// show pickup text
				if (element.Indicator) {
					pickupText = element.Indicator.GetCustomTransform ("pickupText");
					if (pickupText)
						pickupText.gameObject.SetActive (true);
				}

				// wait for interaction input and destroy gameobject
				if (ExampleUniversalInput.GetKeyDown (KeyCode.E))
					Destroy (element.gameObject);
			}
		} else {
			// reset pickup text
			if (pickupText) {
				pickupText.gameObject.SetActive (false);
				pickupText = null;
			}
		}
	}


	void HandleDoorOpening ()
	{
		// check for door
		if (Physics.Raycast (transform.position, transform.TransformDirection (Vector3.forward), out hit, interactionDistance, layerMask) && hit.collider.name.Contains ("Door")) {
			// get HUD navigation element component
			HUDNavigationElement element = hit.collider.gameObject.GetComponent<HUDNavigationElement> ();
			if (element) {
				// show text
				if (element.Indicator) {
					openDoorText = element.Indicator.GetCustomTransform ("openDoorText");
					if (openDoorText)
						openDoorText.gameObject.SetActive (true);
				}

				// wait for input and change scene
				if (ExampleUniversalInput.GetKeyDown (KeyCode.E))
				{
					// toggle SkyIsland / House scene
					if (SceneManager.GetActiveScene().buildIndex == 0)
						SceneManager.LoadScene(1);
					else
						SceneManager.LoadScene(0);
				}
			}
		} else {
			// reset text
			if (openDoorText) {
				openDoorText.gameObject.SetActive (false);
				openDoorText = null;
			}
		}
	}


	void HandlePrismColorChange ()
	{
		// check for colored prisms
		if (Physics.Raycast (transform.position, transform.TransformDirection (Vector3.forward), out hit, interactionDistance, layerMask) && hit.collider.name.Contains ("Prism")) {
			// get HUD navigation element component
			HUDNavigationElement element = hit.collider.gameObject.GetComponentInChildren<HUDNavigationElement> ();
			if (element) {
				// show interaction text
				if (element.Indicator) {
					interactionText = element.Indicator.GetCustomTransform ("interactionText");
					if (interactionText)
						interactionText.gameObject.SetActive (true);
				}

				// wait for interaction input and change prism color
				if (ExampleUniversalInput.GetKeyDown (KeyCode.E)) {
					// generate random color
					Color randomColor = Random.ColorHSV (0f, 1f, 1f, 1f, .5f, 1f);

					// change prism color
					ChangePrismColor (element, randomColor);
				}
			}
		} else {
			// reset interaction text
			if (interactionText) {
				interactionText.gameObject.SetActive (false);
				interactionText = null;
			}
		}
	}


	public void SetInitialPrismColor (HUDNavigationElement element)
	{
		// get renderer from prism
		Renderer prismRenderer = element.transform.parent.GetComponent<Renderer> ();
		if (prismRenderer)
			ChangePrismColor (element, prismRenderer.material.color);
	}


	static void ChangePrismColor (HUDNavigationElement element, Color elementColor)
	{
		// change radar color
		if (element.Radar)
			element.Radar.ChangeIconColor (elementColor);

		// change compass bar color
		if (element.CompassBar)
			element.CompassBar.ChangeIconColor (elementColor);

		// change indicator colors
		if (element.Indicator) {
			element.Indicator.ChangeIconColor (elementColor);
			element.Indicator.ChangeOffscreenIconColor (elementColor);
		}

		// change minimap color
		if (element.Minimap)
			element.Minimap.ChangeIconColor (elementColor);

		// change prism material color
		Renderer prismRenderer = element.transform.parent.GetComponent<Renderer> ();
		if (prismRenderer)
			prismRenderer.material.color = new Color (elementColor.r, elementColor.g, elementColor.b, prismRenderer.material.color.a);

		// change prism light (if present)
		Light prismLight = element.transform.parent.gameObject.GetComponentInChildren<Light> ();
		if (prismLight)
			prismLight.color = new Color (elementColor.r, elementColor.g, elementColor.b);
	}
	#endregion
}
