#pragma warning disable 612, 618
namespace UnluckSoftware
{
	using UnityEngine;
	[ExecuteAlways]
	[System.Serializable]
	public class StylizedWeatherControllerGUI :MonoBehaviour
	{
		[SerializeField] bool disableGUI = false;

		[Header("References")]
		[SerializeField] private StylizedWeatherController controller;
		[SerializeField] private GUIStyle buttonStyle;

		[Header("Layout Settings")]
		[SerializeField] private Vector2 referenceResolution = new Vector2(1920, 1080);
		[SerializeField] private float buttonWidth = 200f;
		[SerializeField] private float buttonHeight = 22f;
		[SerializeField] private float padding = 20f;
		[SerializeField] private float spacing = 4f;
		[SerializeField] private int referenceFontSize = 14;
		[SerializeField] private bool centerVertically = false;

		[Header("Colors")]
		[SerializeField] private Color normalColor = Color.white;
		[SerializeField] private Color activeTint = new Color(0.85f, 0.85f, 0.85f, 1f);
		[SerializeField] private Color hoverTint = new Color(1f, 1f, 1f, 0.8f);
		 
		private string lastWeatherName;

		private void Start()
		{
			if (!controller)
				controller = FindObjectOfType<StylizedWeatherController>();
		}

		private void OnGUI()
		{
			if (disableGUI || controller == null || controller.weatherSettings == null)
				return;

			float scaleX = Screen.width / referenceResolution.x;
			float scaleY = Screen.height / referenceResolution.y;
			float scale = Mathf.Min(scaleX, scaleY);

			float bw = buttonWidth * scale;
			float bh = buttonHeight * scale;
			float pad = padding * scale;
			float space = spacing * scale;

			float totalHeight = controller.weatherSettings.Length * bh + (controller.weatherSettings.Length - 1) * space;
			float startY = centerVertically ? (Screen.height - totalHeight) / 2f : pad;

			for (int i = 0; i < controller.weatherSettings.Length; i++)
			{
				var weather = controller.weatherSettings[i];

				GUIStyle style = new GUIStyle(buttonStyle);
				style.fontSize = Mathf.Max(10, Mathf.RoundToInt(referenceFontSize * scale));
				style.alignment = TextAnchor.MiddleLeft;
				style.padding = new RectOffset(10, 10, 2, 2);
				if(weather == null) continue;
				if (weather.gameObject == null) continue;
				bool isActive = weather.gameObject.name == lastWeatherName;

				if (isActive)
					style.fontStyle = FontStyle.Bold;
				else
					style.fontStyle = FontStyle.Normal;

				Rect buttonRect = new Rect(
					pad,
					startY + i * (bh + space),
					bw,
					bh
				);

				bool isHovering = buttonRect.Contains(Event.current.mousePosition);

				GUI.color = isActive ? activeTint : isHovering ? hoverTint : normalColor;

				string label = $"[ {weather.hotkey} ] {weather.name}";

				if (GUI.Button(buttonRect, label, style))
				{
					controller.ChangeWeather(weather.gameObject.name);
					lastWeatherName = weather.gameObject.name;
				}

				GUI.color = Color.white;
			}
		}

		private void Update()
		{
			if (controller == null || controller.weatherSettings == null)
				return;

			foreach (var weather in controller.weatherSettings)
			{
				
				if(weather !=null && !string.IsNullOrEmpty(weather.hotkey) && Input.GetKeyUp(weather.hotkey))
				{
					controller.ChangeWeather(weather.gameObject.name);
					lastWeatherName = weather.gameObject.name;
					break;
				}
			}
		}
	}
}
