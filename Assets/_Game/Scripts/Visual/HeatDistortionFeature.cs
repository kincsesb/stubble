using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace Fields.Visual
{
    public class HeatDistortionFeature : ScriptableRendererFeature
    {
        public Material material;
        public RenderPassEvent passEvent = RenderPassEvent.BeforeRenderingPostProcessing;

        HeatDistortionPass _pass;

        public override void Create()
        {
            _pass = new HeatDistortionPass(material, passEvent);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (material == null) return;
            _pass.material = material;
            // Request depth+color so URP generates _CameraDepthTexture before our pass
            _pass.ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
            renderer.EnqueuePass(_pass);
        }

        // ------------------------------------------------------------------ //

        class HeatDistortionPass : ScriptableRenderPass
        {
            public Material material;

            static readonly int s_DepthTexID = Shader.PropertyToID("_CameraDepthTexture");

            public HeatDistortionPass(Material mat, RenderPassEvent evt)
            {
                material = mat;
                renderPassEvent = evt;
                requiresIntermediateTexture = true;
            }

            class PassData
            {
                public TextureHandle color;
                public TextureHandle depth;
                public Material material;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resourceData = frameData.Get<UniversalResourceData>();
                var cameraData   = frameData.Get<UniversalCameraData>();

                if (resourceData.isActiveTargetBackBuffer) return;
                if (material == null) return;

                TextureHandle srcColor = resourceData.activeColorTexture;
                TextureHandle srcDepth = resourceData.cameraDepthTexture;

                // Create output texture with same format as camera color
                var desc = cameraData.cameraTargetDescriptor;
                desc.msaaSamples     = 1;
                desc.depthBufferBits = 0;
                TextureHandle dst = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph, desc, "_HeatDistortionBuffer", false);

                using var builder = renderGraph.AddRasterRenderPass<PassData>(
                    "Heat Distortion", out var passData);

                passData.color    = srcColor;
                passData.depth    = srcDepth;
                passData.material = material;

                builder.UseTexture(srcColor, AccessFlags.Read);
                builder.UseTexture(srcDepth, AccessFlags.Read);
                builder.SetRenderAttachment(dst, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                {
                    ctx.cmd.SetGlobalTexture(s_DepthTexID, data.depth);
                    Blitter.BlitTexture(ctx.cmd, data.color,
                        new Vector4(1, 1, 0, 0), data.material, 0);
                });

                resourceData.cameraColor = dst;
            }
        }
    }
}