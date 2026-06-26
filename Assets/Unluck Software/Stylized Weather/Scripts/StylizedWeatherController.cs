/*
 * StylizedWeatherController.cs
 * -----------------------------
 * Central manager for all Stylized Weather System effects.
 *
 * PURPOSE:
 *  - Controls activation of weather presets and particle systems.
 *  - Keeps weather effects following the target (e.g., camera/player).
 *  - Optionally affects global fog and wind settings.
 *
 * HOW IT WORKS:
 *  - Detects all StylizedWeatherSettings under particleContainer.
 *  - Allows switching weather by GameObject name.
 *  - Disables inactive weather systems and can fade out old particles.
 *  - Maintains references to all particle systems and updates them in editor.
 *
 * INSPECTOR:
 *  - followObject: The target transform for weather positioning.
 *  - affectGlobalFog: Enables fog adjustments per weather preset.
 *  - defaultWeather: Default preset activated on start.
 *
 * EDITOR:
 *  - Displays quick buttons for switching weather types.
 *  - Auto-fills particle system and wind zone references.
 *
 * USAGE:
 *  - Attach to root GameObject of the weather system prefab.
 *  - Child objects should each have a StylizedWeatherSettings component.
 *
 * AUTHOR: Unluck Software
 */

namespace UnluckSoftware
{
	using UnityEngine;
#if UNITY_EDITOR
	using UnityEditor;
#endif

	[System.Serializable]
	public class StylizedWeatherController :MonoBehaviour
	{
		[Header("Hierarchy")]
		[Tooltip("Parent object containing all WeatherSettings objects.")]
		public GameObject weatherContainer;// internal reference array of all weather settings
										   // container holding all particle system objects
		public GameObject particleContainer;
		[HideInInspector] public StylizedWeatherSettings[] weatherSettings;
		// cached list of all particle systems in the weather setup
		[HideInInspector] public ParticleSystem[] allParticles;
		// reference to global wind zone
		public WindZone windZone;


		[Header("Behavior Settings")]
		[Tooltip("Optional object for the weather controller to follow (e.g., the player).")]
		public Transform followObject;

		[Tooltip("If true, global fog color and density are affected by the active weather.")]
		public bool affectGlobalFog = true;

		[Tooltip("If true, draws helper gizmos in the Scene view.")]
		public bool drawGizmos = true;

		[Header("Default Weather")]
		[Tooltip("The weather preset that should be activated by default when the scene starts.")]
		public GameObject defaultWeather;

		private ParticleSystem.Particle[] m_particles;


		// Enables WeatherSettings based on the gameobject name
		public void ChangeWeather(string name)
		{
			for (int i = 0; i < weatherSettings.Length; i++)
			{
				if (name == weatherSettings[i].gameObject.name)
				{
					weatherSettings[i].gameObject.SetActive(true);
					return;
				}
			}
			Debug.LogWarning(name + " weather setting not found");
		}

		public void DefaultWeather()
		{
			if (!defaultWeather) return;
			defaultWeather.SetActive(true);
		}

		private void Awake()
		{
			//if (!Application.isPlaying) return;
			for (int i = 0; i < allParticles.Length; i++)
			{
				var em = allParticles[i].emission;
				em.rateOverTime = new ParticleSystem.MinMaxCurve(0f, 0f);
			}
			Invoke("DefaultWeather", 0.1f);
		}

		public void DisableAll(StylizedWeatherSettings enabledW, bool quickly = false)
		{
			for (int i = 0; i < weatherSettings.Length; i++)
			{
				if (weatherSettings[i] != enabledW) weatherSettings[i].gameObject.SetActive(false);
			}
			for (int i = 0; i < allParticles.Length; i++)
			{
				var em = allParticles[i].emission;
				if (quickly && em.rateOverTimeMultiplier > 0f)
				{
					m_particles = new ParticleSystem.Particle[allParticles[i].main.maxParticles];
					int numParticlesAlive = allParticles[i].GetParticles(m_particles);
					for (int j = 0; j < numParticlesAlive; j++)
					{
						m_particles[j].remainingLifetime = m_particles[j].remainingLifetime * .5f;
					}
					allParticles[i].SetParticles(m_particles, numParticlesAlive);
				}
				em.rateOverTime = new ParticleSystem.MinMaxCurve(0f, 0f);
			}
		}

#if UNITY_EDITOR
		private int frameCounter = 0;

		void OnDrawGizmos()
		{
			if (!windZone)
			{
				windZone = transform.GetComponentInChildren<WindZone>();
				if (windZone) EditorUtility.SetDirty(this);
			}
			frameCounter++;
			FillWeather();
			FillParticles();
		}
		public void FillWeather()
		{
			int childCount = weatherContainer.transform.childCount;
			if (weatherSettings.Length != childCount)
			{
				weatherSettings = weatherContainer.GetComponentsInChildren<StylizedWeatherSettings>(true);
				//Debug.Log("Updated weather settings array");
				EditorUtility.SetDirty(this);
				return;
			}
			if (frameCounter % 30 != 0) return;
			GameObject selected = Selection.activeGameObject;
			if (selected == null) return;
			if (selected != weatherContainer && selected.transform.parent != weatherContainer.transform) return;

			//Debug.Log("FillWeather");
			for (int i = 0; i < childCount; i++)
			{
				var ws = weatherContainer.transform.GetChild(i).GetComponent<StylizedWeatherSettings>();
				if (weatherSettings[i] != ws)
				{
					weatherSettings = weatherContainer.GetComponentsInChildren<StylizedWeatherSettings>(true);
					//Debug.Log("Updated weather settings array");
					EditorUtility.SetDirty(this);
					return;
				}
			}
		}

		public void FillParticles()
		{
			if (!particleContainer) particleContainer = transform.Find("Particles")?.gameObject;
			if (!particleContainer) return;

			int childCount = particleContainer.transform.childCount;

			if (allParticles == null || allParticles.Length != childCount)
			{
				allParticles = GetDirectChildParticleSystems(particleContainer.transform);
				//Debug.Log("Updated particle systems array");
				EditorUtility.SetDirty(this);
				return;
			}

			if (frameCounter % 30 != 0) return;
			GameObject selected = Selection.activeGameObject;
			if (selected == null) return;
			if (selected != particleContainer && selected.transform.parent != particleContainer.transform) return;

			//Debug.Log("FillParticles");
			for (int i = 0; i < childCount; i++)
			{
				var ps = particleContainer.transform.GetChild(i).GetComponent<ParticleSystem>();
				if (allParticles[i] != ps)
				{
					allParticles = GetDirectChildParticleSystems(particleContainer.transform);
					//Debug.Log("Updated particle systems array");
					EditorUtility.SetDirty(this);
					return;
				}
			}
		}
		public static ParticleSystem[] GetDirectChildParticleSystems(Transform parent)
		{
			if (!parent) return new ParticleSystem[0];

			int childCount = parent.childCount;
			ParticleSystem[] result = new ParticleSystem[childCount];

			for (int i = 0; i < childCount; i++)
			{
				Transform child = parent.GetChild(i);
				result[i] = child.GetComponent<ParticleSystem>();
			}

			return result;
		}
#endif
	}

#if UNITY_EDITOR
	[CustomEditor(typeof(StylizedWeatherController))]
	public class StylizedWeatherControllerEditor :Editor
	{
		StylizedWeatherController tar;

		public override void OnInspectorGUI()
		{
			EditorGUILayout.HelpBox("Gizmos must be enabled in the editor when adding/removing particles or settings.", MessageType.Info);
			DrawDefaultInspector();
			if (!tar) tar = target as StylizedWeatherController;
			EditorGUILayout.Space();
			for (int i = 0; i < tar.weatherSettings.Length; i++)
			{
				if (tar.weatherSettings[i].isActiveAndEnabled)
				{
					GUI.color = Color.green;
				}
				if (GUILayout.Button(tar.weatherSettings[i].gameObject.name))
				{
					tar.ChangeWeather(tar.weatherSettings[i].gameObject.name);
				}
				GUI.color = Color.white;
			}
		}
	}
#endif
}