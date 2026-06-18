using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace EdgeFusion {

	[Serializable]
	public class IdExclusionPair {
		public bool enabled = true;
		[Range(1, 32)] public int idA = 1;
		[Range(1, 32)] public int idB = 31;
	}

	public enum QualityPreset {
		VeryLow = 4,
		Low = 8,
		Medium = 16,
		High = 24,
		VeryHigh = 32
	}

	public enum DebugMode {
		None,
		ObjectIds,
		Edges,
		Blending,
		Normals,
		Depth,
		SpecialGroup
	}


	[Serializable]
	public sealed class QualityPresetParameter : VolumeParameter<QualityPreset> {
		public QualityPresetParameter (QualityPreset value, bool overrideState = false) : base(value, overrideState) { }
	}

	[Serializable]
	public sealed class DebugModeParameter : VolumeParameter<DebugMode> {
		public DebugModeParameter (DebugMode value, bool overrideState = false) : base(value, overrideState) { }
	}


	[HelpURL("https://kronnect.com/guides/edge-fusion-introduction/")]
	[VolumeComponentMenu("Kronnect/Edge Fusion")]
	public class EdgeFusion : VolumeComponent {

		[Tooltip("Default layers that will be considered for edge fusion blending.")]
		public LayerMaskParameter blendLayers = new LayerMaskParameter(-1);

		[Tooltip("Optional Rendering Layer Filter for single-sided objects selected by Blend Layers (rendering layer can be specified in the object renderer).")]
		public RenderingLayerMaskParameter renderingLayerFilter = new RenderingLayerMaskParameter(uint.MaxValue);

		[Tooltip("When enabled, visible geometry not selected by any layer setting will also blend with marked objects. Disable to restrict blending to explicitly selected layers only.")]
		public BoolParameter blendWithOthers = new BoolParameter(true);

		[Tooltip("Layers that will be rendered double-sided in the ObjectID pass.")]
		public LayerMaskParameter doubleSidedLayers = new LayerMaskParameter(0);

		[Tooltip("Rendering layer filter for Double-Sided selection.")]
		public RenderingLayerMaskParameter doubleSidedRenderingLayerFilter = new RenderingLayerMaskParameter(uint.MaxValue);

		[Tooltip("GameObjects that use special vertex shaders that should also blend")]
		public LayerMaskParameter specialGroupLayers = new LayerMaskParameter(0);

		[Tooltip("Rendering layer filter for Special Group selection.")]
		public RenderingLayerMaskParameter specialGroupRenderingLayerFilter = new RenderingLayerMaskParameter(uint.MaxValue);

		[Tooltip("Overall intensity of the edge fusion effect.")]
		public ClampedFloatParameter intensity = new ClampedFloatParameter(0.0f, 0.0f, 1.0f);

		[Tooltip("Default blur radius in world units (meters). Use Distance Compensation to increase effective radius at distance.")]
		public ClampedFloatParameter radius = new ClampedFloatParameter(0.035f, 0.0001f, 0.5f);

		[Tooltip("Scales the effective radius proportionally with distance from the camera. Use this to keep a small radius up close while increasing it for distant objects.")]
		public ClampedFloatParameter distanceCompensation = new ClampedFloatParameter(0f, 0f, 0.1f);

		[Tooltip("Enable fusion of edges within the same object (based on normal/depth discontinuities)")]
		[InspectorName("Intra-Object Fusion")]
		public BoolParameter enableIntraObjectFusion = new BoolParameter(false);

		[Tooltip("When enabled, intra-object fusion only runs on renderers that have an Edge Fusion Object component with the effect allowed; fill-pass normals are skipped.")]
		public BoolParameter intraObjectFusionPerObject = new BoolParameter(false);

		[Tooltip("Fuse concave edges only. When disabled, both concave and convex edges are fused.")]
		public BoolParameter concavityTest = new BoolParameter(false);

		[Tooltip("Normal threshold for detecting edges within the same object (only used when Intra-Object Fusion is enabled)")]
		public ClampedFloatParameter normalThreshold = new ClampedFloatParameter(0.6f, 0.001f, 0.95f);

		public ClampedIntParameter sampleCount = new ClampedIntParameter(24, 4, 32);

		public BoolParameter jitter = new BoolParameter(false);

		public FloatParameter maxBlendDistance = new FloatParameter(50f);

		[Tooltip("Number of binary search refinement steps when locating nearest edge")]
		public ClampedIntParameter binarySearchSteps = new ClampedIntParameter(7, 1, 10);

		[Tooltip("Max number of edge hits before early exiting the neighbour search loop")]
		public ClampedIntParameter earlyExitHits = new ClampedIntParameter(5, 1, 32);

		[Tooltip("Stabilises the blending when two edge candidates are near-tie in distance. Instead of picking one winner per frame, averages them with a soft-argmin so edgeUV moves continuously. Helps in any scene with sub-pixel motion between frames (VR pose, slow cameras, animated objects, FPS mouse look, camera shake). Disable to match the classic pick-one behaviour.")]
		public BoolParameter antiFlicker = new BoolParameter(false);

		public DebugModeParameter debugMode = new DebugModeParameter(DebugMode.None);
		public ClampedFloatParameter depthDebugMultiplier = new ClampedFloatParameter(1.0f, 0.01f, 100.0f);
		[Tooltip("Adjusts color sampling to reduce MSAA edge artifacts (MSAA only).")]
		public MinFloatParameter msaaEdgeFixPower = new MinFloatParameter(0f, 0f);
		public BoolParameter compareMode = new BoolParameter(false);
		public BoolParameter compareSameSide = new BoolParameter(false);
		public ClampedFloatParameter comparePanning = new ClampedFloatParameter(0.25f, 0f, 0.5f);
		public ClampedFloatParameter compareLineAngle = new ClampedFloatParameter(1.4f, -Mathf.PI, Mathf.PI);
		public ClampedFloatParameter compareLineWidth = new ClampedFloatParameter(0.002f, 0.0001f, 0.05f);
		public ColorParameter compareLineColor = new ColorParameter(Color.white);

		public ClampedFloatParameter maxScreenRadius = new ClampedFloatParameter(0.1f, 0.01f, 0.5f);

		[Tooltip("Threshold for shadow detection and blending adjustment")]
		public ClampedFloatParameter shadowProtection = new ClampedFloatParameter(0.0002f, 0f, 0.01f);

		[Tooltip("Intensity of the 3D noise texture applied to blending (0 = no noise, 1 = full noise influence)")]
		public ClampedFloatParameter noiseIntensity = new ClampedFloatParameter(0.0f, 0.0f, 1.0f);

		[Tooltip("Scale of the 3D noise texture sampling (higher values = finer noise)")]
		public ClampedFloatParameter noiseScale = new ClampedFloatParameter(5.0f, 0.1f, 10.0f);

		[Tooltip("Contrast of the noise applied to edge positions (0 = soft, 1 = sharp)")]
		public ClampedFloatParameter noiseContrast = new ClampedFloatParameter(0.75f, 0.0f, 1.0f);

		[Header("ID Exclusions")]
		[Tooltip("Pairs of custom Object IDs that should not blend with each other. IDs 1-30 are user-assignable, 31 = Terrain (non-blend-layer objects), 32 = Special Group.")]
		public IdExclusionPair[] idExclusionPairs = System.Array.Empty<IdExclusionPair>();

		public bool IsActive () {
			return (blendLayers.value != 0 || doubleSidedLayers.value != 0 || specialGroupLayers.value != 0) && intensity.value > 0f;
		}

		public void OnValidate () {
			maxBlendDistance.value = Mathf.Max(maxBlendDistance.value, 0f);
			msaaEdgeFixPower.value = Mathf.Max(msaaEdgeFixPower.value, 0f);
		}

	}
}