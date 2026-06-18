using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EdgeFusion {

    [HelpURL("https://kronnect.com/guides/edge-fusion-introduction/")]
    public class EdgeFusionRenderFeature : ScriptableRendererFeature {

        [SerializeField]
        [Tooltip("Whether to show the effect in Scene View")]
        public bool showInSceneView = true;

        [Tooltip("Allows Edge Fusion to be executed even if camera has Post Processing option disabled")]
        public bool ignorePostProcessingOption = true;

        [Tooltip("Filter which cameras can render Edge Fusion by their GameObject layer")]
        public LayerMask camerasLayerMask = -1;

        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;


        EdgeFusionRenderPass renderPass;

        public override void Create () {
            renderPass = new EdgeFusionRenderPass(this);
        }

        public override void AddRenderPasses (ScriptableRenderer renderer, ref RenderingData renderingData) {
            if (renderPass == null)
                return;

            if (renderingData.cameraData.isPreviewCamera || renderingData.cameraData.cameraType == CameraType.Reflection || (!showInSceneView && renderingData.cameraData.cameraType == CameraType.SceneView))
                return;

            if (renderingData.cameraData.renderType != CameraRenderType.Base)
                return;

            if ((camerasLayerMask & (1 << renderingData.cameraData.camera.gameObject.layer)) == 0)
                return;

            if (!ignorePostProcessingOption && !renderingData.cameraData.postProcessEnabled)
                return;

            if (IsDepthOnlyTarget(renderingData.cameraData.cameraTargetDescriptor))
                return;

	var settings = VolumeManager.instance.stack.GetComponent<EdgeFusion>();
			bool hasSettings = settings != null;
			if (!hasSettings) return;

			int cameraMask = renderingData.cameraData.camera.cullingMask;
			int effectiveBlendLayers = settings.blendLayers.value & cameraMask;
			int effectiveDoubleSided = settings.doubleSidedLayers.value & cameraMask;
			int effectiveSpecialGroup = settings.specialGroupLayers.value & cameraMask;
			int dsMask = effectiveDoubleSided;
			int ssMask = effectiveBlendLayers & ~effectiveDoubleSided;
			bool hasAnySubset = (dsMask | ssMask | effectiveSpecialGroup) != 0;
			if (!hasAnySubset) return;

			bool effectActive = settings.intensity.value > 0f;
			bool debugging = settings.debugMode.value != DebugMode.None;
			if (!debugging && !effectActive) return;

            renderPass.UpdateInputRequirement(settings);

            renderPass.renderPassEvent = renderPassEvent;
            renderer.EnqueuePass(renderPass);
        }

        protected override void Dispose (bool disposing) {
            renderPass?.Cleanup();
        }

        internal static bool IsDepthOnlyTarget(in RenderTextureDescriptor descriptor) {
            if (descriptor.colorFormat == RenderTextureFormat.Depth) {
                return true;
            }
            if (descriptor.graphicsFormat == GraphicsFormat.None) {
                return true;
            }
            return false;
        }
    }
}