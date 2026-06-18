using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace EdgeFusion {
    public class EdgeFusionRenderPass : ScriptableRenderPass {

        // Shader names
        const string OBJECT_ID_SHADER_PATH = "Kronnect/EdgeFusion/ObjectID";
        const string FUSION_SHADER_PATH = "Hidden/Kronnect/EdgeFusion/EdgeFusion";
        const string FUSION_FILL_SHADER_PATH = "Hidden/Kronnect/EdgeFusion/EdgeFusionFill";

        // Texture names
        const string OBJECT_ID_TEXTURE_NAME = "EdgeFusion_ObjectID";
        const string OBJECT_ID_DEPTH_TEXTURE_NAME = "EdgeFusion_ObjectID_Depth";
        const string CUSTOM_GROUP_TEXTURE_NAME = "EdgeFusion_CustomGroups";
        const string CAMERA_COLOR_TEXTURE_NAME = "EdgeFusion_CameraColor";
        const string COMPARE_TEXTURE_NAME = "EdgeFusion_CompareTex";

        // Pass names
        const string OBJECT_ID_PASS_NAME = "Edge Fusion - ObjectID";
        const string BLEND_FORWARD_PASS_NAME = "Edge Fusion - Blend Forward";
        const string DEBUG_PASS_NAME = "Edge Fusion - Debug";
        const string COMPARE_PASS_NAME = "Edge Fusion - Compare";
        const string SPECIAL_GROUP_PASS_NAME = "Edge Fusion - Special Group";
        const string FILL_OBJECT_ID_PASS_NAME = "Edge Fusion - Fill ObjectID";
        const string COPY_BACK_PASS_NAME = "Edge Fusion - Copy Back";

        EdgeFusionRenderFeature feature;
        Material objectIDMaterial;
        Material fusionMaterial;
        Material fusionFillMaterial;
        readonly List<ShaderTagId> shaderTags;
        UnityEngine.Experimental.Rendering.GraphicsFormat objectIdTextureFormat;
        Texture3D noiseTex;
        static readonly float[] exclusionMask = new float[32];
        static readonly uint[] exclusionMaskBits = new uint[32];
        static Volume cachedExclusionVolume;
        static EdgeFusion cachedEdgeFusion;

        static bool CanStoreMaskValue (uint value) {
            return (uint)(float)value == value;
        }

        public EdgeFusionRenderPass (EdgeFusionRenderFeature feature) {
            this.feature = feature;
            shaderTags = new List<ShaderTagId> {
                new ShaderTagId("SRPDefaultUnlit"),
                new ShaderTagId("UniversalForward"),
                new ShaderTagId("UniversalForwardOnly"),
                new ShaderTagId("LightweightForward")
            };

            var rg32Format = UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32_SFloat;
            bool canUseRG32Format = SystemInfo.IsFormatSupported(rg32Format, UnityEngine.Experimental.Rendering.GraphicsFormatUsage.Render);
            objectIdTextureFormat = canUseRG32Format ? rg32Format : UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32B32A32_SFloat;

            InitializeMaterials();
        }

        public void UpdateInputRequirement (EdgeFusion settings) {
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        void InitializeMaterials () {
            if (objectIDMaterial == null) {
                var shader = Shader.Find(OBJECT_ID_SHADER_PATH);
                if (shader != null) objectIDMaterial = CoreUtils.CreateEngineMaterial(shader);
                if (objectIDMaterial != null) objectIDMaterial.enableInstancing = true;
            }

            if (fusionMaterial == null) {
                var shader = Shader.Find(FUSION_SHADER_PATH);
                if (shader != null) fusionMaterial = CoreUtils.CreateEngineMaterial(shader);
            }

            if (fusionFillMaterial == null) {
                var shader = Shader.Find(FUSION_FILL_SHADER_PATH);
                if (shader != null) fusionFillMaterial = CoreUtils.CreateEngineMaterial(shader);
            }

            noiseTex = Resources.Load<Texture3D>("EdgeFusion/Textures/NoiseTex3D");
        }

        void UpdateMaterialProperties (EdgeFusion settings, bool taa, float effectiveMaxBlendDistance, bool cameraUsesMsaa, bool specialGroupActive) {

            float mappedNoiseContrast = Mathf.Lerp(0.5f, 0.01f, settings.noiseContrast.value);

            Vector4 blendData1 = new Vector4(
                settings.intensity.value,           // x: GlobalIntensity
                effectiveMaxBlendDistance,          // y: MaxBlendDistance
                settings.normalThreshold.value,     // z: NormalThreshold
                mappedNoiseContrast                 // w: NoiseContrast
            );

            Vector4 blendData2 = new Vector4(
                settings.maxScreenRadius.value,     // x: MaxScreenRadius
                settings.shadowProtection.value,    // y: ShadowProtection
                settings.noiseIntensity.value,      // z: NoiseIntensity
                settings.noiseScale.value           // w: NoiseScale
            );

            float radiusScale = Mathf.Max(1f, settings.radius.value / 0.89f);

            fusionMaterial.SetVector(ShaderParams.BlendData1, blendData1);
            fusionMaterial.SetVector(ShaderParams.BlendData2, blendData2);
            fusionMaterial.SetFloat(ShaderParams.DefaultRadiusWorld, settings.radius.value);
            fusionMaterial.SetFloat(ShaderParams.RadiusScale, radiusScale);
            fusionMaterial.SetFloat(ShaderParams.DistanceCompensation, settings.distanceCompensation.value);
            fusionMaterial.SetInt(ShaderParams.SampleCount, settings.sampleCount.value);
            fusionMaterial.SetInt(ShaderParams.BinarySearchSteps, settings.binarySearchSteps.value);
            fusionMaterial.SetInt(ShaderParams.EarlyExitHits, settings.earlyExitHits.value);
            if (settings.antiFlicker.value) {
                fusionMaterial.EnableKeyword(ShaderParams.SKW_ANTI_FLICKER);
            }
            else {
                fusionMaterial.DisableKeyword(ShaderParams.SKW_ANTI_FLICKER);
            }
            fusionMaterial.SetTexture(ShaderParams.NoiseTex3D, noiseTex);

            fusionFillMaterial.SetVector(ShaderParams.BlendData1, blendData1);
            fusionFillMaterial.SetVector(ShaderParams.BlendData2, blendData2);
            fusionFillMaterial.SetFloat(ShaderParams.DefaultRadiusWorld, settings.radius.value);
            fusionFillMaterial.SetFloat(ShaderParams.RadiusScale, radiusScale);
            fusionFillMaterial.SetFloat(ShaderParams.DistanceCompensation, settings.distanceCompensation.value);

            float msaaFixPower = settings.msaaEdgeFixPower.value;
            if (cameraUsesMsaa && msaaFixPower > 0f) {
                fusionMaterial.EnableKeyword(ShaderParams.SKW_MSAA_EDGE_FIX);
                fusionMaterial.SetFloat(ShaderParams.MsaaFixPower, msaaFixPower);
            }
            else {
                fusionMaterial.DisableKeyword(ShaderParams.SKW_MSAA_EDGE_FIX);
            }

            // ObjectID material setup
            objectIDMaterial.SetFloat(ShaderParams.MaxBlendDistance, effectiveMaxBlendDistance);
            objectIDMaterial.SetFloat(ShaderParams.DefaultRadiusWorld, settings.radius.value);
            objectIDMaterial.SetFloat(ShaderParams.RadiusScale, radiusScale);
            objectIDMaterial.SetFloat(ShaderParams.DistanceCompensation, settings.distanceCompensation.value);
            objectIDMaterial.SetVector(ShaderParams.BlendData2, blendData2);
            objectIDMaterial.SetInt(ShaderParams.ZWrite, cameraUsesMsaa ? 1 : 0);
            objectIDMaterial.SetInt(ShaderParams.ZTest, cameraUsesMsaa ? (int)CompareFunction.LessEqual : (int)CompareFunction.Equal);
            objectIDMaterial.SetFloat(ShaderParams.DisallowIntraFusion, settings.intraObjectFusionPerObject.value ? 1f : 0f);

            if (cameraUsesMsaa) {
                objectIDMaterial.EnableKeyword(ShaderParams.SKW_MSAA_ON);
            }
            else {
                objectIDMaterial.DisableKeyword(ShaderParams.SKW_MSAA_ON);
            }

            DebugMode debugMode = settings.debugMode.value;
            if (debugMode == DebugMode.SpecialGroup && !specialGroupActive) debugMode = DebugMode.None;
            fusionMaterial.SetInt(ShaderParams.DebugMode, (int)debugMode);
            fusionMaterial.SetFloat(ShaderParams.DepthDebugMultiplier, settings.depthDebugMultiplier.value);

            bool normalsDebug = debugMode == DebugMode.Normals;
            if (normalsDebug) {
                objectIDMaterial.EnableKeyword(ShaderParams.SKW_DEBUG_BLEND_NORMALS);
                fusionFillMaterial.EnableKeyword(ShaderParams.SKW_DEBUG_BLEND_NORMALS);
            }
            else {
                objectIDMaterial.DisableKeyword(ShaderParams.SKW_DEBUG_BLEND_NORMALS);
                fusionFillMaterial.DisableKeyword(ShaderParams.SKW_DEBUG_BLEND_NORMALS);
            }

            // Jitter keyword toggle
            if (settings.jitter.value) {
                fusionMaterial.EnableKeyword(ShaderParams.SKW_ENABLE_JITTER);
                fusionMaterial.SetFloat(ShaderParams.JitterFrame, taa ? Time.frameCount : 0);
            }
            else {
                fusionMaterial.DisableKeyword(ShaderParams.SKW_ENABLE_JITTER);
            }

            fusionMaterial.DisableKeyword(ShaderParams.SKW_INTRA_OBJECT_FUSION);
            fusionMaterial.DisableKeyword(ShaderParams.SKW_CONCAVE_ONLY);
            objectIDMaterial.DisableKeyword(ShaderParams.SKW_INTRA_OBJECT_FUSION);
            objectIDMaterial.DisableKeyword(ShaderParams.SKW_CONCAVE_ONLY);
            fusionFillMaterial.DisableKeyword(ShaderParams.SKW_INTRA_OBJECT_FUSION);
            fusionFillMaterial.DisableKeyword(ShaderParams.SKW_CONCAVE_ONLY);
            bool intraObjectFusionPerObject = settings.intraObjectFusionPerObject.value;
            if (settings.enableIntraObjectFusion.value) {
                if (settings.concavityTest.value) {
                    fusionMaterial.EnableKeyword(ShaderParams.SKW_CONCAVE_ONLY);
                    objectIDMaterial.EnableKeyword(ShaderParams.SKW_CONCAVE_ONLY);
                    if (!intraObjectFusionPerObject) {
                        fusionFillMaterial.EnableKeyword(ShaderParams.SKW_CONCAVE_ONLY);
                    }
                }
                else {
                    fusionMaterial.EnableKeyword(ShaderParams.SKW_INTRA_OBJECT_FUSION);
                    objectIDMaterial.EnableKeyword(ShaderParams.SKW_INTRA_OBJECT_FUSION);
                    if (!intraObjectFusionPerObject) {
                        fusionFillMaterial.EnableKeyword(ShaderParams.SKW_INTRA_OBJECT_FUSION);
                    }
                }
            }

            if (settings.noiseIntensity.value > 0.0f) {
                fusionMaterial.EnableKeyword(ShaderParams.SKW_NOISE);
            }
            else {
                fusionMaterial.DisableKeyword(ShaderParams.SKW_NOISE);
            }

            if (specialGroupActive) {
                fusionFillMaterial.EnableKeyword(ShaderParams.SKW_SPECIAL_GROUP);
            }
            else {
                fusionFillMaterial.DisableKeyword(ShaderParams.SKW_SPECIAL_GROUP);
            }

            var exclusionPairs = GetActiveExclusionPairs();
            if (exclusionPairs != null && exclusionPairs.Length > 0) {
                System.Array.Clear(exclusionMask, 0, 32);
                System.Array.Clear(exclusionMaskBits, 0, 32);
                bool hasEnabledPairs = false;
                int exclusionPairCount = exclusionPairs.Length;
                for (int i = 0; i < exclusionPairCount; i++) {
                    var pair = exclusionPairs[i];
                    if (!pair.enabled) continue;

                    hasEnabledPairs = true;
                    int a = Mathf.Clamp(pair.idA, 1, 32) - 1;
                    int b = Mathf.Clamp(pair.idB, 1, 32) - 1;
                    uint bitForA = 1u << b;
                    uint bitForB = 1u << a;

                    if (a == b) {
                        uint candidate = exclusionMaskBits[a] | bitForA;
                        if (CanStoreMaskValue(candidate)) {
                            exclusionMaskBits[a] = candidate;
                        }
                        continue;
                    }

                    uint candidateA = exclusionMaskBits[a] | bitForA;
                    uint candidateB = exclusionMaskBits[b] | bitForB;

                    bool canA = CanStoreMaskValue(candidateA);
                    bool canB = CanStoreMaskValue(candidateB);

                    if (canA && (!canB || candidateA <= candidateB)) {
                        exclusionMaskBits[a] = candidateA;
                    }
                    else if (canB) {
                        exclusionMaskBits[b] = candidateB;
                    }
                }

                if (hasEnabledPairs) {
                    for (int i = 0; i < 32; i++) {
                        uint maskValue = exclusionMaskBits[i];
                        exclusionMask[i] = maskValue;
                    }
                    fusionMaterial.EnableKeyword(ShaderParams.SKW_ID_EXCLUSION);
                    fusionMaterial.SetFloatArray(ShaderParams.ExclusionMask, exclusionMask);
                }
                else {
                    fusionMaterial.DisableKeyword(ShaderParams.SKW_ID_EXCLUSION);
                }

            }
            else {
                fusionMaterial.DisableKeyword(ShaderParams.SKW_ID_EXCLUSION);
            }
        }

        class PassData {
            public RendererListHandle rendererListSingle;
            public RendererListHandle rendererListDouble;
            public RendererListHandle rendererListCustom;
            public TextureHandle objectIDTexture;
            public TextureHandle customGroupTexture;
            public TextureHandle sourceColorTexture;
            public TextureHandle compareTexture;
            public Material material;
            public bool hasSingleSided;
            public bool hasDoubleSided;
            public bool useCustomGroups;
            public Vector4 compareParams;
            public Vector4 compareLineColor;
        }

        public override void RecordRenderGraph (RenderGraph renderGraph, ContextContainer frameData) {

            if (feature == null) return;
            var settings = VolumeManager.instance.stack.GetComponent<EdgeFusion>();
            if (settings == null || !settings.IsActive()) return;

            if (objectIDMaterial == null || fusionMaterial == null || fusionFillMaterial == null) {
                InitializeMaterials();
                if (objectIDMaterial == null || fusionMaterial == null || fusionFillMaterial == null) return;
            }

            var cameraData = frameData.Get<UniversalCameraData>();
            if (EdgeFusionRenderFeature.IsDepthOnlyTarget(cameraData.cameraTargetDescriptor)) return;

            var resourceData = frameData.Get<UniversalResourceData>();
            if (!resourceData.activeColorTexture.IsValid()) return;

            var renderingData = frameData.Get<UniversalRenderingData>();
            var lightingData = frameData.Get<UniversalLightData>();

            // Decide whether depth is MSAA by inspecting the cameraDepth handle; fallback to camera descriptor
            bool cameraUsesMsaa;
            if (resourceData.cameraDepth.IsValid()) {
                var cameraDepthDesc = renderGraph.GetTextureDesc(resourceData.cameraDepth);
                cameraUsesMsaa = cameraDepthDesc.msaaSamples != MSAASamples.None;
            }
            else {
                int cameraMsaaSamples = cameraData.cameraTargetDescriptor.msaaSamples;
                cameraUsesMsaa = cameraMsaaSamples > 1;
            }

            // Configure material properties and keywords
            bool taa = cameraData.antialiasing == AntialiasingMode.TemporalAntiAliasing && !cameraUsesMsaa;

            // Determine effective masks (respect camera culling). If neither SS nor DS has content, skip entirely.
            int cameraCullingMask = cameraData.camera.cullingMask;
            int effectiveBlendLayers = settings.blendLayers.value & cameraCullingMask;
            int effectiveDoubleSided = settings.doubleSidedLayers.value & cameraCullingMask;
            int effectiveSpecialGroup = settings.specialGroupLayers.value & cameraCullingMask;

            uint rlMaskSingle = settings.renderingLayerFilter.value;
            uint rlMaskDouble = settings.doubleSidedRenderingLayerFilter.value;
            uint rlMaskSpecial = settings.specialGroupRenderingLayerFilter.value;

            int ssMask = effectiveBlendLayers;
            int dsMask = effectiveDoubleSided;
            int specialMask = effectiveSpecialGroup;

            if ((dsMask & specialMask) != 0 && RenderingLayersOverlap(rlMaskSpecial, rlMaskDouble)) {
                dsMask &= ~specialMask;
            }

            if ((ssMask & specialMask) != 0 && RenderingLayersOverlap(rlMaskSpecial, rlMaskSingle)) {
                ssMask &= ~specialMask;
            }

            if ((ssMask & dsMask) != 0 && RenderingLayersOverlap(rlMaskDouble, rlMaskSingle)) {
                ssMask &= ~dsMask;
            }

            bool hasSingleSided = ssMask != 0 && rlMaskSingle != 0u;
            bool hasDoubleSided = dsMask != 0 && rlMaskDouble != 0u;
            bool hasSpecialGroup = specialMask != 0 && rlMaskSpecial != 0u;
            uint specialGroupCombined = hasSpecialGroup ? rlMaskSpecial : 0u;

            // For orthographic cameras, use a very high blend distance to avoid depth culling
            float effectiveMaxBlendDistance = settings.maxBlendDistance.value;
            if (cameraData.camera.orthographic) {
                effectiveMaxBlendDistance = 1e10f;
            }

            UpdateMaterialProperties(settings, taa, effectiveMaxBlendDistance, cameraUsesMsaa, hasSpecialGroup);

            // Keep a handle to camera color before we rebind it for our output
            TextureHandle preEffectColor = resourceData.cameraColor;

            // Camera-stack safe path: a stacked base shares its color attachment with the overlays,
            // so reassigning cameraColor would orphan the skybox/transparents drawn after this event
            bool isStackedBase = cameraData.camera.TryGetComponent(out UniversalAdditionalCameraData additionalCameraData)
                && additionalCameraData.cameraStack != null && additionalCameraData.cameraStack.Count > 0;

            TextureHandle cameraColorTarget;
            TextureHandle copyBackTarget = TextureHandle.nullHandle;
            if (preEffectColor.IsValid() && isStackedBase) {
                var stackColorDesc = renderGraph.GetTextureDesc(preEffectColor);
                stackColorDesc.name = CAMERA_COLOR_TEXTURE_NAME;
                cameraColorTarget = renderGraph.CreateTexture(stackColorDesc);
                copyBackTarget = preEffectColor;
            }
            else {
                // Allocate a new camera color as our output to avoid read/write hazards without extra passes
                if (preEffectColor.IsValid()) {
                    var newCameraColorDesc = renderGraph.GetTextureDesc(preEffectColor);
                    newCameraColorDesc.name = CAMERA_COLOR_TEXTURE_NAME;
                    var newCameraColor = renderGraph.CreateTexture(newCameraColorDesc);
                    resourceData.cameraColor = newCameraColor;
                }
                cameraColorTarget = resourceData.activeColorTexture;
            }

            // Source color for blending: the old camera color
            TextureHandle sourceColorForBlend = preEffectColor;

            // Compare mode active?
            bool compareActive = settings.compareMode.value && settings.debugMode.value == DebugMode.None;
            TextureHandle compareTexture = TextureHandle.nullHandle;
            if (compareActive) {
                var compareDesc = renderGraph.GetTextureDesc(cameraColorTarget.IsValid() ? cameraColorTarget : preEffectColor);
                compareDesc.name = COMPARE_TEXTURE_NAME;
                compareTexture = renderGraph.CreateTexture(compareDesc);
            }

            // Create main ObjectID texture (RGB 32-bit float for ID, depth, and radius)
            var objIdDescUnified = renderGraph.GetTextureDesc(resourceData.cameraColor.IsValid() ? resourceData.cameraColor : preEffectColor);
            var forcedRGBA32F = settings.enableIntraObjectFusion.value || settings.debugMode.value == DebugMode.Normals;
            objIdDescUnified.colorFormat = forcedRGBA32F ? UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32B32A32_SFloat : objectIdTextureFormat;
            objIdDescUnified.depthBufferBits = DepthBits.None;
            objIdDescUnified.clearColor = SystemInfo.usesReversedZBuffer ? Color.clear : new Color(0, 1, 0, 0);
            objIdDescUnified.clearBuffer = true;
            objIdDescUnified.name = OBJECT_ID_TEXTURE_NAME;
            objIdDescUnified.msaaSamples = MSAASamples.None;
            objIdDescUnified.bindTextureMS = false;
            TextureHandle objectIDTexture = renderGraph.CreateTexture(objIdDescUnified);

            // Special group marker texture (black by default). Same size as camera color, no MSAA, no depth.
            bool useCustomGroups = hasSpecialGroup;
            TextureHandle customGroupTexture = TextureHandle.nullHandle;
            if (useCustomGroups) {
                var customDesc = objIdDescUnified;
                customDesc.clearColor = Color.black;
                customDesc.clearBuffer = true;
                customDesc.depthBufferBits = DepthBits.None;
                customDesc.name = CUSTOM_GROUP_TEXTURE_NAME;
                customGroupTexture = renderGraph.CreateTexture(customDesc);
            }

            // When MSAA, bind fresh depth with Write access; otherwise read-only camera depth
            TextureHandle depthForObjectID = TextureHandle.nullHandle;
            AccessFlags accessFlags = AccessFlags.Read;
            if (cameraUsesMsaa) {
                var objectIDDepthDesc = resourceData.cameraDepth.IsValid()
                    ? renderGraph.GetTextureDesc(resourceData.cameraDepth)
                    : renderGraph.GetTextureDesc(resourceData.cameraColor.IsValid() ? resourceData.cameraColor : preEffectColor);
                objectIDDepthDesc.name = OBJECT_ID_DEPTH_TEXTURE_NAME;
                objectIDDepthDesc.msaaSamples = MSAASamples.None;
                objectIDDepthDesc.bindTextureMS = false;
                objectIDDepthDesc.clearBuffer = true;
                if (!resourceData.cameraDepth.IsValid()) {
                    objectIDDepthDesc.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.None;
                    objectIDDepthDesc.depthBufferBits = DepthBits.Depth24;
                }
                depthForObjectID = renderGraph.CreateTexture(objectIDDepthDesc);
                accessFlags = AccessFlags.Write;
            }
            else {
                // No MSAA: bind camera depth directly
                depthForObjectID = resourceData.cameraDepth;
                accessFlags = AccessFlags.Read;
            }

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(OBJECT_ID_PASS_NAME, out var passData)) {
                builder.AllowGlobalStateModification(true);
                builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                builder.SetRenderAttachment(objectIDTexture, 0);
                builder.SetRenderAttachmentDepth(depthForObjectID, accessFlags);

                var drawSettings = CreateDrawingSettings(shaderTags, renderingData, cameraData, lightingData, SortingCriteria.CommonOpaque);
                drawSettings.overrideMaterial = objectIDMaterial;
                drawSettings.overrideMaterialPassIndex = 0;
                drawSettings.perObjectData = PerObjectData.None;
                drawSettings.enableInstancing = true;

                passData.material = objectIDMaterial;

                passData.hasSingleSided = hasSingleSided;
                passData.hasDoubleSided = hasDoubleSided;
                if (passData.hasSingleSided) {
                    var filterSingle = new FilteringSettings(RenderQueueRange.opaque, ssMask, rlMaskSingle);
                    var rlParamsSingle = new RendererListParams(renderingData.cullResults, drawSettings, filterSingle);
                    passData.rendererListSingle = renderGraph.CreateRendererList(rlParamsSingle);
                    builder.UseRendererList(passData.rendererListSingle);
                }
                if (passData.hasDoubleSided) {
                    var filterDouble = new FilteringSettings(RenderQueueRange.opaque, dsMask, rlMaskDouble);
                    var rlParamsDouble = new RendererListParams(renderingData.cullResults, drawSettings, filterDouble);
                    passData.rendererListDouble = renderGraph.CreateRendererList(rlParamsDouble);
                    builder.UseRendererList(passData.rendererListDouble);
                }

                builder.SetRenderFunc((PassData data, RasterGraphContext ctx) => {
                    if (data.hasSingleSided) {
                        ctx.cmd.SetGlobalInt(ShaderParams.Cull, (int)CullMode.Back);
                        ctx.cmd.DrawRendererList(data.rendererListSingle);
                    }
                    if (data.hasDoubleSided) {
                        ctx.cmd.SetGlobalInt(ShaderParams.Cull, (int)CullMode.Off);
                        ctx.cmd.DrawRendererList(data.rendererListDouble);
                        ctx.cmd.SetGlobalInt(ShaderParams.Cull, (int)CullMode.Back);
                    }
                });
            }

            // Populate special group texture by drawing special-layer renderers.
            if (useCustomGroups) {
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(SPECIAL_GROUP_PASS_NAME, out var passData)) {
                    passData.customGroupTexture = customGroupTexture;
                    passData.material = fusionMaterial;
                    passData.useCustomGroups = true;

                    // Bind depth for testing (LEqual), write to custom color
                    builder.SetRenderAttachment(customGroupTexture, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(depthForObjectID, AccessFlags.Read);

                    var drawSettings = CreateDrawingSettings(shaderTags, renderingData, cameraData, lightingData, SortingCriteria.CommonOpaque);
                    drawSettings.overrideMaterial = null; // use original materials to preserve displacement
                    drawSettings.enableInstancing = true;

                    var filterCustom = new FilteringSettings(RenderQueueRange.opaque, specialMask, specialGroupCombined);
                    var rlParams = new RendererListParams(renderingData.cullResults, drawSettings, filterCustom);
                    passData.rendererListCustom = renderGraph.CreateRendererList(rlParams);
                    builder.UseRendererList(passData.rendererListCustom);

                    builder.SetRenderFunc((PassData data, RasterGraphContext ctx) => {
                        ctx.cmd.DrawRendererList(data.rendererListCustom);
                    });
                }
            }

            // Fill Object ID texture. Execute when not all visible objects are being rendered to the ObjectID texture.
            int layerUnion = ssMask | dsMask;
            bool layersExcluded = layerUnion != cameraCullingMask;
            bool renderingLayersExcluded = (rlMaskSingle != uint.MaxValue) || (rlMaskDouble != uint.MaxValue) || (rlMaskSpecial != uint.MaxValue);
            bool needsOthersFill = (layersExcluded || renderingLayersExcluded) && settings.blendWithOthers.value;
            if (useCustomGroups || needsOthersFill) {
                // Set terrain object ID: 31 if blending with others, 0 otherwise
                fusionFillMaterial.SetFloat(ShaderParams.TerrainObjectId, settings.blendWithOthers.value ? 31f : 0f);
                // Create a temporary texture for the fill pass output
                var objectIDFilledDesc = renderGraph.GetTextureDesc(objectIDTexture);
                objectIDFilledDesc.name = "EdgeFusion_ObjectID_Filled";
                TextureHandle objectIDFilled = renderGraph.CreateTexture(objectIDFilledDesc);

                using (var builder = renderGraph.AddRasterRenderPass<PassData>(FILL_OBJECT_ID_PASS_NAME, out var passData)) {
                    passData.objectIDTexture = objectIDTexture;
                    passData.material = fusionFillMaterial;
                    passData.customGroupTexture = customGroupTexture;
                    passData.useCustomGroups = useCustomGroups;

                    builder.AllowGlobalStateModification(true);

                    builder.UseTexture(objectIDTexture, AccessFlags.Read);
                    builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                    if (useCustomGroups) builder.UseTexture(customGroupTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(objectIDFilled, 0, AccessFlags.Write);

                    builder.SetRenderFunc((PassData data, RasterGraphContext ctx) => {
                        ctx.cmd.SetGlobalTexture(ShaderParams.ObjectIDTexture, data.objectIDTexture);
                        if (data.useCustomGroups) {
                            ctx.cmd.SetGlobalTexture(ShaderParams.CustomGroupTexture, data.customGroupTexture);
                        }
                        CoreUtils.DrawFullScreen(ctx.cmd, data.material, null, ShaderParams.FillPassIndex);
                    });
                }

                // Use filled texture for subsequent passes
                objectIDTexture = objectIDFilled;
            }

            // Final blend
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(BLEND_FORWARD_PASS_NAME, out var passData)) {
                passData.objectIDTexture = objectIDTexture;
                passData.material = fusionMaterial;
                passData.sourceColorTexture = sourceColorForBlend;

                builder.AllowGlobalStateModification(true);

                builder.UseTexture(passData.objectIDTexture, AccessFlags.Read);
                builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                if (resourceData.cameraNormalsTexture.IsValid()) {
                    builder.UseTexture(resourceData.cameraNormalsTexture, AccessFlags.Read);
                }
                if (passData.sourceColorTexture.IsValid()) {
                    builder.UseTexture(passData.sourceColorTexture, AccessFlags.Read);
                }

                // When comparing, write blend output to a temporary compare texture; otherwise write to camera color
                if (compareActive && compareTexture.IsValid()) {
                    builder.SetRenderAttachment(compareTexture, 0, AccessFlags.Write);
                }
                else {
                    builder.SetRenderAttachment(cameraColorTarget, 0, AccessFlags.Write);
                }

                builder.SetRenderFunc((PassData data, RasterGraphContext ctx) => {
                    if (data.sourceColorTexture.IsValid()) {
                        ctx.cmd.SetGlobalTexture(ShaderParams.SrcColorTexture, data.sourceColorTexture);
                    }
                    ctx.cmd.SetGlobalTexture(ShaderParams.ObjectIDTexture, data.objectIDTexture);
                    CoreUtils.DrawFullScreen(ctx.cmd, data.material, null, (int)ShaderParams.Passes.Blend);
                });
            }

            // Compare pass
            if (compareActive && compareTexture.IsValid()) {
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(COMPARE_PASS_NAME, out var passData)) {
                    passData.sourceColorTexture = sourceColorForBlend; // original
                    passData.material = fusionMaterial;

                    builder.AllowGlobalStateModification(true);

                    builder.UseTexture(compareTexture, AccessFlags.Read);
                    if (passData.sourceColorTexture.IsValid()) {
                        builder.UseTexture(passData.sourceColorTexture, AccessFlags.Read);
                    }
                    builder.SetRenderAttachment(cameraColorTarget, 0, AccessFlags.Write);

                    // Precompute compare params (cos, sin, panning or sentinel, line width)
                    float angle = settings.compareSameSide.value ? Mathf.PI * 0.5f : settings.compareLineAngle.value;
                    Vector4 compareParamsValue = new Vector4(Mathf.Cos(angle), Mathf.Sin(angle), settings.compareSameSide.value ? settings.comparePanning.value : -10f, settings.compareLineWidth.value);
                    Color lineColor = settings.compareLineColor.value;
                    Vector4 compareLineColor = new Vector4(lineColor.r, lineColor.g, lineColor.b, lineColor.a);

                    // Store in pass data to avoid lambda capture allocations
                    passData.compareTexture = compareTexture;
                    passData.compareParams = compareParamsValue;
                    passData.compareLineColor = compareLineColor;

                    builder.SetRenderFunc((PassData data, RasterGraphContext ctx) => {
                        if (data.sourceColorTexture.IsValid()) {
                            ctx.cmd.SetGlobalTexture(ShaderParams.SrcColorTexture, data.sourceColorTexture);
                        }
                        ctx.cmd.SetGlobalTexture(ShaderParams.CompareTex, data.compareTexture);
                        ctx.cmd.SetGlobalVector(ShaderParams.CompareParams, data.compareParams);
                        ctx.cmd.SetGlobalVector(ShaderParams.CompareLineColor, data.compareLineColor);
                        CoreUtils.DrawFullScreen(ctx.cmd, data.material, null, (int)ShaderParams.Passes.Compare);
                    });
                }
            }

            // Debug
            if (!compareActive && settings.debugMode.value != DebugMode.None) {
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(DEBUG_PASS_NAME, out var passData)) {
                    passData.objectIDTexture = objectIDTexture;
                    passData.material = fusionMaterial;

                    builder.AllowGlobalStateModification(true);

                    builder.UseTexture(passData.objectIDTexture, AccessFlags.Read);
                    builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                    if (resourceData.cameraNormalsTexture.IsValid()) {
                        builder.UseTexture(resourceData.cameraNormalsTexture, AccessFlags.Read);
                    }
                    builder.SetRenderAttachment(cameraColorTarget, 0, AccessFlags.Write);
                    builder.SetRenderFunc((PassData data, RasterGraphContext ctx) => {
                        ctx.cmd.SetGlobalTexture(ShaderParams.ObjectIDTexture, data.objectIDTexture);
                        CoreUtils.DrawFullScreen(ctx.cmd, data.material, null, (int)ShaderParams.Passes.Debug);
                    });
                }
            }

            // Stacked base: copy our result back into the original camera color
            if (copyBackTarget.IsValid()) {
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(COPY_BACK_PASS_NAME, out var passData)) {
                    passData.sourceColorTexture = cameraColorTarget;
                    builder.AllowGlobalStateModification(true);
                    builder.UseTexture(cameraColorTarget, AccessFlags.Read);
                    builder.SetRenderAttachment(copyBackTarget, 0, AccessFlags.Write);
                    builder.SetRenderFunc((PassData data, RasterGraphContext ctx) => {
                        Blitter.BlitTexture(ctx.cmd, data.sourceColorTexture, new Vector4(1f, 1f, 0f, 0f), 0f, false);
                    });
                }
            }

        }

        static bool RenderingLayersOverlap (uint maskA, uint maskB) {
            if (maskA == 0u || maskB == 0u) return false;
            if (maskA == uint.MaxValue || maskB == uint.MaxValue) return true;
            return (maskA & maskB) != 0u;
        }

        static IdExclusionPair[] GetActiveExclusionPairs () {
            if (cachedExclusionVolume != null && cachedExclusionVolume.isActiveAndEnabled && cachedEdgeFusion != null) {
                return cachedEdgeFusion.idExclusionPairs;
            }
            cachedExclusionVolume = null;
            cachedEdgeFusion = null;
#if UNITY_6000_4_OR_NEWER
            var volumes = Object.FindObjectsByType<Volume>();
#else
            var volumes = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
#endif
            int volumeCount = volumes.Length;
            for (int i = 0; i < volumeCount; i++) {
                var volume = volumes[i];
                if (volume == null || !volume.isActiveAndEnabled || volume.sharedProfile == null) continue;
                if (!volume.sharedProfile.TryGet<EdgeFusion>(out var ef)) continue;
                if (ef.idExclusionPairs != null) {
                    cachedExclusionVolume = volume;
                    cachedEdgeFusion = ef;
                    return cachedEdgeFusion.idExclusionPairs;
                }
            }
            return null;
        }

        public void Cleanup () {
            CoreUtils.Destroy(objectIDMaterial);
            CoreUtils.Destroy(fusionMaterial);
            CoreUtils.Destroy(fusionFillMaterial);
        }
    }

}