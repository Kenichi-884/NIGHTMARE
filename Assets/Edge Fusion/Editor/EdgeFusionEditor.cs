using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace EdgeFusion {

	[CustomEditor(typeof(EdgeFusion))]
	public class EdgeFusionEditor : VolumeComponentEditor {

		SerializedDataParameter blendLayers, renderingLayerFilter, blendWithOthers, specialGroupLayers, doubleSidedLayers, intensity, radius, distanceCompensation, enableIntraObjectFusion, normalThreshold, concavityTest, intraObjectFusionPerObject;
		SerializedDataParameter sampleCount, jitter, maxBlendDistance, binarySearchSteps, maxScreenRadius;
		SerializedDataParameter doubleSidedRenderingLayerFilter, specialGroupRenderingLayerFilter;
		SerializedDataParameter earlyExitHits, antiFlicker;
		SerializedDataParameter debugMode, depthDebugMultiplier, shadowProtection, noiseIntensity, noiseScale, noiseContrast;
		SerializedDataParameter compareMode, compareSameSide, comparePanning, compareLineAngle, compareLineWidth, compareLineColor;
		SerializedDataParameter msaaEdgeFixPower;
		SerializedProperty idExclusionPairs;

		static readonly QualityPreset[] k_Presets = (QualityPreset[])Enum.GetValues(typeof(QualityPreset));
		static readonly int[] k_PresetValues = k_Presets
			.Select(p => (int)p)
			.ToArray();
		static PropertyInfo pipelineMsaaProperty;
		static Type cachedPipelineType;
		string[] presetNames;
		bool renderFeaturePresent;
		Terrain[] sceneTerrainsCache;
		bool hasEdgeFusionObjectInScene;
		const string k_AdditionalGroupsFoldoutKey = "EdgeFusion.AdditionalGroupsFoldout";
		bool showAdditionalGroups;
		bool msaaCameraActive;

		public override void OnEnable () {

			try {
				presetNames = k_Presets
					.Select(p => ObjectNames.NicifyVariableName(p.ToString()))
					.ToArray();
				var o = new PropertyFetcher<EdgeFusion>(serializedObject);
				blendLayers = Unpack(o.Find(x => x.blendLayers));
				renderingLayerFilter = Unpack(o.Find(x => x.renderingLayerFilter));
				blendWithOthers = Unpack(o.Find(x => x.blendWithOthers));
				specialGroupLayers = Unpack(o.Find(x => x.specialGroupLayers));
				doubleSidedLayers = Unpack(o.Find(x => x.doubleSidedLayers));
				doubleSidedRenderingLayerFilter = Unpack(o.Find(x => x.doubleSidedRenderingLayerFilter));
				specialGroupRenderingLayerFilter = Unpack(o.Find(x => x.specialGroupRenderingLayerFilter));
				intensity = Unpack(o.Find(x => x.intensity));
				radius = Unpack(o.Find(x => x.radius));
				distanceCompensation = Unpack(o.Find(x => x.distanceCompensation));
				enableIntraObjectFusion = Unpack(o.Find(x => x.enableIntraObjectFusion));
				intraObjectFusionPerObject = Unpack(o.Find(x => x.intraObjectFusionPerObject));
				normalThreshold = Unpack(o.Find(x => x.normalThreshold));
				concavityTest = Unpack(o.Find(x => x.concavityTest));
				sampleCount = Unpack(o.Find(x => x.sampleCount));
				jitter = Unpack(o.Find(x => x.jitter));
				maxBlendDistance = Unpack(o.Find(x => x.maxBlendDistance));
				binarySearchSteps = Unpack(o.Find(x => x.binarySearchSteps));
				earlyExitHits = Unpack(o.Find(x => x.earlyExitHits));
				antiFlicker = Unpack(o.Find(x => x.antiFlicker));
				debugMode = Unpack(o.Find(x => x.debugMode));
				depthDebugMultiplier = Unpack(o.Find(x => x.depthDebugMultiplier));
				msaaEdgeFixPower = Unpack(o.Find(x => x.msaaEdgeFixPower));
				shadowProtection = Unpack(o.Find(x => x.shadowProtection));
				maxScreenRadius = Unpack(o.Find(x => x.maxScreenRadius));
				noiseIntensity = Unpack(o.Find(x => x.noiseIntensity));
				noiseScale = Unpack(o.Find(x => x.noiseScale));
				noiseContrast = Unpack(o.Find(x => x.noiseContrast));
				compareMode = Unpack(o.Find(x => x.compareMode));
				compareSameSide = Unpack(o.Find(x => x.compareSameSide));
				comparePanning = Unpack(o.Find(x => x.comparePanning));
				compareLineAngle = Unpack(o.Find(x => x.compareLineAngle));
				compareLineWidth = Unpack(o.Find(x => x.compareLineWidth));
				compareLineColor = Unpack(o.Find(x => x.compareLineColor));
				idExclusionPairs = serializedObject.FindProperty("idExclusionPairs");
				renderFeaturePresent = IsEdgeFusionRenderFeaturePresent();
#if UNITY_6000_4_OR_NEWER
				sceneTerrainsCache = FindObjectsByType<Terrain>();
#else
				sceneTerrainsCache = FindObjectsByType<Terrain>(FindObjectsSortMode.None);
#endif
				showAdditionalGroups = EditorPrefs.GetBool(k_AdditionalGroupsFoldoutKey, false);
				msaaCameraActive = CameraUsesMsaa();
				hasEdgeFusionObjectInScene = SceneHasEdgeFusionObject();
			}
			catch {
			}
		}

		public override void OnInspectorGUI () {

			int blendLayersValue = blendLayers.overrideState.boolValue ? blendLayers.value.intValue : -1;
			int doubleSidedLayersValue = doubleSidedLayers.overrideState.boolValue ? doubleSidedLayers.value.intValue : 0;
			int specialGroupLayersValue = specialGroupLayers.overrideState.boolValue ? specialGroupLayers.value.intValue : 0;
			uint renderingLayerFilterValue = renderingLayerFilter.overrideState.boolValue ? (uint)renderingLayerFilter.value.intValue : uint.MaxValue;

			serializedObject.Update();

			if (!renderFeaturePresent) {
				EditorGUILayout.Space();
				EditorGUILayout.HelpBox("Edge Fusion Render Feature is not added to the current URP Renderer. The effect will not be visible.", MessageType.Error);
				if (GUILayout.Button("Select Current URP Renderer", GUILayout.Height(20))) {
					SelectCurrentURPRenderer();
				}
				EditorGUILayout.Space();
			}

			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Online Resources & Support")) {
				ContactUsWindow.ShowScreen();
			}
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.Space();

			EditorGUILayout.LabelField("Object Selection", EditorStyles.boldLabel);
			PropertyField(blendLayers);
			PropertyField(renderingLayerFilter, new GUIContent("Rendering Layer Filter"));

			// Only show "Blend With Others" if something is excluded (otherwise it has no effect)
			bool somethingExcluded = blendLayersValue != -1 || renderingLayerFilterValue != uint.MaxValue;
			if (somethingExcluded) {
				PropertyField(blendWithOthers, new GUIContent("Blend With Others", "When enabled, visible geometry not selected by any layer setting will also blend with marked objects."));
			}

			EditorGUILayout.Space(4);
			using (var foldoutScope = new EditorGUI.ChangeCheckScope()) {
				showAdditionalGroups = EditorGUILayout.Foldout(showAdditionalGroups, "Additional Object Groups", true);
				if (foldoutScope.changed) {
					EditorPrefs.SetBool(k_AdditionalGroupsFoldoutKey, showAdditionalGroups);
				}
			}

			if (showAdditionalGroups) {
				EditorGUI.indentLevel++;
				EditorGUILayout.BeginVertical(EditorStyles.helpBox);
				EditorGUILayout.LabelField("Double-Sided Objects", EditorStyles.miniLabel);
				PropertyField(doubleSidedLayers, new GUIContent("Layers"));
				PropertyField(doubleSidedRenderingLayerFilter, new GUIContent("Rendering Layer Filter"));
				EditorGUILayout.EndVertical();

				EditorGUILayout.BeginVertical(EditorStyles.helpBox);
				EditorGUILayout.LabelField("Special Group (vertex displacement, animated vegetation)", EditorStyles.miniLabel);
				PropertyField(specialGroupLayers, new GUIContent("Layers"));
				PropertyField(specialGroupRenderingLayerFilter, new GUIContent("Rendering Layer Filter"));
				EditorGUILayout.EndVertical();
				EditorGUI.indentLevel--;

				if (specialGroupLayers.overrideState.boolValue && specialGroupLayersValue == -1) {
					EditorGUILayout.HelpBox("Special Group selection should only include objects with custom vertex shaders.", MessageType.Warning);
				}
			}

			if ((blendLayersValue & doubleSidedLayersValue) != 0) {
				EditorGUILayout.HelpBox("Layers included in Double-Sided Layers are automatically excluded from Blend Layers.", MessageType.Info);
			}
			if (((blendLayersValue & specialGroupLayersValue) != 0) || ((doubleSidedLayersValue & specialGroupLayersValue) != 0)) {
				EditorGUILayout.HelpBox("Layers included in Special Group Layers are automatically excluded from Blend Layers and Double-Sided Layers.", MessageType.Info);
			}
			int effectiveBlendForTerrain = (blendLayersValue & ~doubleSidedLayersValue) & ~specialGroupLayersValue;
			CheckTerrainBlendLayers(effectiveBlendForTerrain, renderingLayerFilterValue);

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("ID Exclusions", EditorStyles.boldLabel);
			DrawIdExclusionPairs(blendLayersValue, renderingLayerFilterValue);

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Blending", EditorStyles.boldLabel);
			PropertyField(intensity);
			PropertyField(radius);
			PropertyField(distanceCompensation, new GUIContent("Distance Compensation", "Scales the effective radius proportionally with distance from the camera. Keeps a small radius up close while increasing it for distant objects."));
			PropertyField(maxScreenRadius);
			PropertyField(enableIntraObjectFusion);
			if (enableIntraObjectFusion.value.boolValue) {
				EditorGUI.indentLevel++;
				PropertyField(normalThreshold);
				PropertyField(concavityTest);
				PropertyField(intraObjectFusionPerObject, new GUIContent("Per Object", "When enabled, intra-object fusion only runs on renderers that have an Edge Fusion Object component with the effect allowed; fill-pass normals are skipped."));
				if (intraObjectFusionPerObject.value.boolValue && !hasEdgeFusionObjectInScene) {
					EditorGUILayout.HelpBox("Add 'Edge Fusion Object' component to the gameobjects you want to control intra-object fusion.", MessageType.Warning);
				}
				EditorGUI.indentLevel--;
			}
			PropertyField(shadowProtection);

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Noise", EditorStyles.boldLabel);
			PropertyField(noiseIntensity);
			if (noiseIntensity.value.floatValue > 0) {
				EditorGUI.indentLevel++;
				PropertyField(noiseScale, new GUIContent("Scale"));
				PropertyField(noiseContrast, new GUIContent("Contrast"));
				EditorGUI.indentLevel--;
			}
			PropertyField(jitter);

			EditorGUILayout.Space();
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Performance", EditorStyles.boldLabel);
			if (GUILayout.Button("Tips", GUILayout.Width(50), GUILayout.Height(18))) {
				Application.OpenURL("https://kronnect.com/guides/edge-fusion-performance-tips/");
			}
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.BeginHorizontal();
			PropertyField(sampleCount);
			int currentQuality = sampleCount.value.intValue;
			int selectedIndex = GetClosestPresetIndex(currentQuality);
			using (var cc = new EditorGUI.ChangeCheckScope()) {
				EditorGUI.BeginDisabledGroup(!sampleCount.overrideState.boolValue);
				int newIndex = EditorGUILayout.Popup(selectedIndex, presetNames ?? Array.Empty<string>(), GUILayout.Width(80f));
				EditorGUI.EndDisabledGroup();
				if (cc.changed && newIndex >= 0 && newIndex < k_PresetValues.Length) {
					var selectedPreset = (QualityPreset)k_PresetValues[newIndex];
					sampleCount.value.intValue = k_PresetValues[newIndex];
					binarySearchSteps.value.intValue = GetBinarySearchStepsForPreset(selectedPreset);
					binarySearchSteps.overrideState.boolValue = true;
					earlyExitHits.value.intValue = GetEarlyExitHitsForPreset(selectedPreset);
					earlyExitHits.overrideState.boolValue = true;
				}
			}
			EditorGUILayout.EndHorizontal();
			PropertyField(binarySearchSteps);
			PropertyField(earlyExitHits, new GUIContent("Early Exit Hits", "Stops searching edge position after n nearest edge hits"));
			PropertyField(antiFlicker, new GUIContent("Anti-Flicker", "Stabilises the blending when two edge candidates are near-tie in distance. Averages them with a soft-argmin so edgeUV moves continuously instead of snapping between winners. Helps in any scene with sub-pixel motion between frames (VR, slow camera motion, animated objects, FPS mouse look, camera shake). Disable to match the classic pick-one behaviour."));
			PropertyField(maxBlendDistance);

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
			PropertyField(debugMode);
			if (debugMode.overrideState.boolValue && debugMode.value.intValue == (int)DebugMode.Depth) {
				EditorGUI.indentLevel++;
				PropertyField(depthDebugMultiplier, new GUIContent("Depth Multiplier"));
				EditorGUI.indentLevel--;
			}
			if (debugMode.overrideState.boolValue && debugMode.value.intValue == (int)DebugMode.SpecialGroup && (!specialGroupLayers.overrideState.boolValue || specialGroupLayersValue == 0)) {
				EditorGUILayout.HelpBox("Special Group is not executing because Special Group Layers is set to Nothing.", MessageType.Warning);
			}

			PropertyField(compareMode, new GUIContent("Compare Mode"));
			if (compareMode.value.boolValue) {
				EditorGUI.indentLevel++;
				PropertyField(compareSameSide, new GUIContent("Same Side"));
				if (compareSameSide.value.boolValue) {
					PropertyField(comparePanning, new GUIContent("Panning"));
				}
				else {
					PropertyField(compareLineAngle, new GUIContent("Line Angle"));
				}
				PropertyField(compareLineWidth, new GUIContent("Line Width"));
				PropertyField(compareLineColor, new GUIContent("Line Color"));
				EditorGUI.indentLevel--;
			}

			if (msaaCameraActive) {
				PropertyField(msaaEdgeFixPower, new GUIContent("MSAA Edge Fix Amount", "When using MSAA in forward pass and certain blending layers configuration, you can use this option to reduce MSAA edge artifacts due to multi-sampling."));
			}

			serializedObject.ApplyModifiedProperties();
		}

		void CheckTerrainBlendLayers (int blendLayerMask, uint renderingLayerMaskValue) {
			if (sceneTerrainsCache == null || sceneTerrainsCache.Length == 0) return;

			bool hasTerrainWithDrawInstanced = false;
			bool hasTerrainWithoutDrawInstanced = false;

			uint rlMask = renderingLayerMaskValue;
			foreach (var terrain in sceneTerrainsCache) {
				if (terrain == null) continue;
				bool selectedByBlendLayers = IsLayerInMask(terrain.gameObject.layer, blendLayerMask);
				bool selectedByRenderingLayers = (rlMask == uint.MaxValue) || ((terrain.renderingLayerMask & rlMask) != 0);
				bool consideredForBlend = selectedByBlendLayers && selectedByRenderingLayers;
				if (!consideredForBlend) continue;

				if (terrain.drawInstanced) {
					hasTerrainWithDrawInstanced = true;
				}
				else {
					hasTerrainWithoutDrawInstanced = true;
				}
			}

			if (hasTerrainWithDrawInstanced) {
				EditorGUILayout.HelpBox(
					"Terrain has Draw Instanced option enabled which is not compatible with Edge Fusion. However, you can exclude it from blending layers or rendering layers as a workaround which is also faster.",
					MessageType.Error
				);
			}
			else if (hasTerrainWithoutDrawInstanced) {
				EditorGUILayout.HelpBox(
					"For best performance, you can exclude terrain objects from blending (via Blend Layers or Rendering Layers).",
					MessageType.Warning
				);
			}
		}

		static bool IsLayerInMask (int layer, int layerMask) {
			return ((layerMask & (1 << layer)) != 0);
		}

		static readonly string[] exclusionIdNames = BuildExclusionIdNames();

		bool SceneHasEdgeFusionObject () {
#if UNITY_6000_4_OR_NEWER
			return FindObjectsByType<EdgeFusionObject>().Length > 0;
#else
			return FindObjectsByType<EdgeFusionObject>(FindObjectsSortMode.None).Length > 0;
#endif
		}

		static string[] BuildExclusionIdNames () {
			var names = new string[32];
			for (int i = 0; i < 30; i++) {
				names[i] = $"ID {i + 1}";
			}
			names[30] = "ID 31 (Terrain)";
			names[31] = "ID 32 (Special Group)";
			return names;
		}

		void DrawIdExclusionPairs (int blendLayersValue, uint renderingLayerFilterValue) {
			EditorGUILayout.BeginHorizontal();
			GUILayout.Space(15f);
			EditorGUILayout.BeginVertical(EditorStyles.helpBox);
			EditorGUILayout.LabelField("Exclusion Pairs", EditorStyles.miniLabel);

			bool usesTerrain = false;
			for (int i = 0; i < idExclusionPairs.arraySize; i++) {
				var element = idExclusionPairs.GetArrayElementAtIndex(i);
				var enabledProp = element.FindPropertyRelative("enabled");
				var idA = element.FindPropertyRelative("idA");
				var idB = element.FindPropertyRelative("idB");

				if (enabledProp.boolValue && (idA.intValue == 31 || idB.intValue == 31)) usesTerrain = true;

				EditorGUILayout.BeginHorizontal();
				enabledProp.boolValue = EditorGUILayout.Toggle(enabledProp.boolValue, GUILayout.Width(16));
				EditorGUI.BeginDisabledGroup(!enabledProp.boolValue);
				idA.intValue = EditorGUILayout.Popup(idA.intValue - 1, exclusionIdNames) + 1;
				EditorGUILayout.LabelField("↔", GUILayout.Width(20));
				idB.intValue = EditorGUILayout.Popup(idB.intValue - 1, exclusionIdNames) + 1;
				EditorGUI.EndDisabledGroup();
				if (GUILayout.Button("×", GUILayout.Width(20))) {
					idExclusionPairs.DeleteArrayElementAtIndex(i);
					i--;
				}
				EditorGUILayout.EndHorizontal();
			}

			if (GUILayout.Button("+ Add Exclusion Pair")) {
				idExclusionPairs.InsertArrayElementAtIndex(idExclusionPairs.arraySize);
				var newElement = idExclusionPairs.GetArrayElementAtIndex(idExclusionPairs.arraySize - 1);
				newElement.FindPropertyRelative("enabled").boolValue = true;
				newElement.FindPropertyRelative("idA").intValue = 1;
				newElement.FindPropertyRelative("idB").intValue = 31;
			}

			EditorGUILayout.EndVertical();
			EditorGUILayout.EndHorizontal();

			bool terrainIncludedInBlend = blendLayersValue == -1 && renderingLayerFilterValue == uint.MaxValue;
			if (usesTerrain && terrainIncludedInBlend) {
				EditorGUILayout.HelpBox(
					"You are excluding Terrain (ID 31) but Blend Layers and Rendering Layer Filter include everything. " +
					"For terrain to receive ID 31, it must be excluded from Blend Layers or Rendering Layer Filter so it goes through the fill pass.",
					MessageType.Warning
				);
			}

			if (idExclusionPairs.arraySize > 0) {
				EditorGUILayout.HelpBox(
					"IDs 1-30: Set via EdgeFusionObject component\n" +
					"ID 31: Terrain (objects not in Blend Layers)\n" +
					"ID 32: Special Group objects",
					MessageType.Info
				);
			}
		}

		static int GetClosestPresetIndex (int sampleCount) {
			int closestIndex = 0;
			int smallestDelta = int.MaxValue;
			for (int i = 0; i < k_PresetValues.Length; i++) {
				int delta = Mathf.Abs(k_PresetValues[i] - sampleCount);
				if (delta < smallestDelta) {
					smallestDelta = delta;
					closestIndex = i;
				}
			}
			return closestIndex;
		}

		static int GetBinarySearchStepsForPreset (QualityPreset preset) {
			switch (preset) {
				case QualityPreset.VeryLow: return 2;
				case QualityPreset.Low: return 4;
				case QualityPreset.Medium: return 5;
				case QualityPreset.High: return 7;
				case QualityPreset.VeryHigh: return 8;
				default: return 7;
			}
		}

		static int GetEarlyExitHitsForPreset (QualityPreset preset) {
			switch (preset) {
				case QualityPreset.VeryLow: return 1;
				case QualityPreset.Low: return 2;
				case QualityPreset.Medium: return 3;
				case QualityPreset.High: return 4;
				case QualityPreset.VeryHigh: return 5;
				default: return 5;
			}
		}

		bool IsEdgeFusionRenderFeaturePresent () {
			return GetCurrentURPRendererData() != null && CheckRendererForEdgeFusion(GetCurrentURPRendererData());
		}

		UnityEngine.Object GetCurrentURPRendererData () {
			var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
			if (pipeline == null) return null;

			var serializedAsset = new SerializedObject(pipeline);
			var rendererDataList = serializedAsset.FindProperty("m_RendererDataList");
			if (rendererDataList == null || !rendererDataList.isArray || rendererDataList.arraySize == 0) return null;

			var defaultRendererIndex = serializedAsset.FindProperty("m_DefaultRendererIndex");
			int index = defaultRendererIndex != null ? defaultRendererIndex.intValue : 0;
			if (index < 0 || index >= rendererDataList.arraySize) return null;

			var rendererDataProperty = rendererDataList.GetArrayElementAtIndex(index);
			return rendererDataProperty.objectReferenceValue;
		}

		bool CheckRendererForEdgeFusion (UnityEngine.Object rendererData) {
			if (rendererData == null) return false;

			var serializedRenderer = new SerializedObject(rendererData);
			var featuresProperty = serializedRenderer.FindProperty("m_RendererFeatures");
			if (featuresProperty == null || !featuresProperty.isArray) return false;

			for (int i = 0; i < featuresProperty.arraySize; i++) {
				var featureProperty = featuresProperty.GetArrayElementAtIndex(i);
				var feature = featureProperty.objectReferenceValue;
				if (feature != null && feature.GetType().Name == "EdgeFusionRenderFeature") {
					return true;
				}
			}

			return false;
		}

		void SelectCurrentURPRenderer () {
			var rendererData = GetCurrentURPRendererData();
			if (rendererData != null) {
				Selection.activeObject = rendererData;
				EditorGUIUtility.PingObject(rendererData);
			}
		}

		static bool CameraUsesMsaa () {
			if (TryGetPipelineMsaaSamples(GraphicsSettings.currentRenderPipeline, out int pipelineSamples) && pipelineSamples > 1) {
				return true;
			}
			foreach (Camera cam in Camera.allCameras) {
				if (cam == null || !cam.allowMSAA) continue;
				if (cam.targetTexture != null) {
					if (cam.targetTexture.antiAliasing > 1) return true;
				}
				else if (QualitySettings.antiAliasing > 1) {
					return true;
				}
			}
			return false;
		}

		static bool TryGetPipelineMsaaSamples (RenderPipelineAsset pipeline, out int samples) {
			samples = 0;
			if (pipeline == null) return false;
			var type = pipeline.GetType();
			if (type != cachedPipelineType) {
				cachedPipelineType = type;
				pipelineMsaaProperty = type.GetProperty("msaaSampleCount", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			}
			if (pipelineMsaaProperty != null && pipelineMsaaProperty.PropertyType == typeof(int)) {
				samples = (int)pipelineMsaaProperty.GetValue(pipeline);
				return true;
			}
			return false;
		}

	}
}