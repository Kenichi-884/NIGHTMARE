/*
 * StylizedWeatherSettings.cs
 * ---------------------------
 * Defines a single weather preset in the Stylized Weather System.
 *
 * PURPOSE:
 *  - Stores and applies visual/particle settings for one weather type.
 *  - Updates emission, speed, size, lifetime, color, and force curves.
 *  - Optionally adjusts global fog color and density.
 *  - Syncs wind power with the controller’s WindZone.
 *
 * HOW IT WORKS:
 *  - On enable, disables other presets via StylizedWeatherController.
 *  - Updates all assigned particle systems based on the Element array.
 *  - Smoothly transitions fog to target values (if enabled).
 *  - Repeats updates every 0.5s to keep systems synchronized.
 *
 * STRUCT: Element
 *  - Holds all settings for a single ParticleSystem (size, color, speed, etc.).
 *
 * EDITOR:
 *  - Auto-fills element names.
 *  - Warns about duplicate ParticleSystems.
 *  - Validates collision setup for non-collision systems.
 *
 * USAGE:
 *  - Attach to each weather-type child under StylizedWeatherController.
 *  - Define particle behaviors, fog values, and wind settings per preset.
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
	public struct Element
	{
		[HideInInspector][SerializeField] public string name; // Internal display name
		[SerializeField] public ParticleSystem particleSystem; // Reference to particle system
		[SerializeField] public ParticleSystem.MinMaxCurve size; // Start size
		[SerializeField] public ParticleSystem.MinMaxGradient color; // Start color
		[SerializeField] public ParticleSystem.MinMaxCurve lifetime; // Particle lifetime
		[SerializeField] public ParticleSystem.MinMaxCurve emission; // Emission rate
		[SerializeField] public ParticleSystem.MinMaxCurve speed; // Start speed
		[SerializeField] public ParticleSystem.MinMaxCurve externalForce; // External force curve
		[SerializeField] public bool enableCollision; // Collision toggle
	}

	[ExecuteAlways]
	public class StylizedWeatherSettings :MonoBehaviour
	{
		public float windPower = 1f; // Wind zone strength
		public float globalFogDensity; // Target fog density
		public Color globalFogColor = Color.green; // Target fog color
		public bool quickDisable; // If true, disables old weather quickly
		public string hotkey; // Optional activation hotkey
		public Element[] elements; // All particle modifiers in this preset

		private StylizedWeatherController controller; // Cached controller reference
		private bool isUpdatingFog; // Fog updating state

		void OnEnable()
		{
			if (!controller) controller = transform.parent.parent.GetComponent<StylizedWeatherController>();
			controller.DisableAll(this, quickDisable);
			if (controller.windZone) controller.windZone.windMain = windPower;
#if UNITY_EDITOR
			FixNewValues();
#endif
			UpdateSystems();
			if (controller.affectGlobalFog) StartUpdatingGlobalFog();
			if (globalFogColor == Color.green) globalFogColor = RenderSettings.fogColor;
		}

#if UNITY_EDITOR
		void FixNewValues()
		{
			for (int i = 0; i < elements.Length; i++)
			{
				if (elements[i].particleSystem == null) continue;
				if (!elements[i].particleSystem.gameObject.name.Contains("-C"))
				{
					if (elements[i].enableCollision == true) Debug.LogWarning(elements[i].particleSystem.gameObject + "not set up for collisions");
					elements[i].enableCollision = false;
				}
			}
		}
#endif

		public void StartUpdatingGlobalFog() { InvokeRepeating(nameof(UpdateGlobalFog), 0f, 0.1f); }

		private void UpdateGlobalFog()
		{
			RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, globalFogDensity, 0.01f);
			RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, globalFogColor, 0.01f);
			if (Mathf.Abs(RenderSettings.fogDensity - globalFogDensity) < 0.001f && Vector4.Distance(RenderSettings.fogColor, globalFogColor) < 0.01f)
			{
				RenderSettings.fogDensity = globalFogDensity;
				RenderSettings.fogColor = globalFogColor;
				CancelInvoke(nameof(UpdateGlobalFog));
			}
		}

		private void OnDisable() { CancelInvoke(); }

		void UpdateSystems()
		{
			Invoke("UpdateSystems", 0.5f);
			for (int i = 0; i < elements.Length; i++)
			{
				if (elements[i].particleSystem == null) continue;
				var ps = elements[i].particleSystem;
				var emission = ps.emission; emission.rateOverTime = elements[i].emission;
				var main = ps.main;
				main.startLifetime = elements[i].lifetime;
				main.startSpeed = elements[i].speed;
				main.startColor = elements[i].color;
				main.startSize = elements[i].size;
				var collision = ps.collision; collision.enabled = elements[i].enableCollision;
				var force = ps.externalForces; force.multiplierCurve = elements[i].externalForce;
			}
		}
	}

#if UNITY_EDITOR
	[CustomEditor(typeof(StylizedWeatherSettings))]
	public class StylizedWeatherSettingsEditor :Editor
	{
		StylizedWeatherSettings tar;

		public override void OnInspectorGUI()
		{
			DrawDefaultInspector();
			CheckForDuplicateParticleSystems();
			FillName();
		}

		private void FillName()
		{
			if (!tar) tar = target as StylizedWeatherSettings;
			for (int i = 0; i < tar.elements.Length; i++)
				if (tar.elements[i].particleSystem && tar.elements[i].name != tar.elements[i].particleSystem.name)
					tar.elements[i].name = tar.elements[i].particleSystem.name;
		}

		private void CheckForDuplicateParticleSystems()
		{
			if (!tar) tar = target as StylizedWeatherSettings;
			for (int i = 0; i < tar.elements.Length; i++)
			{
				var psA = tar.elements[i].particleSystem;
				if (psA == null) continue;
				for (int j = i + 1; j < tar.elements.Length; j++)
				{
					var psB = tar.elements[j].particleSystem;
					if (psB == null) continue;
					if (psA == psB)
					{
						EditorGUILayout.HelpBox("Duplicate ParticleSystems: " + psA.name + "\nOnly one reference allowed.", MessageType.Error);
						i++; j++; continue;
					}
				}
			}
		}
	}
#endif
}
