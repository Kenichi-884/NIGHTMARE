//#define URP_10_0_0_OR_NEWER
//#define UNITY_2021_2_OR_NEWER

#pragma warning disable CS0618
#pragma warning disable CS0672

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace ScreenSpaceCavityCurvature.Universal
{
    public class SSCCRendererFeature : ScriptableRendererFeature
    {
        private class SSCCRenderPass : ScriptableRenderPass
        {
            void CheckParameters()
            {
#if !URP_10_0_0_OR_NEWER
                if (sscc.normalsSource.value == SSCC.PerPixelNormals.Camera_URP_VER_TOO_LOW)
                {
                    sscc.normalsSource.value = SSCC.PerPixelNormals.ReconstructedFromDepth;
                    Debug.LogWarning("URP version too low for Camera based normals, only available in URP 10+ (Unity 2020+).");
                }
                if (sscc.output.value == SSCC.OutputEffectTo._SSCCTexture)
                {
                    sscc.output.value = SSCC.OutputEffectTo.Screen;
                    Debug.LogWarning("URP version too low for texture output mode, only available in URP 10+ (Unity 2020+).");
                }
#endif
            }

            SSCC.OutputEffectTo Output => (sscc != null && sscc.debugMode.value != SSCC.DebugMode.Disabled) ? SSCC.OutputEffectTo.Screen : (sscc != null ? sscc.output.value : SSCC.OutputEffectTo.Screen);

            public SSCC sscc;

            static class Pass
            {
                public const int Copy = 0;
                public const int GenerateCavity = 1;
                public const int HorizontalBlur = 2;
                public const int VerticalBlur = 3;
                public const int Final = 4;
            }

            static class ShaderProperties
            {
                public static int mainTex = Shader.PropertyToID("_MainTex");
                public static int cavityTex = Shader.PropertyToID("_CavityTex");
                public static int cavityTex1 = Shader.PropertyToID("_CavityTex1");
                public static int tempTex = Shader.PropertyToID("_TempTex");
                public static int uvTransform = Shader.PropertyToID("_UVTransform");
                public static int inputTexelSize = Shader.PropertyToID("_Input_TexelSize");
                public static int cavityTexTexelSize = Shader.PropertyToID("_CavityTex_TexelSize");
                public static int worldToCameraMatrix = Shader.PropertyToID("_WorldToCameraMatrix");

                public static int effectIntensity = Shader.PropertyToID("_EffectIntensity");
                public static int distanceFade = Shader.PropertyToID("_DistanceFade");

                public static int curvaturePixelRadius = Shader.PropertyToID("_CurvaturePixelRadius");
                public static int curvatureRidge = Shader.PropertyToID("_CurvatureBrights");
                public static int curvatureValley = Shader.PropertyToID("_CurvatureDarks");

                public static int cavityWorldRadius = Shader.PropertyToID("_CavityWorldRadius");
                public static int cavityRidge = Shader.PropertyToID("_CavityBrights");
                public static int cavityValley = Shader.PropertyToID("_CavityDarks");

                public static int globalSSCCTexture = Shader.PropertyToID("_SSCCTexture");
            }

            Material mat { get; set; }
            RenderTargetIdentifier source { get; set; }
            CameraData cameraData { get; set; }
            RenderTextureDescriptor sourceDesc { get; set; }

            public void Setup(Shader shader, ScriptableRenderer renderer, RenderingData renderingData)
            {
                if (mat == null) mat = CoreUtils.CreateEngineMaterial(shader);

                FetchVolumeComponent();

                if (sscc != null && sscc.IsActive())
                {
                    if (Output == SSCC.OutputEffectTo.Screen)
                    {
                        renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
                    }
                    else
                    {
#if UNITY_6000_0_OR_NEWER
                        // AfterRenderingPrePasses is the earliest point where depth/normals
                        // from the prepass are available. Using BeforeRenderingOpaques causes
                        // the Render Graph to defer the pass until after opaques when it sees
                        // the dependency on cameraDepthTexture.
                        renderPassEvent = RenderPassEvent.AfterRenderingPrePasses;
#else
                        renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
#endif
                    }
                }

#if !URP_10_0_0_OR_NEWER
                source = renderer.cameraColorTarget;
                cameraData = renderingData.cameraData;
#endif
            }

#if URP_10_0_0_OR_NEWER
            #if UNITY_2022_1_OR_NEWER
            static RTHandle noneRTHandle = RTHandles.Alloc(BuiltinRenderTextureType.None);
            #endif
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                #if UNITY_2022_1_OR_NEWER
                source = renderingData.cameraData.renderer.cameraColorTargetHandle;
                #else
                source = renderingData.cameraData.renderer.cameraColorTarget;
                #endif
                cameraData = renderingData.cameraData;

                FetchVolumeComponent();

                var passInput = ScriptableRenderPassInput.Depth;
                if (sscc != null && sscc.normalsSource.value == SSCC.PerPixelNormals.Camera)
                    passInput |= ScriptableRenderPassInput.Normal;

                ConfigureInput(passInput);

#if UNITY_2021_2_OR_NEWER
                ConfigureColorStoreAction(RenderBufferStoreAction.DontCare);
#endif

                if (Output == SSCC.OutputEffectTo._SSCCTexture)
                #if UNITY_2022_1_OR_NEWER
                    ConfigureTarget(noneRTHandle);
                #else
                    ConfigureTarget(BuiltinRenderTextureType.None);
                #endif
            }
#endif

            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                if (mat == null) return;

                FetchVolumeComponent();

                if (sscc == null || !sscc.IsActive()) return;

                cameraTextureDescriptor.msaaSamples = 1;
                cameraTextureDescriptor.depthBufferBits = 0;
                sourceDesc = cameraTextureDescriptor;

                CheckParameters();
                UpdateMaterialProperties(cameraData.camera, sourceDesc.width, sourceDesc.height);
                UpdateShaderKeywords(cameraData.camera);
                
                if (Output == SSCC.OutputEffectTo._SSCCTexture)
                #if UNITY_2022_1_OR_NEWER
                    ConfigureTarget(noneRTHandle);
                #else
                    ConfigureTarget(BuiltinRenderTextureType.None);
                #endif
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (mat == null)
                {
                    Debug.LogError("SSCC material has not been correctly initialized...");
                    return;
                }
                if (sscc == null || !sscc.IsActive()) return;
                var cmd = CommandBufferPool.Get("SSCC");

                int div = sscc.cavityResolution.value == SSCC.CavityResolution.Full ? 1 : sscc.cavityResolution.value == SSCC.CavityResolution.HalfUpscaled ? 2 : 2;
                cmd.GetTemporaryRT(ShaderProperties.cavityTex, sourceDesc.width / div, sourceDesc.height / div, 0, FilterMode.Bilinear, GraphicsFormat.R32G32B32A32_SFloat);
                cmd.GetTemporaryRT(ShaderProperties.cavityTex1, sourceDesc.width / div, sourceDesc.height / div, 0, FilterMode.Bilinear, GraphicsFormat.R32G32B32A32_SFloat);
                Render(ShaderProperties.cavityTex, cmd, mat, Pass.GenerateCavity);

                if (sscc.cavityResolution.value == SSCC.CavityResolution.HalfUpscaled)
                {
                    RenderWith(ShaderProperties.cavityTex, ShaderProperties.cavityTex1, cmd, mat, Pass.HorizontalBlur);
                    RenderWith(ShaderProperties.cavityTex1, ShaderProperties.cavityTex, cmd, mat, Pass.VerticalBlur);
                }

                if (Output == SSCC.OutputEffectTo._SSCCTexture)
                {
                    cmd.ReleaseTemporaryRT(ShaderProperties.globalSSCCTexture);
                    cmd.GetTemporaryRT(ShaderProperties.globalSSCCTexture, sourceDesc.width, sourceDesc.height, 0, FilterMode.Bilinear, GraphicsFormat.R16G16B16A16_SFloat);
                    cmd.SetGlobalTexture(ShaderProperties.globalSSCCTexture, new RenderTargetIdentifier(ShaderProperties.globalSSCCTexture));
                    Render(ShaderProperties.globalSSCCTexture, cmd, mat, Pass.Final);
                }
                else
                {
                    cmd.GetTemporaryRT(ShaderProperties.tempTex, sourceDesc);
                    RenderWith(source, ShaderProperties.tempTex, cmd, mat, Pass.Copy);
                    RenderWith(ShaderProperties.tempTex, source, cmd, mat, Pass.Final);
                    cmd.ReleaseTemporaryRT(ShaderProperties.tempTex);
                }
                
                cmd.ReleaseTemporaryRT(ShaderProperties.cavityTex);
                cmd.ReleaseTemporaryRT(ShaderProperties.cavityTex1);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

#if UNITY_6000_0_OR_NEWER
            private class CavityPassData { public Material mat; public Mesh mesh; public TextureHandle cavityTex; }
            private class CopyPassData { public Material mat; public Mesh mesh; public TextureHandle source; public TextureHandle destination; }
            private class BlurPassData { public Material mat; public Mesh mesh; public TextureHandle source; public TextureHandle destination; }
            private class FinalPassData { public Material mat; public Mesh mesh; public TextureHandle mainTex; public TextureHandle cavityTex; public TextureHandle destination; }
            private class FinalToTexturePassData { public Material mat; public Mesh mesh; public TextureHandle cavityTex; public TextureHandle destination; }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (mat == null)
                {
                    Debug.LogError("SSCC material has not been correctly initialized...");
                    return;
                }

                FetchVolumeComponent();
                if (sscc == null || !sscc.IsActive()) return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalCameraData universalCameraData = frameData.Get<UniversalCameraData>();

                CheckParameters();

                RenderTextureDescriptor desc = universalCameraData.cameraTargetDescriptor;
                desc.msaaSamples = 1;
                desc.depthBufferBits = 0;

                UpdateMaterialProperties(universalCameraData.camera, desc.width, desc.height);
                UpdateShaderKeywords(universalCameraData.camera);

                int div = sscc.cavityResolution.value == SSCC.CavityResolution.Full ? 1 : 2;

                RenderTextureDescriptor cavityDesc = new RenderTextureDescriptor(Mathf.Max(1, desc.width / div), Mathf.Max(1, desc.height / div), GraphicsFormat.R32G32B32A32_SFloat, 0) { msaaSamples = 1 };

                TextureHandle cavityTex = UniversalRenderer.CreateRenderGraphTexture(renderGraph, cavityDesc, "_SSCCCavityTex", false, FilterMode.Bilinear);

                Mesh passMesh = fullscreenMesh;

                using (IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass<CavityPassData>("SSCC Generate Cavity", out CavityPassData passData))
                {
                    passData.mat = mat;
                    passData.mesh = passMesh;
                    passData.cavityTex = cavityTex;

                    builder.UseTexture(cavityTex, AccessFlags.Write);

                    if (resourceData.cameraDepthTexture.IsValid())
                        builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                    if (sscc.normalsSource.value == SSCC.PerPixelNormals.Camera && resourceData.cameraNormalsTexture.IsValid())
                        builder.UseTexture(resourceData.cameraNormalsTexture, AccessFlags.Read);

                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc(static (CavityPassData data, UnsafeGraphContext ctx) =>
                    {
                        CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);
                        cmd.SetRenderTarget(data.cavityTex, 0, CubemapFace.Unknown, -1);
                        cmd.DrawMesh(data.mesh, Matrix4x4.identity, data.mat, 0, Pass.GenerateCavity);
                    });
                }

                if (sscc.cavityResolution.value == SSCC.CavityResolution.HalfUpscaled)
                {
                    TextureHandle cavityTex1 = UniversalRenderer.CreateRenderGraphTexture(renderGraph, cavityDesc, "_SSCCCavityTex1", false, FilterMode.Bilinear);

                    using (IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass<BlurPassData>("SSCC Horizontal Blur", out BlurPassData passData))
                    {
                        passData.mat = mat;
                        passData.mesh = passMesh;
                        passData.source = cavityTex;
                        passData.destination = cavityTex1;

                        builder.UseTexture(cavityTex, AccessFlags.Read);
                        builder.UseTexture(cavityTex1, AccessFlags.Write);
                        builder.AllowGlobalStateModification(true);

                        builder.SetRenderFunc(static (BlurPassData data, UnsafeGraphContext ctx) =>
                        {
                            CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);
                            cmd.SetGlobalTexture(ShaderProperties.mainTex, data.source);
                            cmd.SetRenderTarget(data.destination, 0, CubemapFace.Unknown, -1);
                            cmd.DrawMesh(data.mesh, Matrix4x4.identity, data.mat, 0, Pass.HorizontalBlur);
                        });
                    }

                    using (IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass<BlurPassData>("SSCC Vertical Blur", out BlurPassData passData))
                    {
                        passData.mat = mat;
                        passData.mesh = passMesh;
                        passData.source = cavityTex1;
                        passData.destination = cavityTex;

                        builder.UseTexture(cavityTex1, AccessFlags.Read);
                        builder.UseTexture(cavityTex, AccessFlags.Write);
                        builder.AllowGlobalStateModification(true);

                        builder.SetRenderFunc(static (BlurPassData data, UnsafeGraphContext ctx) =>
                        {
                            CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);
                            cmd.SetGlobalTexture(ShaderProperties.mainTex, data.source);
                            cmd.SetRenderTarget(data.destination, 0, CubemapFace.Unknown, -1);
                            cmd.DrawMesh(data.mesh, Matrix4x4.identity, data.mat, 0, Pass.VerticalBlur);
                        });
                    }
                }

                if (Output == SSCC.OutputEffectTo._SSCCTexture)
                {
                    RenderTextureDescriptor ssccTexDesc = new RenderTextureDescriptor(desc.width, desc.height, GraphicsFormat.R16G16B16A16_SFloat, 0) { msaaSamples = 1 };

                    TextureHandle ssccTex = UniversalRenderer.CreateRenderGraphTexture(renderGraph, ssccTexDesc, "_SSCCTexture", false, FilterMode.Bilinear);

                    using (IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass<FinalToTexturePassData>("SSCC Final (Texture)", out FinalToTexturePassData passData))
                    {
                        passData.mat = mat;
                        passData.mesh = passMesh;
                        passData.cavityTex = cavityTex;
                        passData.destination = ssccTex;

                        builder.UseTexture(cavityTex, AccessFlags.Read);
                        builder.UseTexture(ssccTex, AccessFlags.Write);

                        if (resourceData.cameraDepthTexture.IsValid())
                            builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                        if (sscc.normalsSource.value == SSCC.PerPixelNormals.Camera && resourceData.cameraNormalsTexture.IsValid())
                            builder.UseTexture(resourceData.cameraNormalsTexture, AccessFlags.Read);

                        builder.AllowGlobalStateModification(true);

                        // Instruct the Render Graph engine to persist and attach this Texture property beyond the graph
                        builder.SetGlobalTextureAfterPass(ssccTex, ShaderProperties.globalSSCCTexture);

                        builder.SetRenderFunc(static (FinalToTexturePassData data, UnsafeGraphContext ctx) =>
                        {
                            CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);
                            cmd.SetGlobalTexture(ShaderProperties.cavityTex, data.cavityTex);
                            cmd.SetRenderTarget(data.destination, 0, CubemapFace.Unknown, -1);
                            cmd.DrawMesh(data.mesh, Matrix4x4.identity, data.mat, 0, Pass.Final);
                        });
                    }
                }
                else
                {
                    TextureHandle cameraColor = resourceData.activeColorTexture;
                    TextureHandle tempTex = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_SSCCTemp", false, FilterMode.Bilinear);

                    using (IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass<CopyPassData>("SSCC Copy", out CopyPassData passData))
                    {
                        passData.mat = mat;
                        passData.mesh = passMesh;
                        passData.source = cameraColor;
                        passData.destination = tempTex;

                        builder.UseTexture(cameraColor, AccessFlags.Read);
                        builder.UseTexture(tempTex, AccessFlags.Write);
                        builder.AllowGlobalStateModification(true);

                        builder.SetRenderFunc(static (CopyPassData data, UnsafeGraphContext ctx) =>
                        {
                            CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);
                            cmd.SetGlobalTexture(ShaderProperties.mainTex, data.source);
                            cmd.SetRenderTarget(data.destination, 0, CubemapFace.Unknown, -1);
                            cmd.DrawMesh(data.mesh, Matrix4x4.identity, data.mat, 0, Pass.Copy);
                        });
                    }

                    using (IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass<FinalPassData>("SSCC Final", out FinalPassData passData))
                    {
                        passData.mat = mat;
                        passData.mesh = passMesh;
                        passData.mainTex = tempTex;
                        passData.cavityTex = cavityTex;
                        passData.destination = cameraColor;

                        builder.UseTexture(tempTex, AccessFlags.Read);
                        builder.UseTexture(cavityTex, AccessFlags.Read);
                        builder.UseTexture(cameraColor, AccessFlags.Write);

                        if (resourceData.cameraDepthTexture.IsValid())
                            builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                        if (sscc.normalsSource.value == SSCC.PerPixelNormals.Camera && resourceData.cameraNormalsTexture.IsValid())
                            builder.UseTexture(resourceData.cameraNormalsTexture, AccessFlags.Read);

                        builder.AllowGlobalStateModification(true);

                        builder.SetRenderFunc(static (FinalPassData data, UnsafeGraphContext ctx) =>
                        {
                            CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);
                            cmd.SetGlobalTexture(ShaderProperties.mainTex, data.mainTex);
                            cmd.SetGlobalTexture(ShaderProperties.cavityTex, data.cavityTex);
                            cmd.SetRenderTarget(data.destination, 0, CubemapFace.Unknown, -1);
                            cmd.DrawMesh(data.mesh, Matrix4x4.identity, data.mat, 0, Pass.Final);
                        });
                    }
                }
            }
#endif
            
            public void Cleanup()
            {
                CoreUtils.Destroy(mat);
            }

            private void FetchVolumeComponent()
            {
                if (sscc == null)
                    sscc = VolumeManager.instance.stack.GetComponent<SSCC>();
            }
            
            void UpdateMaterialProperties(Camera camera, int sourceWidth, int sourceHeight)
            {
                mat.SetVector(ShaderProperties.inputTexelSize, new Vector4(1f / sourceWidth, 1f / sourceHeight, sourceWidth, sourceHeight));
                int div = sscc.cavityResolution.value == SSCC.CavityResolution.Full ? 1 : sscc.cavityResolution.value == SSCC.CavityResolution.HalfUpscaled ? 2 : 2;
                mat.SetVector(ShaderProperties.cavityTexTexelSize, new Vector4(1f / (sourceWidth / div), 1f / (sourceHeight / div), sourceWidth / div, sourceHeight / div));
                mat.SetMatrix(ShaderProperties.worldToCameraMatrix, camera.worldToCameraMatrix);

                mat.SetFloat(ShaderProperties.effectIntensity, sscc.effectIntensity.value);
                mat.SetFloat(ShaderProperties.distanceFade, sscc.distanceFade.value);

                mat.SetFloat(ShaderProperties.curvaturePixelRadius, new float[] { 0f, 0.5f, 1f, 1.5f, 2.5f }[sscc.curvaturePixelRadius.value]);
                mat.SetFloat(ShaderProperties.curvatureRidge, sscc.curvatureBrights.value == 0f ? 999f : (5f - sscc.curvatureBrights.value));
                mat.SetFloat(ShaderProperties.curvatureValley, sscc.curvatureDarks.value == 0f ? 999f : (5f - sscc.curvatureDarks.value));

                mat.SetFloat(ShaderProperties.cavityWorldRadius, sscc.cavityRadius.value);
                mat.SetFloat(ShaderProperties.cavityRidge, sscc.cavityBrights.value * 2f);
                mat.SetFloat(ShaderProperties.cavityValley, sscc.cavityDarks.value * 2f);
            }

            void UpdateShaderKeywords(Camera camera)
            {
                mat.shaderKeywords = new string[]
                {
                    camera.orthographic ? "ORTHOGRAPHIC_PROJECTION" :  "__",
                    sscc.debugMode.value == SSCC.DebugMode.EffectOnly ? "DEBUG_EFFECT" : sscc.debugMode.value == SSCC.DebugMode.ViewNormals ? "DEBUG_NORMALS" : "__",
                    sscc.normalsSource.value == SSCC.PerPixelNormals.ReconstructedFromDepth ? "NORMALS_RECONSTRUCT" : "__",
                    sscc.cavitySamples.value == SSCC.CavitySamples.Low6 ? "CAVITY_SAMPLES_6" : sscc.cavitySamples.value == SSCC.CavitySamples.Medium8 ? "CAVITY_SAMPLES_8" : sscc.cavitySamples.value == SSCC.CavitySamples.High12 ? "CAVITY_SAMPLES_12" : sscc.cavitySamples.value == SSCC.CavitySamples.VeryHigh20 ? "CAVITY_SAMPLES_20" : "",
                    sscc.saturateCavity.value ? "SATURATE_CAVITY" : "__",
                    Output == SSCC.OutputEffectTo._SSCCTexture ? "OUTPUT_TO_TEXTURE" : "__",
                    sscc.cavityResolution.value == SSCC.CavityResolution.HalfUpscaled ? "UPSCALE_CAVITY" : "__"
                };
            }

            //-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
            
            static Mesh s_FullscreenMesh = null;
            static Mesh fullscreenMesh
            {
                get
                {
                    if (s_FullscreenMesh != null) return s_FullscreenMesh;
                    float topV = 1.0f;
                    float bottomV = 0.0f;
                    s_FullscreenMesh = new Mesh { name = "Fullscreen Quad" };
                    s_FullscreenMesh.SetVertices(new List<Vector3>
                    {
                        new Vector3(-1.0f, -1.0f, 0.0f),
                        new Vector3(-1.0f,  1.0f, 0.0f),
                        new Vector3(1.0f, -1.0f, 0.0f),
                        new Vector3(1.0f,  1.0f, 0.0f)
                    });
                    s_FullscreenMesh.SetUVs(0, new List<Vector2>
                    {
                        new Vector2(0.0f, bottomV),
                        new Vector2(0.0f, topV),
                        new Vector2(1.0f, bottomV),
                        new Vector2(1.0f, topV)
                    });
                    s_FullscreenMesh.SetIndices(new[] { 0, 1, 2, 2, 1, 3 }, MeshTopology.Triangles, 0, false);
                    s_FullscreenMesh.UploadMeshData(true);
                    return s_FullscreenMesh;
                }
            }
            
            public void RenderWith(RenderTargetIdentifier source, RenderTargetIdentifier destination, CommandBuffer cmd, Material material, int passIndex = 0)
            {
                cmd.SetGlobalTexture(ShaderProperties.mainTex, source);
                cmd.SetRenderTarget(destination, 0, CubemapFace.Unknown, -1);
                cmd.DrawMesh(fullscreenMesh, Matrix4x4.identity, material, 0, passIndex);
            }

            public void Render(RenderTargetIdentifier destination, CommandBuffer cmd, Material material, int passIndex = 0)
            {
                cmd.SetRenderTarget(destination, 0, CubemapFace.Unknown, -1);
                cmd.DrawMesh(fullscreenMesh, Matrix4x4.identity, material, 0, passIndex);
            }
        }

        [SerializeField]
        [Space(15)][Header("You can now add SSCC to your Post Process Volume\nin your scene to finetune SSCC's parameters further.")]
        Shader shader;
        private SSCCRenderPass renderPass;

        public override void Create()
        {
            if (!isActive)
            {
                renderPass?.Cleanup();
                renderPass = null;
                return;
            }

            name = "SSCC";
            renderPass = new SSCCRenderPass();
        }

        void OnDisable()
        {
            renderPass?.Cleanup();
        }

#if URP_10_0_0_OR_NEWER
        protected override void Dispose(bool disposing)
        {
            renderPass?.Cleanup();
            renderPass = null;
        }
#endif

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            shader = Shader.Find("Hidden/Universal Render Pipeline/SSCC");
            if (shader == null)
            {
                Debug.LogWarning("SSCC shader was not found. Please ensure it compiles correctly");
                return;
            }

            if (renderingData.cameraData.postProcessEnabled)
            {
                renderPass.Setup(shader, renderer, renderingData);
                renderer.EnqueuePass(renderPass);
            }
        }
    }
}