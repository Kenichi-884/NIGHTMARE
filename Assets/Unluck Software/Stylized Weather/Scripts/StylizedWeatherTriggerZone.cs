/*
 * StylizedWeatherTriggerZone.cs
 * -------------------------------
 * Triggers weather presets when a player enters or exits a BoxCollider zone.
 *
 * PURPOSE:
 *  - Activates a defined "enter" weather preset on trigger enter.
 *  - Activates an optional "exit" weather preset on trigger exit.
 *  - Works with StylizedWeatherController to manage active weather.
 *
 * HOW IT WORKS:
 *  - Uses a BoxCollider set as a trigger.
 *  - Checks for collisions with a specific player tag.
 *  - Can be set as reusable for multiple activations.
 *
 * EDITOR:
 *  - Draws a semi-transparent cube in Scene view to visualize the trigger.
 *  - Displays a label showing the enter and exit weather names.
 *
 * USAGE:
 *  - Attach to a GameObject with a BoxCollider.
 *  - Define weather presets to trigger on enter and exit.
 *
 * AUTHOR: Unluck Software
 */
#pragma warning disable 612, 618
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnluckSoftware
{
	[RequireComponent(typeof(BoxCollider))]
	public class StylizedWeatherTriggerZone :MonoBehaviour
	{
		[Header("Weather Settings")]
		[Tooltip("The name of the weather preset GameObject to activate on enter.")]
		public string enterWeatherName;

		[Tooltip("The name of the weather preset GameObject to activate on exit.")]
		public string exitWeatherName;

		[Tooltip("Tag used to detect the player.")]
		public string playerTag = "Player";

		[Tooltip("If true, the trigger can be used multiple times.")]
		public bool reusable = true;

		[Header("Gizmo Settings")]
		public bool showGizmos = true; // toggle in inspector

		private bool _hasTriggered;
		private StylizedWeatherController _controller;
		private BoxCollider _collider;

		private void Reset()
		{
			BoxCollider col = GetComponent<BoxCollider>();
			col.isTrigger = true;
		}

		private void Start()
		{
			_collider = GetComponent<BoxCollider>();
			_controller = FindObjectOfType<StylizedWeatherController>();
		}

		private void OnTriggerEnter(Collider other)
		{
			if (_hasTriggered && !reusable) return;
			if (!string.IsNullOrEmpty(playerTag) && !other.CompareTag(playerTag)) return;
			if (_controller == null || string.IsNullOrEmpty(enterWeatherName)) return;

			_controller.ChangeWeather(enterWeatherName);
			_hasTriggered = true;
		}

		private void OnTriggerExit(Collider other)
		{
			if (!reusable && !_hasTriggered) return;
			if (!string.IsNullOrEmpty(playerTag) && !other.CompareTag(playerTag)) return;
			if (_controller == null || string.IsNullOrEmpty(exitWeatherName)) return;

			_controller.ChangeWeather(exitWeatherName);
			_hasTriggered = false; // allow re-entry
		}

#if UNITY_EDITOR
		private void OnDrawGizmos()
		{
			if (!showGizmos) return;

			BoxCollider col = GetComponent<BoxCollider>();
			if (col == null) return;

			Matrix4x4 cubeTransform = Matrix4x4.TRS(transform.position + col.center, transform.rotation, transform.lossyScale);
			Gizmos.matrix = cubeTransform;

			// Draw solid and wire cube
			Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.2f);
			Gizmos.DrawCube(Vector3.zero, col.size);
			Gizmos.color = new Color(0.3f, 0.6f, 1f, 1f);
			Gizmos.DrawWireCube(Vector3.zero, col.size);

			// Draw label above the box
			Vector3 labelPos = transform.position + transform.rotation * (col.center + Vector3.up * (col.size.y * 0.6f));
			string labelText = string.IsNullOrEmpty(enterWeatherName) ? "(No Enter Weather)" : enterWeatherName;
			if (!string.IsNullOrEmpty(exitWeatherName))
				labelText += $" → Exit: {exitWeatherName}";

			Handles.Label(labelPos, $"☁ {labelText}");
		}
#endif
	}
}