using UnityEditor;
using UnityEngine;

namespace EdgeFusion {
    
    [CustomEditor(typeof(EdgeFusionObject))]
    public class EdgeFusionObjectEditor : Editor {
        SerializedProperty overrideRadius;
        SerializedProperty customRadius;
        SerializedProperty overrideObjectId;
        SerializedProperty customObjectId;
        SerializedProperty useRandomId;
        SerializedProperty randomIdPerChild;
        SerializedProperty disallowIntraObjectFusion;
        SerializedProperty includeMode;
        SerializedProperty childLayerMask;

        void OnEnable () {
            overrideRadius = serializedObject.FindProperty("overrideRadius");
            customRadius = serializedObject.FindProperty("customRadius");
            overrideObjectId = serializedObject.FindProperty("overrideObjectId");
            customObjectId = serializedObject.FindProperty("customObjectId");
            useRandomId = serializedObject.FindProperty("useRandomId");
            randomIdPerChild = serializedObject.FindProperty("randomIdPerChild");
            disallowIntraObjectFusion = serializedObject.FindProperty("disallowIntraObjectFusion");
            includeMode = serializedObject.FindProperty("includeMode");
            childLayerMask = serializedObject.FindProperty("childLayerMask");
            EdgeFusionObject edgeFusionObject = (EdgeFusionObject)target;
            edgeFusionObject.Refresh();
        }

        public override void OnInspectorGUI () {
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Optional Override", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(overrideObjectId, new GUIContent("Override Object ID"));
            if (overrideObjectId.boolValue) {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(useRandomId, new GUIContent("Use Random ID", "Use a random Object ID, instead of an Id derived from object position"));
                if (useRandomId.boolValue && includeMode.intValue == (int)IncludeMode.IncludeChildren) {
                    EditorGUILayout.PropertyField(randomIdPerChild, new GUIContent("Random Id Per Child", "Assign a different random ID to each child renderer"));
                }
                if (!useRandomId.boolValue) {
                    EditorGUILayout.PropertyField(customObjectId, new GUIContent("Custom Object ID", "0 = auto. >0 overrides the auto-generated ID. Objects sharing the same ID are considered the same for inter-object detection."));
                    if (customObjectId.intValue <= 0) {
                        EditorGUILayout.HelpBox("Custom Object ID must be greater than 0.", MessageType.Warning);
                    }
                }
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.PropertyField(overrideRadius, new GUIContent("Override Radius"));
            if (overrideRadius.boolValue) {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(customRadius, new GUIContent("Custom Radius", "Custom blend radius in meters (0 = disables blending for this object)"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.PropertyField(disallowIntraObjectFusion, new GUIContent("Disallow Intra-Object Fusion", "Disable intra-object fusion for this object/children while keeping inter-object fusion."));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Inclusion", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(includeMode, new GUIContent("Include Mode"));

            if (includeMode.intValue == (int)IncludeMode.IncludeChildren) {
                EditorGUILayout.PropertyField(childLayerMask, new GUIContent("Child Layer Mask"));
            }

            if (!serializedObject.isEditingMultipleObjects) {
                EdgeFusionObject edgeFusionObject = (EdgeFusionObject)target;
                Material invalidMaterial = edgeFusionObject.nonInstancingMaterial;
                if (invalidMaterial != null) {
                    EditorGUILayout.Space();
                    EditorGUILayout.HelpBox($"Material '{invalidMaterial.name}' does not support GPU instancing.", MessageType.Warning);
                    if (GUILayout.Button("Select Material", EditorStyles.linkLabel)) {
                        Selection.activeObject = invalidMaterial;
                        EditorGUIUtility.PingObject(invalidMaterial);
                    }
                }
            } else {
                for (int i = 0; i < targets.Length; i++) {
                    EdgeFusionObject edgeFusionObject = (EdgeFusionObject)targets[i];
                    if (edgeFusionObject.nonInstancingMaterial != null) {
                        EditorGUILayout.Space();
                        EditorGUILayout.HelpBox("Some selected objects use materials that do not support GPU instancing.", MessageType.Warning);
                        break;
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
