using UnityEditor;
using UnityEngine;

namespace SickscoreGames.HUDNavigationSystem
{
	[CustomEditor(typeof(HNSTextReference))]
	public class HNSTextReferenceEditor : HUDNavigationBaseEditor
	{
		#region Variables

		protected HNSTextReference hudTarget;

		#endregion


		#region Main Methods

		void OnEnable()
		{
			editorTitle = "HNS Text Reference";
			splashTexture = (Texture2D)Resources.Load("Textures/splashTexture_TextReference", typeof(Texture2D));
			showExpandButton = false;

			hudTarget = (HNSTextReference)target;
		}

		protected override void OnBaseInspectorGUI()
		{
			// update serialized object
			serializedObject.Update();

			// get the text adapter 
			var adapter = hudTarget.GetAdapter();

			// cache serialized properties
			SerializedProperty _pTextComponent = serializedObject.FindProperty("_textComponent");

			EditorGUILayout.PropertyField(_pTextComponent, new GUIContent("Text Component", "The text component which will get updated"));
			EditorGUILayout.LabelField("Adapter", adapter?.GetType().Name ?? "<none>");

			if (adapter == null)
			{
				EditorGUILayout.Space();

				if (hudTarget.GetTextComponent() != null)
					EditorGUILayout.HelpBox("Assigned component is not a valid text adapter.", MessageType.Error);
				else
					EditorGUILayout.HelpBox("No supported text component detected.", MessageType.Warning);

				if (!Application.isPlaying)
				{
					if (GUILayout.Button("Refresh"))
					{
						hudTarget.ForceInitialize();
					}
				}
			}

			// apply modified properties
			serializedObject.ApplyModifiedProperties ();
		}

		#endregion
	}
}