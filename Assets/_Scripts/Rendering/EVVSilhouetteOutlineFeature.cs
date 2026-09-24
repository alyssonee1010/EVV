using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Draws one outline around every character marked with EVVSilhouetteOutline: around the
/// whole body, never between the character's own parts, instead of around each sprite of
/// its rig.
///
/// Injected after the sprites of the configured sorting layer (Gameplay), which is its own
/// render batch because the 2D renderer's Camera Sorting Layer Texture ends on that layer.
/// The pass renders that layer once more into a "key" texture that stores, per pixel, how
/// near the visible sprite is to the camera (its lane depth) and whether it belongs to an
/// outlined character. The outline is then drawn on a quad around each character, only over
/// pixels the character is in front of, so a defender in a nearer lane still covers the
/// outline of a Viking walking behind it.
///
/// The key is drawn as two renderer lists - outlined sprites, then everything else - each
/// with its own material carrying a constant id. That keeps the id out of per-renderer
/// MaterialPropertyBlocks, which break sprite batching badly: carrying it per renderer
/// measured about 8 ms of extra frame time at 60 characters, roughly three quarters of what
/// the outline used to cost. A real depth attachment resolves the two lists against each
/// other, so their draw order does not matter.
/// </summary>
public class EVVSilhouetteOutlineFeature : ScriptableRendererFeature2D
{
    [SerializeField] Shader keyShader;
    [SerializeField] Shader outlineShader;
    [SerializeField] Color outlineColor = Color.black;
    [SerializeField, Min(0f)] float outlineWidth = 0.035f;
    [Tooltip("Caps the outline width in screen pixels on high-resolution screens. Lower this for phones or weak GPUs.")]
    [SerializeField, Range(1f, EVVSilhouetteOutlinePass.MaxRadiusPixels)] float maxOutlinePixels = 5f;
    [Tooltip("Resolution of the key texture relative to the screen. 0.5 quarters the pixels the key pass fills and the outline pass reads, at the cost of a slightly coarser outline edge. The first thing to lower for phones or weak GPUs.")]
    [SerializeField, Range(0.25f, 1f)] float keyResolutionScale = 1f;
    [SerializeField, Range(0f, 1f)] float alphaCutoff = 0.5f;

    Material outlinedKeyMaterial;
    Material occluderKeyMaterial;
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

        outlinedKeyMaterial = CoreUtils.CreateEngineMaterial(keyShader);
        occluderKeyMaterial = CoreUtils.CreateEngineMaterial(keyShader);
        outlinedKeyMaterial.SetFloat(EVVSilhouetteOutlinePass.OutlineIdId, 1f);
        occluderKeyMaterial.SetFloat(EVVSilhouetteOutlinePass.OutlineIdId, 0f);
        outlineMaterial = CoreUtils.CreateEngineMaterial(outlineShader);
        pass = new EVVSilhouetteOutlinePass(outlinedKeyMaterial, occluderKeyMaterial, outlineMaterial);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (pass == null || outlineWidth <= 0f || !SortingLayer.IsValid(sortingLayerID) || EVVSilhouetteOutline.Active.Count == 0)
        {
            return;
        }

        pass.renderPassEvent2D = injectionPoint2D;
        pass.renderPassSortingLayerID = sortingLayerID;
        pass.Setup(outlineColor, outlineWidth, maxOutlinePixels, alphaCutoff, keyResolutionScale);
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        DestroyMaterials();
    }

    void DestroyMaterials()
    {
        CoreUtils.Destroy(outlinedKeyMaterial);
        CoreUtils.Destroy(occluderKeyMaterial);
        CoreUtils.Destroy(outlineMaterial);
        outlinedKeyMaterial = null;
        occluderKeyMaterial = null;
        outlineMaterial = null;
    }
}

class EVVSilhouetteOutlinePass : ScriptableRenderPass2D
{
    // The outline pass reads every texel within the radius, so keep it small. The offset
    // table in the outline shader covers this radius.
    public const float MaxRadiusPixels = 8f;
    // Must match MAX_RECTS in the outline shader.
    const int MaxRectsPerDraw = 64;

    public static readonly int OutlineIdId = Shader.PropertyToID("_OutlineId");

    // The passes the 2D renderer draws, so every sprite material qualifies for the key texture.
    static readonly List<ShaderTagId> ShaderTags = new List<ShaderTagId>
    {
        new ShaderTagId("SRPDefaultUnlit"),
        new ShaderTagId("Universal2D"),
    };

    static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
    static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    static readonly int OutlineParamsId = Shader.PropertyToID("_OutlineParams");
    static readonly int OutlineRectsId = Shader.PropertyToID("_OutlineRects");
    static readonly int OutlineDepthId = Shader.PropertyToID("_OutlineDepth");
    static readonly int KeyTextureId = Shader.PropertyToID("_SilhouetteKeyTex");

    class KeyPassData
    {
        public RendererListHandle outlined;
        public RendererListHandle occluders;
    }

    class OutlinePassData
    {
        public TextureHandle key;
        public Material material;
        public MaterialPropertyBlock properties;
        public Color color;
        public Vector4 parameters;
        public Vector4[] rects;
        public int rectCount;
        public float depth;
    }

    readonly Material outlinedKeyMaterial;
    readonly Material occluderKeyMaterial;
    readonly Material outlineMaterial;
    readonly MaterialPropertyBlock outlineProperties = new MaterialPropertyBlock();
    readonly Vector4[] rects = new Vector4[MaxRectsPerDraw];
    Color outlineColor;
    float outlineWidth;
    float maxRadiusPixels;
    float alphaCutoff;
    float keyScale;

    public EVVSilhouetteOutlinePass(Material outlinedKeyMaterial, Material occluderKeyMaterial, Material outlineMaterial)
    {
        this.outlinedKeyMaterial = outlinedKeyMaterial;
        this.occluderKeyMaterial = occluderKeyMaterial;
        this.outlineMaterial = outlineMaterial;
        profilingSampler = new ProfilingSampler("EVV Silhouette Outline");
    }

    public void Setup(Color color, float width, float maxPixels, float cutoff, float resolutionScale)
    {
        outlineColor = color;
        outlineWidth = width;
        maxRadiusPixels = Mathf.Clamp(maxPixels, 1f, MaxRadiusPixels);
        alphaCutoff = cutoff;
        keyScale = Mathf.Clamp(resolutionScale, 0.25f, 1f);
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
        UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
        UniversalLightData lightData = frameData.Get<UniversalLightData>();

        // One quad per outlined character: its sprite bounds plus the outline width.
        float depth = 0f;
        int rectCount = 0;
        foreach (EVVSilhouetteOutline character in EVVSilhouetteOutline.Active)
        {
            if (rectCount == MaxRectsPerDraw)
            {
                break;
            }

            if (!character.TryGetBounds(out Bounds bounds))
            {
                continue;
            }

            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            rects[rectCount] = new Vector4(min.x - outlineWidth, min.y - outlineWidth, max.x + outlineWidth, max.y + outlineWidth);
            depth += bounds.center.z;
            rectCount++;
        }

        if (rectCount == 0)
        {
            return;
        }

        depth /= rectCount;

        RenderTextureDescriptor descriptor = cameraData.cameraTargetDescriptor;
        int screenWidth = descriptor.width;
        int screenHeight = descriptor.height;
        descriptor.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
        descriptor.depthBufferBits = 0;
        descriptor.msaaSamples = 1;
        descriptor.width = Mathf.Max(1, Mathf.RoundToInt(screenWidth * keyScale));
        descriptor.height = Mathf.Max(1, Mathf.RoundToInt(screenHeight * keyScale));
        TextureHandle key = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "_EVVSilhouetteOutlineKey", true);

        // Real depth, so the outlined and occluder lists resolve against each other by lane
        // depth instead of by which list happens to be drawn second.
        RenderTextureDescriptor depthDescriptor = descriptor;
        depthDescriptor.graphicsFormat = GraphicsFormat.None;
        depthDescriptor.depthStencilFormat = GraphicsFormat.D32_SFloat;
        TextureHandle keyDepth = UniversalRenderer.CreateRenderGraphTexture(renderGraph, depthDescriptor, "_EVVSilhouetteOutlineKeyDepth", true);

        outlinedKeyMaterial.SetFloat(CutoffId, alphaCutoff);
        occluderKeyMaterial.SetFloat(CutoffId, alphaCutoff);

        short layerValue = (short)SortingLayer.GetLayerValueFromID(renderPassSortingLayerID);
        RendererListHandle outlinedList = CreateList(renderGraph, renderingData, cameraData, lightData,
            outlinedKeyMaterial, layerValue, EVVSilhouetteOutline.OutlinedRenderingLayer);
        RendererListHandle occluderList = CreateList(renderGraph, renderingData, cameraData, lightData,
            occluderKeyMaterial, layerValue, ~EVVSilhouetteOutline.OutlinedRenderingLayer);

        using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("EVV Silhouette Outline Key", out KeyPassData data, profilingSampler))
        {
            data.outlined = outlinedList;
            data.occluders = occluderList;
            builder.UseRendererList(outlinedList);
            builder.UseRendererList(occluderList);
            builder.SetRenderAttachment(key, 0);
            builder.SetRenderAttachmentDepth(keyDepth);
            builder.SetRenderFunc(static (KeyPassData passData, RasterGraphContext context) =>
            {
                context.cmd.DrawRendererList(passData.occluders);
                context.cmd.DrawRendererList(passData.outlined);
            });
        }

        Camera camera = cameraData.camera;
        float pixelsPerUnit = camera.orthographic
            ? screenHeight / (2f * camera.orthographicSize)
            : screenHeight * 0.1f;
        // Radius is authored in screen pixels but the ring is sampled in key pixels.
        float radiusPixels = Mathf.Min(outlineWidth * pixelsPerUnit, maxRadiusPixels) * keyScale;

        using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("EVV Silhouette Outline", out OutlinePassData data, profilingSampler))
        {
            data.key = key;
            data.material = outlineMaterial;
            data.properties = outlineProperties;
            data.color = outlineColor;
            // xy: screen pixel -> uv, for the pixel this fragment is on. z: ring radius in key
            // pixels. w: the key's scale, so the shader can step the ring by whole key texels.
            data.parameters = new Vector4(1f / screenWidth, 1f / screenHeight, radiusPixels, keyScale);
            data.rects = rects;
            data.rectCount = rectCount;
            data.depth = depth;
            builder.UseTexture(key);
            builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
            builder.SetRenderFunc(static (OutlinePassData passData, RasterGraphContext context) =>
            {
                RTHandle keyTexture = passData.key;
                MaterialPropertyBlock properties = passData.properties;
                properties.SetTexture(KeyTextureId, keyTexture);
                properties.SetColor(OutlineColorId, passData.color);
                properties.SetVector(OutlineParamsId, passData.parameters);
                properties.SetVectorArray(OutlineRectsId, passData.rects);
                properties.SetFloat(OutlineDepthId, passData.depth);
                context.cmd.DrawProcedural(Matrix4x4.identity, passData.material, 0, MeshTopology.Quads, 4, passData.rectCount, properties);
            });
        }
    }

    RendererListHandle CreateList(RenderGraph renderGraph, UniversalRenderingData renderingData, UniversalCameraData cameraData,
        UniversalLightData lightData, Material overrideMaterial, short layerValue, uint renderingLayerMask)
    {
        // Sorted the way the layer itself is drawn (lane depth), so the nearest sprite wins.
        DrawingSettings drawing = CreateDrawingSettings(ShaderTags, renderingData, cameraData, lightData, SortingCriteria.CommonTransparent);
        drawing.overrideMaterial = overrideMaterial;
        drawing.overrideMaterialPassIndex = 0;

        FilteringSettings filtering = new FilteringSettings(RenderQueueRange.all)
        {
            sortingLayerRange = new SortingLayerRange(layerValue, layerValue),
            renderingLayerMask = renderingLayerMask,
        };
        return renderGraph.CreateRendererList(new RendererListParams(renderingData.cullResults, drawing, filtering));
    }
}
