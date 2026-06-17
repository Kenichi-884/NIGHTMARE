#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;

namespace DecalCollider.Editor
{
    /// <summary>
    /// Custom editor for the <see cref="Runtime.DecalCollider"/> component.
    /// Provides a user-friendly interface in the Inspector and custom handles in the Scene view
    /// for intuitive decal projection and collider manipulation.
    /// </summary>
    [CustomEditor(typeof(Runtime.DecalCollider)), CanEditMultipleObjects]
    public class DecalColliderEditor : UnityEditor.Editor
    {
        #region FIELDS & PROPERTIES

        // --- Target & Serialized Objects ---
        private Runtime.DecalCollider m_TargetComponent;
        private SerializedObject m_MeshColliderSo;
        
        // --- Serialized Properties ---
        private SerializedProperty m_DecalModeProp;
        private SerializedProperty m_InputMeshProp;
        private SerializedProperty m_SizeProp;
        private SerializedProperty m_MeshScaleProp;
        private SerializedProperty m_MaxDistanceProp;
        private SerializedProperty m_ProjectionSpaceProp;
        private SerializedProperty m_ProjectionDirectionProp;
        private SerializedProperty m_WrapMaskProp;
        private SerializedProperty m_CenterProp;
        private SerializedProperty m_MeshOffsetProp;
        private SerializedProperty m_RaycastGridExtentProp;
        private SerializedProperty m_MeshSubdivisionsProp;
        private SerializedProperty m_ColliderSubdivisionsProp;
        private SerializedProperty m_ColliderScaleProp;
        private SerializedProperty m_AlphaThresholdProp;
        private SerializedProperty m_AlwaysRebuildProp;
        private SerializedProperty m_IgnoreSelfProp;
        private SerializedProperty m_CullIfInvisibleProp;
        private SerializedProperty m_ShowBoundsGizmosProp;
        private SerializedProperty m_ShowDebugRaysProp;
        private SerializedProperty m_EditorToolEditColliderActiveProp;
        private SerializedProperty m_EditorToolEditProjectionActiveProp;
        
        // --- Live Link Properties ---
        private SerializedProperty m_SourceTMPProp;
        private SerializedProperty m_SourceSpriteRendererProp;
        private SerializedProperty m_LiveUpdateIntervalProp;
        private SerializedProperty m_UpdateColliderOnLiveProp;

        // --- Serialized Properties for MeshCollider (Cached) ---
        private SerializedProperty m_McConvexProp;
        private SerializedProperty m_McIsTriggerProp;
        private SerializedProperty m_McProvidesContactsProp;
        private SerializedProperty m_McCookingOptionsProp;
        private SerializedProperty m_McMaterialProp;
        private SerializedProperty m_McLayerOverridePriorityProp;
        private SerializedProperty m_McIncludeLayersProp;
        private SerializedProperty m_McExcludeLayersProp;

        // --- Editor State ---
        private Tool m_LastTool;
        private bool m_LastCustomActive;

        // --- Foldout States ---
        private bool m_FoldProjectionSettings = true;
        private bool m_FoldMeshColliderGeneration = true;
        private bool m_FoldAlphaMasking = true;
        private bool m_FoldColliderPhysicsSettings = true;
        private bool m_FoldLayerOverrides = true;
        private bool m_FoldRayHitObjects;
        private bool m_FoldInfo;
        private bool m_FoldLiveLink = true; 
        
        // --- LOD ---
        private SerializedProperty m_UseDynamicLODProp;     
        private SerializedProperty m_LodDistanceProp;       
        private SerializedProperty m_LodCheckIntervalProp;
        
        // --- GUI Contents ---
        private static GUIContent _sEditColliderContent;
        private static GUIContent _sEditProjectionContent;
        private static GUIContent _sBoundsToolContent;
        private static GUIContent _sDebugRaysContent;
        private static GUIContent _sRefreshContent;
        
        // --- GUI Styles ---
        private static GUIStyle _sLeftButtonStyle;
        private static GUIStyle _sMidButtonStyle;
        private static GUIStyle _sRightButtonStyle;

        // --- Scene Handle Hashes ---
        private static readonly int SDecalArrowControlHash = "DecalColliderYellowArrowHash".GetHashCode();
        private static readonly int SMaxDistanceHandleHash = "DecalMaxDistanceHandleHash".GetHashCode();
        private static readonly int SRaycastExtentHandleHash = "DecalRaycastExtentHandleHash".GetHashCode();
        
        #endregion

        #region UNITY MESSAGES

        /// <summary>
        /// Initializes the GUI Styles if they haven't been created yet.
        /// </summary>
        private static void InitializeStyles()
        {
            if (_sLeftButtonStyle != null) return;

            _sLeftButtonStyle = new GUIStyle(EditorStyles.miniButtonLeft);
            _sMidButtonStyle = new GUIStyle(EditorStyles.miniButtonMid);
            _sRightButtonStyle = new GUIStyle(EditorStyles.miniButtonRight);
        }

        /// <summary>
        /// Called when the editor is enabled. Initializes properties and sets up hooks.
        /// </summary>
        private void OnEnable()
        {
            InitializeTargetComponent();
            FindSerializedProperties();
            InitializeGUIContent();
            SetIconForScript();
            InitializeMeshCollider();
            ScheduleRebuild();
        }

        /// <summary>
        /// Called when the editor is disabled. Cleans up tool states.
        /// </summary>
        private void OnDisable()
        {
            if (m_TargetComponent == null || serializedObject.targetObject != m_TargetComponent || Application.isPlaying)
                return;
                
            ResetSceneToolStates();
        }

        /// <summary>
        /// Draws the custom inspector GUI.
        /// </summary>
        public override void OnInspectorGUI()
        {
            InitializeTargetIfInvalid();
            if (CheckPropertiesAreNull())
            {
                OnEnable();
                Repaint();
                return;
            }

            serializedObject.Update();
            
            DrawTopToolbar();
            DrawFoldoutGroups();

            if (serializedObject.ApplyModifiedProperties())
            {
                // Only rebuild if the user is not actively dragging a slider (hotControl == 0)
                if (GUIUtility.hotControl == 0)
                {
                    if (m_TargetComponent != null && (m_TargetComponent.alwaysRebuild || !Application.isPlaying))
                    {
                        if (m_TargetComponent.gameObject.activeInHierarchy && m_TargetComponent.enabled)
                        {
                            m_TargetComponent.RebuildSafe();
                        }
                    }
                }
                SceneView.RepaintAll();
            }
        }

        /// <summary>
        /// Draws custom handles and gizmos in the Scene view.
        /// </summary>
        private void OnSceneGUI()
        {
            if (m_TargetComponent == null || !m_TargetComponent.enabled || !m_TargetComponent.gameObject.activeInHierarchy)
                return;

            HandleToolSelection();
            
            Transform tf = m_TargetComponent.transform;
            Vector3 projDir = m_TargetComponent.GetProjectionVector(tf).normalized;
            if (projDir.sqrMagnitude < Runtime.DecalCollider.KEpsilon * Runtime.DecalCollider.KEpsilon)
                projDir = -tf.up;

            Vector3 wsCenter = tf.TransformPoint(m_TargetComponent.center);
            Vector3 up = GetOrthogonalUpVector(tf, projDir);
            Quaternion gizmoRot = Quaternion.LookRotation(projDir, up);
            
            if (m_TargetComponent.editorToolEditColliderActive) HandleMaxDistanceSlider(wsCenter, projDir, gizmoRot);
            if (m_TargetComponent.editorToolEditProjectionActive) HandleRotation(wsCenter, gizmoRot);
            if (m_TargetComponent.showBoundsGizmos) HandleRaycastExtent(tf, wsCenter, gizmoRot);

            DrawProjectionArrow(wsCenter, projDir, gizmoRot);

            m_LastTool = Tools.current;
            m_LastCustomActive = IsAnyCustomToolActive();
        }
        
        #endregion

        #region INSPECTOR GUI DRAWING

        /// <summary>
        /// Draws the main toolbar with icon buttons for activating scene tools.
        /// </summary>
        private void DrawTopToolbar()
        {
            InitializeStyles();
            
            const float btnHeight = 24f;
            _sLeftButtonStyle.fixedHeight = btnHeight;
            _sMidButtonStyle.fixedHeight = btnHeight;
            _sRightButtonStyle.fixedHeight = btnHeight;
            var widthOption = GUILayout.Width(38);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            bool repaintNeeded = false;
            bool currentEditCollider = m_EditorToolEditColliderActiveProp.boolValue;
            bool currentEditProjection = m_EditorToolEditProjectionActiveProp.boolValue;
            bool currentBounds = m_ShowBoundsGizmosProp.boolValue;
            bool currentRays = m_ShowDebugRaysProp.boolValue;

            Color originalBgColor = new Color(0.75f, 0.75f, 0.75f);
            Color selectedButtonColor = Color.white; 
            
            // Edit Collider Button
            GUI.backgroundColor = currentEditCollider ? selectedButtonColor : originalBgColor;
            if (GUILayout.Button(_sEditColliderContent, _sLeftButtonStyle, widthOption))
            {
                bool newState = !currentEditCollider;
                m_EditorToolEditColliderActiveProp.boolValue = newState;
                if (newState)
                {
                    m_EditorToolEditProjectionActiveProp.boolValue = false;
                    m_ShowBoundsGizmosProp.boolValue = false;
                }
                repaintNeeded = true;
            }

            // Edit Projection Button
            GUI.backgroundColor = currentEditProjection ? selectedButtonColor : originalBgColor;
            if (GUILayout.Button(_sEditProjectionContent, _sMidButtonStyle, widthOption))
            {
                bool newState = !currentEditProjection;
                m_EditorToolEditProjectionActiveProp.boolValue = newState;
                if (newState)
                {
                    m_EditorToolEditColliderActiveProp.boolValue = false;
                    m_ShowBoundsGizmosProp.boolValue = false;
                }
                repaintNeeded = true;
            }
    
            // Edit Bounds Button
            GUI.backgroundColor = currentBounds ? selectedButtonColor : originalBgColor;
            if (GUILayout.Button(_sBoundsToolContent, _sMidButtonStyle, widthOption))
            {
                bool newState = !currentBounds;
                m_ShowBoundsGizmosProp.boolValue = newState;
                if (newState)
                {
                    m_EditorToolEditColliderActiveProp.boolValue = false;
                    m_EditorToolEditProjectionActiveProp.boolValue = false;
                }
                repaintNeeded = true;
            }
    
            // Debug Rays Button
            GUI.backgroundColor = currentRays ? selectedButtonColor : originalBgColor;
            if (GUILayout.Button(_sDebugRaysContent, _sMidButtonStyle, widthOption))
            {
                m_ShowDebugRaysProp.boolValue = !currentRays;
                repaintNeeded = true;
            }

            GUI.backgroundColor = originalBgColor; 
            
            // Refresh Button
            if (GUILayout.Button(_sRefreshContent, _sRightButtonStyle, widthOption))
            {
                foreach (var t in targets)
                    if (t is Runtime.DecalCollider dc)
                        dc.RebuildSafe();
                repaintNeeded = true;
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (repaintNeeded)
            {
                if (m_TargetComponent != null)
                    EditorUtility.SetDirty(m_TargetComponent);
                SceneView.RepaintAll();
            }
            EditorGUILayout.Space();
            GUI.backgroundColor = Color.white;
        }

        /// <summary>
        /// Draws the main foldout groups in the inspector.
        /// </summary>
        private void DrawFoldoutGroups()
        {
            m_FoldProjectionSettings = EditorGUILayout.BeginFoldoutHeaderGroup(m_FoldProjectionSettings, "Projection Settings");
            if (m_FoldProjectionSettings) DrawProjectionSettingsGUI();
            EditorGUILayout.EndFoldoutHeaderGroup();

            m_FoldMeshColliderGeneration = EditorGUILayout.BeginFoldoutHeaderGroup(m_FoldMeshColliderGeneration, "Mesh & Collider Generation");
            if (m_FoldMeshColliderGeneration) DrawMeshGenerationGUI();
            EditorGUILayout.EndFoldoutHeaderGroup();

            bool hasSource = m_SourceTMPProp.objectReferenceValue != null || 
                             m_SourceSpriteRendererProp.objectReferenceValue != null;

            if (m_TargetComponent.decalMode == Runtime.DecalMode.MeshProjection && hasSource)
            {
                m_FoldLiveLink = EditorGUILayout.BeginFoldoutHeaderGroup(m_FoldLiveLink, "Live Link (TMP/Sprite Updates)");
                if (m_FoldLiveLink) DrawLiveLinkGUI();
                EditorGUILayout.EndFoldoutHeaderGroup();
            }
            
            m_FoldAlphaMasking = EditorGUILayout.BeginFoldoutHeaderGroup(m_FoldAlphaMasking, "Alpha Masking");
            if (m_FoldAlphaMasking) DrawAlphaMaskingGUI();
            EditorGUILayout.EndFoldoutHeaderGroup();

            m_FoldColliderPhysicsSettings = EditorGUILayout.BeginFoldoutHeaderGroup(m_FoldColliderPhysicsSettings, "Physics Settings");
            if (m_FoldColliderPhysicsSettings) DrawPhysicsSettingsGUI();
            EditorGUILayout.EndFoldoutHeaderGroup();
            
            m_FoldInfo = EditorGUILayout.BeginFoldoutHeaderGroup(m_FoldInfo, "Info");
            if (m_FoldInfo) DrawInfoGUI();
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        /// <summary>
        /// Draws properties related to the projection volume and direction.
        /// </summary>
        private void DrawProjectionSettingsGUI()
        {
            EditorGUILayout.PropertyField(m_WrapMaskProp, new GUIContent("Wrap Mask", "Defines which layers this decal will project onto."));
            EditorGUILayout.PropertyField(m_ProjectionSpaceProp, new GUIContent("Projection Space", "Defines whether the projection is relative to the object's axes (Local) or the world's axes (World)."));
            EditorGUILayout.PropertyField(m_ProjectionDirectionProp, new GUIContent("Projection Direction", "The local axis along which the decal is projected."));
            EditorGUILayout.PropertyField(m_SizeProp, new GUIContent("Size", "The local X/Y size of the projection box."));
            EditorGUILayout.PropertyField(m_MaxDistanceProp, new GUIContent("Max Distance", "The maximum distance the decal will project forward."));
            EditorGUILayout.PropertyField(m_CenterProp, new GUIContent("Center", "The local offset of the projection box's center."));
        }

        /// <summary>
        /// Draws properties related to the mesh generation process.
        /// </summary>
        private void DrawMeshGenerationGUI()
        {
            EditorGUILayout.PropertyField(m_DecalModeProp, new GUIContent("Generation Mode"));

            // --- MESH PROJECTION MODE ---
            if (m_DecalModeProp.intValue == (int)Runtime.DecalMode.MeshProjection)
            {
                EditorGUILayout.PropertyField(m_InputMeshProp, new GUIContent("Input Mesh"));
                EditorGUILayout.PropertyField(m_MeshOffsetProp, new GUIContent("Vertex Offset"));
                EditorGUILayout.PropertyField(m_MeshScaleProp, new GUIContent("Mesh Scale", "Sprite/Mesh size multiplier."));
                
                EditorGUILayout.IntSlider(m_MeshSubdivisionsProp, 1, 128, new GUIContent("Mesh Density", "Target resolution (e.g. 64, 128). The script automatically calculates the required subdivision level based on this value."));
    
                EditorGUILayout.IntSlider(m_ColliderSubdivisionsProp, 1, 64, new GUIContent("Collider Density", "Target resolution for physics mesh."));

                if (m_TargetComponent.inputMesh == null)
                {
                    if (m_TargetComponent.sourceSpriteRenderer == null)
                    {
                        EditorGUILayout.HelpBox("Please assign a Mesh to project.", MessageType.Warning);
                    }
                }
            }
            // --- GRID PROJECTION MODE ---
            else 
            {
                // Warn if in Grid Mode and no MeshRenderer exists
                if (m_TargetComponent.GetComponent<MeshRenderer>() == null)
                {
                    EditorGUILayout.HelpBox("Grid Projection Mode requires a 'Mesh Renderer' component to function correctly (for alpha masking and visualization). Please add one.", MessageType.Warning);
                }

                EditorGUILayout.PropertyField(m_MeshOffsetProp, new GUIContent("Mesh Offset"));
                EditorGUILayout.PropertyField(m_RaycastGridExtentProp, new GUIContent("Raycast Grid Extent"));
                EditorGUILayout.IntSlider(m_MeshSubdivisionsProp, 1, 128, new GUIContent("Mesh Subdivisions", "Grid resolution (e.g. 32, 64, 128)."));
                EditorGUILayout.IntSlider(m_ColliderSubdivisionsProp, 1, 64, new GUIContent("Collider Subdivisions", "Grid resolution (e.g. 16, 32, 64)."));
            }

            // COMMON SETTINGS
            EditorGUILayout.PropertyField(m_ColliderScaleProp, new GUIContent("Collider Scale"));
            EditorGUILayout.PropertyField(m_AlwaysRebuildProp, new GUIContent("Always Rebuild"));
            if (m_AlwaysRebuildProp.boolValue)
            {
                EditorGUI.indentLevel++; 
                EditorGUILayout.PropertyField(m_CullIfInvisibleProp, new GUIContent("Cull If Invisible", "Optimization: Stops rebuilding if the object is not visible to the Main Camera. Highly recommended for performance."));
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(m_UseDynamicLODProp, new GUIContent("Use Dynamic LOD", "Reduces mesh detail based on distance."));
    
            if (m_UseDynamicLODProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(m_LodDistanceProp, new GUIContent("LOD Distance", "Distance at which the mesh switches to lowest quality (Quad)."));
                EditorGUILayout.PropertyField(m_LodCheckIntervalProp, new GUIContent("Check Interval", "How often (in seconds) to check distance."));
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(m_IgnoreSelfProp, new GUIContent("Ignore Self", "ON: Decal does not project onto itself (Self-Hit blocked).\nOFF: Decal can hit its own collider."));
        }

        /// <summary>
        /// Draws properties and warnings related to alpha masking.
        /// </summary>
        private void DrawAlphaMaskingGUI()
        {
            // If in Mesh Projection mode, only show the Threshold slider.
            if (m_TargetComponent.decalMode == Runtime.DecalMode.MeshProjection)
            {
                EditorGUILayout.PropertyField(m_AlphaThresholdProp, new GUIContent("Alpha Threshold", "Cutout sensitivity. (Recommended: 0.1 - 0.5)"));
                return;
            }
            
            // Keep old controls for Grid mode
            EditorGUILayout.PropertyField(m_AlphaThresholdProp, new GUIContent("Alpha Threshold", "Vertices will be removed if their corresponding texture alpha is below this value (0-1). Requires a material with a readable main texture."));
            
            var renderer = m_TargetComponent.GetComponent<MeshRenderer>();
            if (renderer == null || renderer.sharedMaterial == null || renderer.sharedMaterial.mainTexture == null)
            {
                EditorGUILayout.HelpBox("Alpha masking requires a MeshRenderer with a material and a main texture.", MessageType.Info);
            }
            else if (renderer.sharedMaterial.mainTexture is Texture2D { isReadable: false })
            {
                EditorGUILayout.HelpBox("The main texture is not marked as Read/Write enabled in its import settings.", MessageType.Warning);
            }
        }
        
        /// <summary>
        /// Draws live link settings (Active only if Source TMP or Sprite exists).
        /// </summary>
        private void DrawLiveLinkGUI()
        {
            if (m_SourceTMPProp.objectReferenceValue != null)
            {
                EditorGUILayout.HelpBox("Live Link Active: Connected to local TextMeshPro.", MessageType.Info);
            }
            else if (m_SourceSpriteRendererProp.objectReferenceValue != null)
            {
                EditorGUILayout.HelpBox("Live Link Active: Connected to local SpriteRenderer.", MessageType.Info);
            }
            
            EditorGUILayout.PropertyField(m_LiveUpdateIntervalProp, new GUIContent("Update Interval (sec)", "How many times per second to update?"));
            EditorGUILayout.PropertyField(m_UpdateColliderOnLiveProp, new GUIContent("Update Collider", "ON: Physics is also updated (Heavy).\nOFF: Only visuals are updated (Light)."));
            
            if (m_UpdateColliderOnLiveProp.boolValue)
            {
                EditorGUILayout.HelpBox("WARNING: When 'Update Collider' is on, physics meshes are constantly recalculated. FPS may drop with moving text/animations.", MessageType.Warning);
            }
        }
        
        /// <summary>
        /// Draws properties from the attached MeshCollider component directly in this inspector.
        /// </summary>
        private void DrawPhysicsSettingsGUI()
        {
            if (m_MeshColliderSo == null || m_MeshColliderSo.targetObject == null)
            {
                EditorGUILayout.HelpBox("No MeshCollider component is attached. Add one to configure physics properties.", MessageType.Warning);
                return;
            }

            EditorGUI.indentLevel++;
            m_MeshColliderSo.Update();
            
            EditorGUILayout.PropertyField(m_McConvexProp, new GUIContent("Convex"));
            
            EditorGUI.BeginDisabledGroup(!m_McConvexProp.boolValue);
            EditorGUILayout.PropertyField(m_McIsTriggerProp, new GUIContent("Is Trigger", "Toggled with 'Convex' enabled."));
            EditorGUI.EndDisabledGroup();

            if (m_McProvidesContactsProp != null) EditorGUILayout.PropertyField(m_McProvidesContactsProp, new GUIContent("Provides Contacts"));

            if (m_McCookingOptionsProp != null)
            {
                MeshColliderCookingOptions currentOptions = (MeshColliderCookingOptions)m_McCookingOptionsProp.intValue;
                MeshColliderCookingOptions newOptions = (MeshColliderCookingOptions)EditorGUILayout.EnumFlagsField("Cooking Options", currentOptions);
                if (newOptions != currentOptions) m_McCookingOptionsProp.intValue = (int)newOptions;
            }
            
            EditorGUILayout.PropertyField(m_McMaterialProp, new GUIContent("Material", "The physics material for the collider."));
            
            if (m_McLayerOverridePriorityProp != null)
            {
                m_FoldLayerOverrides = EditorGUILayout.Foldout(m_FoldLayerOverrides, "Layer Overrides", true);
                if (m_FoldLayerOverrides)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(m_McLayerOverridePriorityProp, new GUIContent("Layer Override Priority"));
                    EditorGUILayout.PropertyField(m_McIncludeLayersProp, new GUIContent("Include Layers"));
                    EditorGUILayout.PropertyField(m_McExcludeLayersProp, new GUIContent("Exclude Layers"));
                    EditorGUI.indentLevel--;
                }
            }
            
            EditorGUI.indentLevel--;
            m_MeshColliderSo.ApplyModifiedProperties();
        }

        /// <summary>
        /// Draws informational and debug data about the last rebuild in a grid layout.
        /// </summary>
        private void DrawInfoGUI()
        {
            EditorGUI.BeginDisabledGroup(true); 

            var stats = m_TargetComponent.LastRebuildStats;

            EditorGUILayout.BeginHorizontal();
            {
                DrawStatBox("Visual Tris", stats.TrianglesVisual, 2000, 5000, "N0");
                DrawStatBox("Collider Tris", stats.TrianglesCollider, 1000, 2500, "N0");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            {
                DrawStatBox("Rays Hit", stats.RaysHit, 500, 2000, "N0");
                DrawStatBox("Memory (KB)", stats.MemoryKb, 500f, 2000f, "F2");
            }
            EditorGUILayout.EndHorizontal();

            DrawStatBox("Build Time (ms)", (float)stats.BuildTimeMS, 4.0f, 10.0f, "F2");
            EditorGUILayout.Space();

            List<GameObject> hitObjects = m_TargetComponent.GetHitObjects();
            m_FoldRayHitObjects = EditorGUILayout.Foldout(m_FoldRayHitObjects, $"Ray Hit Objects ({hitObjects.Count})", true);

            if (m_FoldRayHitObjects)
            {
                if (hitObjects.Count == 0)
                {
                    EditorGUILayout.HelpBox("No objects were hit in the last rebuild.", MessageType.Info);
                }
                else
                {
                    EditorGUI.indentLevel++;
                    for (int i = 0; i < hitObjects.Count; i++)
                    {
                        EditorGUILayout.ObjectField($"Hit {i + 1}", hitObjects[i], typeof(GameObject), true);
                    }
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUI.EndDisabledGroup();
        }
        
        /// <summary>
        /// Helper method to draw a stat inside a styled box (Grid Cell).
        /// </summary>
        private static void DrawStatBox(string label, float value, float warningThreshold, float criticalThreshold, string format)
        {
            // Determine status color
            bool isCritical = value >= criticalThreshold;
            bool isWarning = value >= warningThreshold;

            GUIStyle valueStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };

            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter
            };

            // Set Color based on performance impact
            if (isCritical)
            {
                valueStyle.normal.textColor = new Color(1f, 0.4f, 0.4f); // Pastel Red
            }
            else if (isWarning)
            {
                valueStyle.normal.textColor = new Color(1f, 0.8f, 0.2f); // Pastel Yellow
            }
            // else: default text color

            // Draw the Box
            GUILayout.BeginVertical(EditorStyles.helpBox);
            {
                GUILayout.Label(label, labelStyle);
                GUILayout.Label(value.ToString(format), valueStyle);
            }
            GUILayout.EndVertical();
        }
        
        #endregion

        #region SCENE GUI HANDLES

        /// <summary>
        /// Manages the state between Unity's built-in transform tools and this component's custom scene tools.
        /// </summary>
        private void HandleToolSelection()
        {
            Tool currentTool = Tools.current;
            bool anyCustomToolActive = IsAnyCustomToolActive();

            if (m_LastCustomActive && !anyCustomToolActive)
            {
                Tools.current = Tool.Move;
                currentTool = Tool.Move;
            }
            
            if (anyCustomToolActive && currentTool != m_LastTool && currentTool != Tool.None)
            {
                Undo.RecordObject(m_TargetComponent, "Disable Custom Tools");
                m_TargetComponent.editorToolEditColliderActive = false;
                m_TargetComponent.editorToolEditProjectionActive = false;
                m_TargetComponent.showBoundsGizmos = false;
                EditorUtility.SetDirty(m_TargetComponent);
            }
            else if (anyCustomToolActive && currentTool != Tool.None)
            {
                Tools.current = Tool.None;
            }
        }
        
        /// <summary>
        /// Draws and handles the logic for the 'Max Distance' slider handle in the scene.
        /// </summary>
        private void HandleMaxDistanceSlider(Vector3 wsCenter, Vector3 projDir, Quaternion gizmoRot)
        {
            float scaledDistance = m_TargetComponent.maxDistance * Mathf.Abs(m_TargetComponent.transform.localScale.z);
            Vector3 faceCenter = wsCenter + gizmoRot * (Vector3.forward * scaledDistance);

            Handles.color = Color.yellow;
            float capSize = HandleUtility.GetHandleSize(faceCenter) * 0.1f;
            int controlId = GUIUtility.GetControlID(SMaxDistanceHandleHash, FocusType.Passive);

            EditorGUI.BeginChangeCheck();
            Vector3 newPos = Handles.Slider(controlId, faceCenter, projDir, capSize, Handles.CubeHandleCap, 0.01f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(m_TargetComponent, "Change Decal Max Distance");

                float rawDist = Vector3.Dot(newPos - wsCenter, projDir);
                float newDist = rawDist / Mathf.Max(Runtime.DecalCollider.KEpsilon, Mathf.Abs(m_TargetComponent.transform.localScale.z));

                m_TargetComponent.maxDistance = Mathf.Max(0.001f, newDist);
                EditorUtility.SetDirty(m_TargetComponent);

                if (GUIUtility.hotControl == 0 && (m_TargetComponent.alwaysRebuild || !Application.isPlaying))
                {
                    if (m_TargetComponent.gameObject.activeInHierarchy && m_TargetComponent.enabled)
                        m_TargetComponent.RebuildSafe();
                }
            }
        }
        
        /// <summary>
        /// Draws and handles the logic for the rotation handle to change projection direction.
        /// </summary>
        private void HandleRotation(Vector3 wsCenter, Quaternion gizmoRot)
        {
            EditorGUI.BeginChangeCheck();
            Quaternion newRot = Handles.RotationHandle(gizmoRot, wsCenter);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(m_TargetComponent, "Change Decal Projection Direction");
                Vector3 dirWs = (newRot * Vector3.forward).normalized;
                if (dirWs.sqrMagnitude < Runtime.DecalCollider.KEpsilon)
                    dirWs = m_TargetComponent.GetProjectionVector(m_TargetComponent.transform);
                
                m_TargetComponent.projectionDirection = SnapToBestProjectionDirection(dirWs, m_TargetComponent.transform);
                EditorUtility.SetDirty(m_TargetComponent);
                if (m_TargetComponent.gameObject.activeInHierarchy && m_TargetComponent.enabled) m_TargetComponent.RebuildSafe();
            }
        }

        private void HandleRaycastExtent(Transform tf, Vector3 wsCenter, Quaternion gizmoRot)
        {
            Vector3 extentOrigin = wsCenter;
            
            // --- DETERMINE HANDLE POSITIONS ---
            Vector2 currentHandleSize;
            
            if (m_TargetComponent.decalMode == Runtime.DecalMode.MeshProjection)
            {
                // In Mesh Mode: Handles sit at Mesh edges (multiplied by MeshScale)
                Vector3 meshBounds = Vector3.one;
                if (m_TargetComponent.inputMesh != null) meshBounds = m_TargetComponent.inputMesh.bounds.size;
                if (meshBounds.x < 0.01f) meshBounds.x = 1f;
                if (meshBounds.y < 0.01f) meshBounds.y = 1f;

                // Handle position = MeshBounds * MeshScale
                currentHandleSize = new Vector2(
                    meshBounds.x * m_TargetComponent.meshScale.x,
                    meshBounds.y * m_TargetComponent.meshScale.y
                );
            }
            else
            {
                // In Grid Mode: Use Extent directly
                currentHandleSize = m_TargetComponent.raycastGridExtent;
            }

            Vector2 halfSize = currentHandleSize * 0.5f;

            float handleSize = HandleUtility.GetHandleSize(extentOrigin) * .2f;
            Vector3 rightWs = gizmoRot * Vector3.right * tf.localScale.x;
            Vector3 upWs = gizmoRot * Vector3.up * tf.localScale.y;
            Vector3 forwardWs = gizmoRot * Vector3.forward;

            float hx = halfSize.x;
            float hy = halfSize.y;

            Vector3[] corners =
            {
                extentOrigin + rightWs * hx + upWs * hy,
                extentOrigin - rightWs * hx + upWs * hy,
                extentOrigin - rightWs * hx - upWs * hy,
                extentOrigin + rightWs * hx - upWs * hy
            };

            Handles.color = new Color(0.2f, 0.5f, 1f, 0.1f);
            Handles.DrawAAConvexPolygon(corners);
            Handles.color = new Color(0.2f, 0.5f, 1f, 1f);
            Handles.DrawPolyLine(corners[0], corners[1], corners[2], corners[3], corners[0]);

            for (int i = 0; i < 4; i++)
            {
                EditorGUI.BeginChangeCheck();
                int cid = GUIUtility.GetControlID(SRaycastExtentHandleHash + i, FocusType.Passive);
                Vector3 newCornerPos = Handles.FreeMoveHandle(cid, corners[i], handleSize, Vector3.zero, Handles.SphereHandleCap);
                
                if (EditorGUI.EndChangeCheck())
                {
                    Plane interactionPlane = new Plane(forwardWs, extentOrigin);
                    newCornerPos = interactionPlane.ClosestPointOnPlane(newCornerPos);

                    Vector3 delta = newCornerPos - extentOrigin;
                    float newHalfX = Mathf.Abs(Vector3.Dot(delta, rightWs.normalized) / Mathf.Max(Runtime.DecalCollider.KEpsilon, tf.localScale.x));
                    float newHalfY = Mathf.Abs(Vector3.Dot(delta, upWs.normalized) / Mathf.Max(Runtime.DecalCollider.KEpsilon, tf.localScale.y));
                    
                    float fullX = newHalfX * 2f;
                    float fullY = newHalfY * 2f;

                    if (m_TargetComponent.decalMode == Runtime.DecalMode.MeshProjection)
                    {
                        Undo.RecordObject(m_TargetComponent, "Scale Decal Mesh");
                        
                        // Calculate Mesh Scale: (New Size) / (Original Mesh Size)
                        Vector3 baseSize = m_TargetComponent.inputMesh != null ? m_TargetComponent.inputMesh.bounds.size : Vector3.one;
                        if (baseSize.x < 0.01f) baseSize.x = 1f;
                        if (baseSize.y < 0.01f) baseSize.y = 1f;

                        m_TargetComponent.meshScale = new Vector2(
                            fullX / baseSize.x,
                            fullY / baseSize.y
                        );
                        
                        // NOTE: We do not touch the 'size' variable! Yellow box remains constant.
                    }
                    else
                    {
                        Undo.RecordObject(m_TargetComponent, "Change Grid Extent");
                        m_TargetComponent.raycastGridExtent = new Vector2(Mathf.Max(0.01f, fullX), Mathf.Max(0.01f, fullY));
                    }

                    EditorUtility.SetDirty(m_TargetComponent);
                    if (m_TargetComponent.gameObject.activeInHierarchy && m_TargetComponent.enabled) m_TargetComponent.RebuildSafe();
                    SceneView.RepaintAll();
                    break; 
                }
            }
        }
        
        /// <summary>
        /// Draws a yellow arrow gizmo to indicate the projection direction when a transform tool is active.
        /// </summary>
        private void DrawProjectionArrow(Vector3 originCenter, Vector3 dirWs, Quaternion gizmoRot)
        {
            if (m_TargetComponent.showBoundsGizmos) return;

            bool isAnyToolActive = IsAnyCustomToolActive();
            bool isTransformToolActive = Tools.current == Tool.Move || Tools.current == Tool.Transform;
            if (!isAnyToolActive && !isTransformToolActive) return;

            int arrowID = GUIUtility.GetControlID(SDecalArrowControlHash, FocusType.Passive);
            
            Vector3 start = isTransformToolActive ? Tools.handlePosition : originCenter;
            
            bool isDraggingOtherObject = Tools.current == Tool.Move && GUIUtility.hotControl != 0 && !Tools.viewToolActive;
            Handles.color = isDraggingOtherObject && HandleUtility.nearestControl != arrowID
                ? new Color(1f, 1f, 0f, 0.25f)
                : Color.yellow;

            float arrowLength = HandleUtility.GetHandleSize(start) * 0.8f;
            Handles.ArrowHandleCap(arrowID, start, gizmoRot, arrowLength, EventType.Repaint);
            
            if (Event.current.type == EventType.Layout)
            {
                HandleUtility.AddControl(arrowID, HandleUtility.DistanceToLine(start, start + dirWs * arrowLength));
            }
        }

        #endregion

        #region INITIALIZATION & UTILITIES
        
        /// <summary>
        /// Initializes static GUIContent objects with icons and tooltips for the toolbar.
        /// </summary>
        private static void InitializeGUIContent()
        {
            _sEditColliderContent = new GUIContent(EditorGUIUtility.IconContent("EditCollider"))
            {
                tooltip = "Edit Max Distance: Adjust the projection depth in the scene."
            };

            _sEditProjectionContent = new GUIContent(EditorGUIUtility.IconContent("d_RotateTool"))
            {
                tooltip = "Edit Projection Direction: Rotate the projection direction in the scene."
            };

            _sBoundsToolContent = new GUIContent(EditorGUIUtility.IconContent("RectTool"))
            {
                tooltip = "Edit Raycast Extent: Adjust the size of the raycasting grid."
            };

            _sDebugRaysContent = new GUIContent(EditorGUIUtility.IconContent("d_PhysicsRaycaster Icon"))
            {
                tooltip = "Toggle Debug Rays: Show or hide the raycasts used for generation."
            };

            _sRefreshContent = new GUIContent(EditorGUIUtility.IconContent("d_Refresh"))
            {
                tooltip = "Rebuild: Manually force the decal mesh and collider to rebuild."
            };
        }
        
        /// <summary>
        /// Snaps a given world-space direction vector to the closest predefined ProjectionDirection enum value.
        /// </summary>
        private static Runtime.ProjectionDirection SnapToBestProjectionDirection(Vector3 worldDirection, Transform referenceTransform)
        {
            var directions = (Runtime.ProjectionDirection[])Enum.GetValues(typeof(Runtime.ProjectionDirection));
            Runtime.ProjectionDirection bestDirection = directions[0];
            float maxDot = -2f;
            worldDirection.Normalize();

            foreach (var enumDir in directions)
            {
                Vector3 baseDir = Runtime.DecalCollider.GetBaseDirectionVector(enumDir);
                Vector3 vectorDir = referenceTransform.TransformDirection(baseDir).normalized;

                float dot = Vector3.Dot(worldDirection, vectorDir);
                if (dot > maxDot)
                {
                    maxDot = dot;
                    bestDirection = enumDir;
                }
            }
            return bestDirection;
        }
        
        /// <summary>
        /// Calculates a stable 'up' vector that is orthogonal to the projection direction.
        /// </summary>
        private static Vector3 GetOrthogonalUpVector(Transform tf, Vector3 projDir)
        {
            Vector3 up = Mathf.Abs(Vector3.Dot(projDir, tf.up)) < 0.999f ? tf.up :
                Mathf.Abs(Vector3.Dot(projDir, tf.right)) < 0.999f ? tf.right :
                tf.forward;
            
            if (Mathf.Abs(Vector3.Dot(projDir, up.normalized)) > 0.999f)
            {
                up = Vector3.Cross(projDir, Vector3.right).sqrMagnitude > Runtime.DecalCollider.KEpsilon
                    ? Vector3.Cross(projDir, Vector3.right).normalized
                    : Vector3.forward;
            }
            return up;
        }
        
        /// <summary>
        /// Caches a reference to the target DecalCollider component.
        /// </summary>
        private void InitializeTargetComponent() => m_TargetComponent = (Runtime.DecalCollider)target;

        /// <summary>
        /// Finds and caches all required SerializedProperty references from the target component.
        /// </summary>
        private void FindSerializedProperties()
        {
            var so = serializedObject;
            m_SizeProp = so.FindProperty("size");
            m_MeshScaleProp = so.FindProperty("meshScale");
            m_MaxDistanceProp = so.FindProperty("maxDistance");
            m_ProjectionSpaceProp = so.FindProperty("projectionSpace");
            m_ProjectionDirectionProp = so.FindProperty("projectionDirection");
            m_WrapMaskProp = so.FindProperty("wrapMask");
            m_CenterProp = so.FindProperty("center");
            m_MeshOffsetProp = so.FindProperty("meshOffset");
            m_RaycastGridExtentProp = so.FindProperty("raycastGridExtent");
            m_MeshSubdivisionsProp = so.FindProperty("meshSubdivisions");
            m_ColliderSubdivisionsProp = so.FindProperty("colliderSubdivisions");
            m_ColliderScaleProp = so.FindProperty("colliderScale");
            m_AlphaThresholdProp = so.FindProperty("alphaThreshold");
            m_AlwaysRebuildProp = so.FindProperty("alwaysRebuild");
            m_IgnoreSelfProp = so.FindProperty("ignoreSelf");
            m_CullIfInvisibleProp = so.FindProperty("cullIfInvisible");
            m_UseDynamicLODProp = so.FindProperty("useDynamicLOD");
            m_LodDistanceProp = so.FindProperty("lodDistance");
            m_LodCheckIntervalProp = so.FindProperty("lodCheckInterval");
            m_ShowBoundsGizmosProp = so.FindProperty("showBoundsGizmos");
            m_ShowDebugRaysProp = so.FindProperty("showDebugRays");
            m_EditorToolEditColliderActiveProp = so.FindProperty("editorToolEditColliderActive");
            m_EditorToolEditProjectionActiveProp = so.FindProperty("editorToolEditProjectionActive");
            m_DecalModeProp = so.FindProperty("decalMode");
            m_InputMeshProp = so.FindProperty("inputMesh");
            m_SourceTMPProp = so.FindProperty("sourceTMP");
            m_SourceSpriteRendererProp = so.FindProperty("sourceSpriteRenderer");
            m_LiveUpdateIntervalProp = so.FindProperty("liveUpdateInterval");
            m_UpdateColliderOnLiveProp = so.FindProperty("updateColliderOnLive");
        }
        
        /// <summary>
        /// Finds the custom icon for the DecalCollider script and applies it in the Project view.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void SetIconForScript()
        {
            Texture2D icon = null;
            string[] iconGuids = AssetDatabase.FindAssets("DecalColliderIcon t:Texture2D");
            if (iconGuids.Length > 0)
            {
                icon = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(iconGuids[0]));
            }
            if (icon == null) return;
            
            string[] scriptGuids = AssetDatabase.FindAssets("t:MonoScript DecalCollider");
            foreach (string guid in scriptGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("DecalCollider.cs"))
                {
                    var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                    if (script != null && script.GetClass() == typeof(Runtime.DecalCollider))
                    {
                        EditorGUIUtility.SetIconForObject(script, icon);
                        return; 
                    }
                }
            }
        }
        
        /// <summary>
        /// Caches a SerializedObject and its properties for the attached MeshCollider for physics property editing.
        /// </summary>
        private void InitializeMeshCollider()
        {
            if (m_TargetComponent != null && m_TargetComponent.TryGetComponent(out MeshCollider mc))
            {
                m_MeshColliderSo = new SerializedObject(mc);
                m_McConvexProp = m_MeshColliderSo.FindProperty("m_Convex");
                m_McIsTriggerProp = m_MeshColliderSo.FindProperty("m_IsTrigger");
                m_McProvidesContactsProp = m_MeshColliderSo.FindProperty("m_ProvidesContacts");
                m_McCookingOptionsProp = m_MeshColliderSo.FindProperty("m_CookingOptions");
                m_McMaterialProp = m_MeshColliderSo.FindProperty("m_Material");
                m_McLayerOverridePriorityProp = m_MeshColliderSo.FindProperty("m_LayerOverridePriority");
                m_McIncludeLayersProp = m_MeshColliderSo.FindProperty("m_IncludeLayers");
                m_McExcludeLayersProp = m_MeshColliderSo.FindProperty("m_ExcludeLayers");
            }
            else
            {
                m_MeshColliderSo = null;
            }
        }
        
        /// <summary>
        /// Schedules a safe rebuild of the decal after a short delay, used on enable.
        /// </summary>
        private void ScheduleRebuild()
        {
            if (m_TargetComponent == null || !m_TargetComponent.enabled || Application.isPlaying) return;
            EditorApplication.delayCall += () =>
            {
                if (m_TargetComponent != null && m_TargetComponent.enabled && m_TargetComponent.gameObject.activeInHierarchy)
                {
                    m_TargetComponent.RebuildSafe();
                    SceneView.RepaintAll();
                }
            };
        }
        
        /// <summary>
        /// Resets all custom tool states to false, typically called when the component or editor is disabled.
        /// </summary>
        private void ResetSceneToolStates()
        {
            var so = serializedObject;
            if (so.targetObject == null) return;

            bool wasModified = m_ShowBoundsGizmosProp.boolValue || m_EditorToolEditColliderActiveProp.boolValue || m_EditorToolEditProjectionActiveProp.boolValue;

            if (wasModified)
            {
                m_ShowBoundsGizmosProp.boolValue = false;
                m_EditorToolEditColliderActiveProp.boolValue = false;
                m_EditorToolEditProjectionActiveProp.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>
        /// Re-initializes the target component and its properties if they become null.
        /// </summary>
        private void InitializeTargetIfInvalid()
        {
            if (m_TargetComponent == null)
            {
                var currentTarget = target;
                if(currentTarget != null) 
                {
                    m_TargetComponent = (Runtime.DecalCollider)currentTarget;
                    OnEnable();
                }
            }
        }
        
        /// <summary>
        /// Checks if essential serialized properties are null, indicating a need for re-initialization.
        /// </summary>
        private bool CheckPropertiesAreNull()
        {
            return m_SizeProp == null || m_MaxDistanceProp == null || m_EditorToolEditColliderActiveProp == null;
        }
        
        /// <summary>
        /// Checks if any of the custom scene view tools are currently active.
        /// </summary>
        private bool IsAnyCustomToolActive()
        {
            if (m_TargetComponent == null) return false;
            return m_TargetComponent.editorToolEditColliderActive ||
                   m_TargetComponent.editorToolEditProjectionActive ||
                   m_TargetComponent.showBoundsGizmos;
        }
        
        #endregion
    }
}
#endif