using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Draws an outline around the whole silhouette of every character marked with
/// EVVSilhouetteOutline, instead of around each sprite of its rig.
///
/// Injected after the sprites of the configured sorting layer (Gameplay), which is
/// its own render batch because the 2D renderer's Camera Sorting Layer Texture ends
/// on that layer. The pass renders that layer once more into a "key" texture that
/// stores how near each visible sprite is to the camera (its lane depth) and which
/// pixels belong to outlined characters. The outline is then drawn in screen space
/// only over pixels the character is in front of, so a defender in a nearer lane
/// still covers the outline of a Viking walking behind it.
/// </summary>
public class EVVSilhouetteOutlineFeature : ScriptableRendererFeature2D
{
    [SerializeField] Shader keyShader;
    [SerializeField] Shader outlineShader;
    [SerializeField] Color outlineColor = Color.black;
    [SerializeField, Min(0f)] float outlineWidth = 0.035f;
    [SerializeField, Range(0f, 1f)] float alphaCutoff = 0.5f;

    Material keyMaterial;
    Material outlineMaterial;
    EVVSilhouetteOutlinePass pass;

    public override void Create()
    {
        DestroyMaterials();
        pass = null;
        if (keyShader == null || outlineShader == null)
        {
            return;
        }

        keyMaterial = CoreUtils.CreateEngineMaterial(keyShader);
        outlineMaterial = CoreUtils.CreateEngineMaterial(outlineShader);
        pass = new EVVSilhouetteOutlinePass(keyMaterial, outlineMaterial);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (pass == null || outlineWidth <= 0f || !SortingLayer.IsValid(sortingLayerID) || EVVSilhouetteOutline.ActiveCount == 0)
        {
            return;
        }

        pass.renderPassEvent2D = injectionPoint2D;
        pass.renderPassSortingLayerID = sortingLayerID;
        pass.Setup(outlineColor, outlineWidth, alphaCutoff);
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        DestroyMaterials();
    }

    void DestroyMaterials()
    {
        CoreUtils.Destroy(keyMaterial);
        CoreUtils.Destroy(outlineMaterial);
        keyMaterial = null;
        outlineMaterial = null;
    }
}

class EVVSilhouetteOutlinePass : ScriptableRenderPass2D
{
    // The passes the 2D renderer draws, so every sprite material qualifies for the key texture.
    static readonly List<ShaderTagId> ShaderTags = new List<ShaderTagId>
    {
        new ShaderTagId("SRPDefaultUnlit"),
        new ShaderTagId("Universal2D"),
    };

    static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
    static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    static readonly int OutlineParamsId = Shader.PropertyToID("_OutlineParams");

    class KeyPassData
    {
        public RendererListHandle occluders;
        public RendererListHandle characters;
    }

    class OutlinePassData
    {
        public TextureHandle key;
        public Material material;
        public Color color;
        public Vector4 parameters;
    }

    readonly Material keyMaterial;
    readonly Material outlineMaterial;
    Color outlineColor;
    float outlineWidth;
    float alphaCutoff;

    public EVVSilhouetteOutlinePass(Material keyMaterial, Material outlineMaterial)
    {
        this.keyMaterial = keyMaterial;
        this.outlineMaterial = outlineMaterial;
        profilingSampler = new ProfilingSampler("EVV Silhouette Outline");
    }

    public void Setup(Color color, float width, float cutoff)
    {
        outlineColor = color;
        outlineWidth = width;
        alphaCutoff = cutoff;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
        UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
        UniversalLightData lightData = frameData.Get<UniversalLightData>();

        RenderTextureDescriptor descriptor = cameraData.cameraTargetDescriptor;
        descriptor.graphicsFormat = GraphicsFormat.R16G16_SFloat;
        descriptor.depthBufferBits = 0;
        descriptor.msaaSamples = 1;
        TextureHandle key = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "_EVVSilhouetteOutlineKey", true, FilterMode.Bilinear);

        keyMaterial.SetFloat(CutoffId, alphaCutoff);

        // Sorted the way the layer itself is drawn (lane depth), so the nearest sprite wins in the key texture.
        DrawingSettings occluderDrawing = CreateDrawingSettings(ShaderTags, renderingData, cameraData, lightData, SortingCriteria.CommonTransparent);
        occluderDrawing.overrideMaterial = keyMaterial;
        occluderDrawing.overrideMaterialPassIndex = 0;
        DrawingSettings characterDrawing = occluderDrawing;
        characterDrawing.overrideMaterialPassIndex = 1;

        short layerValue = (short)SortingLayer.GetLayerValueFromID(renderPassSortingLayerID);
        FilteringSettings occluderFilter = new FilteringSettings(RenderQueueRange.all)
        {
            sortingLayerRange = new SortingLayerRange(layerValue, layerValue),
        };
        FilteringSettings characterFilter = occluderFilter;
        characterFilter.renderingLayerMask = EVVSilhouetteOutline.RenderingLayerMask;

        RendererListHandle occluders = renderGraph.CreateRendererList(new RendererListParams(renderingData.cullResults, occluderDrawing, occluderFilter));
        RendererListHandle characters = renderGraph.CreateRendererList(new RendererListParams(renderingData.cullResults, characterDrawing, characterFilter));

        using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("EVV Silhouette Outline Key", out KeyPassData data, profilingSampler))
        {
            data.occluders = occluders;
            data.characters = characters;
            builder.UseRendererList(occluders);
            builder.UseRendererList(characters);
            builder.SetRenderAttachment(key, 0);
            builder.SetRenderFunc(static (KeyPassData passData, RasterGraphContext context) =>
            {
                context.cmd.DrawRendererList(passData.occluders);
                context.cmd.DrawRendererList(passData.characters);
            });
        }

        Camera camera = cameraData.camera;
        float pixelsPerUnit = camera.orthographic
            ? descriptor.height / (2f * camera.orthographicSize)
            : descriptor.height * 0.1f;

        using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("EVV Silhouette Outline", out OutlinePassData data, profilingSampler))
        {
            data.key = key;
            data.material = outlineMaterial;
            data.color = outlineColor;
            data.parameters = new Vector4(1f / descriptor.width, 1f / descriptor.height, outlineWidth * pixelsPerUnit, 0f);
            builder.UseTexture(key);
            builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
            builder.SetRenderFunc(static (OutlinePassData passData, RasterGraphContext context) =>
            {
                passData.material.SetColor(OutlineColorId, passData.color);
                passData.material.SetVector(OutlineParamsId, passData.parameters);
                Blitter.BlitTexture(context.cmd, passData.key, new Vector4(1f, 1f, 0f, 0f), passData.material, 0);
            });
        }
    }
}
