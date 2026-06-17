using UnityEngine;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SickscoreGames.ExampleScene
{
	public static class ExampleUniversalInput
	{
		static Dictionary<string, float> axisState = new Dictionary<string, float>();

		public static float GetAxis(string axis)
		{
#if ENABLE_INPUT_SYSTEM
			float target = GetAxisRawNew(axis);
#else
			float target = Input.GetAxisRaw(axis);
#endif
			float current = axisState.TryGetValue(axis, out var v) ? v : 0f;
			current = Mathf.MoveTowards(current, target, 3f * Time.deltaTime);
			axisState[axis] = current;

			return current;
		}

		public static bool GetKey(KeyCode key)
		{
#if ENABLE_INPUT_SYSTEM
			return GetKeyNew(key);
#else
			return Input.GetKey(key);
#endif
		}

		public static bool GetKeyDown(KeyCode key)
		{
#if ENABLE_INPUT_SYSTEM
			return GetKeyNew(key, true);
#else
			return Input.GetKeyDown(key);
#endif
		}

#if ENABLE_INPUT_SYSTEM
		static bool GetKeyNew(KeyCode key, bool downOnly = false)
		{
			var kb = Keyboard.current;
			if (kb == null) return false;

			return key switch
			{
				KeyCode.Space => downOnly ? kb.spaceKey.wasPressedThisFrame : kb.spaceKey.isPressed,
				KeyCode.LeftShift => downOnly ? kb.leftShiftKey.wasPressedThisFrame : kb.leftShiftKey.isPressed,
				KeyCode.X => downOnly ? kb.xKey.wasPressedThisFrame : kb.xKey.isPressed,
				KeyCode.C => downOnly ? kb.cKey.wasPressedThisFrame : kb.cKey.isPressed,
				KeyCode.V => downOnly ? kb.vKey.wasPressedThisFrame : kb.vKey.isPressed,
				KeyCode.B => downOnly ? kb.bKey.wasPressedThisFrame : kb.bKey.isPressed,
				KeyCode.N => downOnly ? kb.nKey.wasPressedThisFrame : kb.nKey.isPressed,
				KeyCode.M => downOnly ? kb.mKey.wasPressedThisFrame : kb.mKey.isPressed,
				KeyCode.H => downOnly ? kb.hKey.wasPressedThisFrame : kb.hKey.isPressed,
				KeyCode.Alpha1 => downOnly ? kb.digit1Key.wasPressedThisFrame : kb.digit1Key.isPressed,
				KeyCode.Alpha2 => downOnly ? kb.digit2Key.wasPressedThisFrame : kb.digit2Key.isPressed,
				KeyCode.Alpha3 => downOnly ? kb.digit3Key.wasPressedThisFrame : kb.digit3Key.isPressed,
				KeyCode.Alpha4 => downOnly ? kb.digit4Key.wasPressedThisFrame : kb.digit4Key.isPressed,
				KeyCode.Alpha5 => downOnly ? kb.digit5Key.wasPressedThisFrame : kb.digit5Key.isPressed,
				KeyCode.Alpha6 => downOnly ? kb.digit6Key.wasPressedThisFrame : kb.digit6Key.isPressed,
				KeyCode.Alpha7 => downOnly ? kb.digit7Key.wasPressedThisFrame : kb.digit7Key.isPressed,
				KeyCode.E => downOnly ? kb.eKey.wasPressedThisFrame : kb.eKey.isPressed,
				_ => false
			};
		}

		static float GetAxisRawNew(string axis)
		{
			var k = Keyboard.current;
			if (k == null) return 0f;

			switch (axis)
			{
				case "Horizontal":
					return (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0) -
					       (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0);

				case "Vertical":
					return (k.wKey.isPressed || k.upArrowKey.isPressed ? 1 : 0) -
					       (k.sKey.isPressed || k.downArrowKey.isPressed ? 1 : 0);
			}

			return 0f;
		}
#endif
	}
}
