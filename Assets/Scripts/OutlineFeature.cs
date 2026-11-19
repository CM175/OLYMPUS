using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

public class OutlineFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Material outlineCompositeMaterial;
        public RenderingLayerMask teamALayerMask = 1 << 1; // Rendering Layer 1
        public RenderingLayerMask teamBLayerMask = 1 << 2; // Rendering Layer 2
        [ColorUsage(true, true)] public Color teamAColor = new Color(0.15f, 0.65f, 1.0f, 1.0f);
        [ColorUsage(true, true)] public Color teamBColor = new Color(1.0f, 0.15f, 0.15f, 1.0f);
        public bool showThroughWallsForTeamA = true;
    }

    public Settings settings = new Settings();
    private Material _maskMaterial;
    private OutlineRenderPass _pass;

    public override void Create()
    {
        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlitShader != null)
        {
            _maskMaterial = CoreUtils.CreateEngineMaterial(unlitShader);
            _maskMaterial.SetColor("_BaseColor", Color.white);
        }

        _pass = new OutlineRenderPass(settings, _maskMaterial)
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.outlineCompositeMaterial == null || _maskMaterial == null) return;
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(_maskMaterial);
    }

    private class OutlineRenderPass : ScriptableRenderPass
    {
        private readonly Settings _settings;
        private readonly Material _maskMat;

        private static readonly int TeamAMaskId = Shader.PropertyToID("_TeamAMaskTex");
        private static readonly int TeamBMaskId = Shader.PropertyToID("_TeamBMaskTex");
        private static readonly int TeamAColorId = Shader.PropertyToID("_TeamAColor");
        private static readonly int TeamBColorId = Shader.PropertyToID("_TeamBColor");

        private static readonly List<ShaderTagId> ShaderTagIds = new List<ShaderTagId>
        {
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly"),
            new ShaderTagId("SRPDefaultUnlit")
        };

        private class MaskPassData
        {
            public RendererListHandle rendererList;
        }

        private class CompositePassData
        {
            public TextureHandle teamAMask;
            public TextureHandle teamBMask;
            public TextureHandle sourceColor;
            public Material compositeMat;
            public Color teamAColor;
            public Color teamBColor;
        }

        public OutlineRenderPass(Settings settings, Material maskMat)
        {
            _settings = settings;
            _maskMat = maskMat;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalLightData lightData = frameData.Get<UniversalLightData>();

            RenderTextureDescriptor maskDesc = cameraData.cameraTargetDescriptor;
            maskDesc.colorFormat = RenderTextureFormat.R8;
            maskDesc.depthBufferBits = 0;
            maskDesc.msaaSamples = 1;

            TextureHandle teamAMask = UniversalRenderer.CreateRenderGraphTexture(renderGraph, maskDesc, "_TeamAMaskTex", true);
            TextureHandle teamBMask = UniversalRenderer.CreateRenderGraphTexture(renderGraph, maskDesc, "_TeamBMaskTex", true);

            // 1. Render Team A Mask
            using (var builder = renderGraph.AddRasterRenderPass<MaskPassData>("TeamA Silhouette Mask", out var data))
            {
                var filter = new FilteringSettings(RenderQueueRange.opaque)
                {
                    renderingLayerMask = _settings.teamALayerMask
                };
                var drawSettings = RenderingUtils.CreateDrawingSettings(
                    ShaderTagIds, renderingData, cameraData, lightData, SortingCriteria.CommonOpaque);
                drawSettings.overrideMaterial = _maskMat;

                data.rendererList = renderGraph.CreateRendererList(
                    new RendererListParams(renderingData.cullResults, drawSettings, filter));

                builder.UseRendererList(data.rendererList);
                builder.SetRenderAttachment(teamAMask, 0);
                if (!_settings.showThroughWallsForTeamA)
                {
                    builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);
                }

                builder.SetRenderFunc((MaskPassData d, RasterGraphContext ctx) =>
                {
                    ctx.cmd.ClearRenderTarget(false, true, Color.black);
                    ctx.cmd.DrawRendererList(d.rendererList);
                });
            }

            // 2. Render Team B Mask
            using (var builder = renderGraph.AddRasterRenderPass<MaskPassData>("TeamB Silhouette Mask", out var data))
            {
                var filter = new FilteringSettings(RenderQueueRange.opaque)
                {
                    renderingLayerMask = _settings.teamBLayerMask
                };
                var drawSettings = RenderingUtils.CreateDrawingSettings(
                    ShaderTagIds, renderingData, cameraData, lightData, SortingCriteria.CommonOpaque);
                drawSettings.overrideMaterial = _maskMat;

                data.rendererList = renderGraph.CreateRendererList(
                    new RendererListParams(renderingData.cullResults, drawSettings, filter));

                builder.UseRendererList(data.rendererList);
                builder.SetRenderAttachment(teamBMask, 0);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);

                builder.SetRenderFunc((MaskPassData d, RasterGraphContext ctx) =>
                {
                    ctx.cmd.ClearRenderTarget(false, true, Color.black);
                    ctx.cmd.DrawRendererList(d.rendererList);
                });
            }

            // 3. Composite onto Temp Texture, then swap activeCameraColor
            RenderTextureDescriptor colorDesc = cameraData.cameraTargetDescriptor;
            colorDesc.depthBufferBits = 0;
            TextureHandle tempColor = UniversalRenderer.CreateRenderGraphTexture(renderGraph, colorDesc, "_OutlineTempColor", false);

            using (var builder = renderGraph.AddRasterRenderPass<CompositePassData>("Overwatch 2D Outline Composite", out var data))
            {
                data.teamAMask = teamAMask;
                data.teamBMask = teamBMask;
                data.sourceColor = resourceData.activeColorTexture;
                data.compositeMat = _settings.outlineCompositeMaterial;
                data.teamAColor = _settings.teamAColor;
                data.teamBColor = _settings.teamBColor;

                builder.UseTexture(teamAMask, AccessFlags.Read);
                builder.UseTexture(teamBMask, AccessFlags.Read);
                builder.UseTexture(resourceData.activeColorTexture, AccessFlags.Read);
                builder.SetRenderAttachment(tempColor, 0);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc((CompositePassData d, RasterGraphContext ctx) =>
                {
                    ctx.cmd.SetGlobalTexture(TeamAMaskId, d.teamAMask);
                    ctx.cmd.SetGlobalTexture(TeamBMaskId, d.teamBMask);
                    d.compositeMat.SetColor(TeamAColorId, d.teamAColor);
                    d.compositeMat.SetColor(TeamBColorId, d.teamBColor);

                    Blitter.BlitTexture(ctx.cmd, d.sourceColor, new Vector4(1, 1, 0, 0), d.compositeMat, 0);
                });
            }

            // Point camera color to the composited output (zero extra copy pass!)
            resourceData.cameraColor = tempColor;
        }
    }
}