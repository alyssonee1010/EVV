using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Draws an outline around every character marked with EVVSilhouetteOutline: around the
/// whole body, and around each limb group where it moves in front of another part of the
/// same character, instead of around each sprite of its rig.
///
/// Injected after the sprites of the configured sorting layer (Gameplay), which is
/// its own render batch because the 2D renderer's Camera Sorting Layer Texture ends
/// on that layer. The pass renders that layer once more into a "key" texture that
/// stores, per pixel, how near the visible sprite is to the camera (its lane depth),
/// its outline id (character + limb group) and its sorting order. The outline is then
/// drawn on a quad around each character, only over pixels the outlined part is in front
/// of, so a defender in a nearer lane still covers the outline of a Viking walking
/// behind it.
/// </summary>
public class EVVSilhouetteOutlineFeature : ScriptableRendererFeature2D
{
    [SerializeField] Shader keyShader;
    [SerializeField] Shader outlineShader;
    [SerializeField] Color outlineColor = Color.black;
    [SerializeField, Min(0f)] float outlineWidth = 0.035f;
    [Tooltip("Caps the outline width in screen pixels on high-resolution screens. The cost of the outline pass grows with the square of the pixel width, so lower this for phones or weak GPUs.")]
    [SerializeField, Range(1f, EVVSilhouetteOutlinePass.MaxRadiusPixels)] float maxOutlinePixels = 5f;
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
        if (pass == null || outlineWidth <= 0f || !SortingLayer.IsValid(sortingLayerID) || EVVSilhouetteOutline.Active.Count == 0)
        {
            return;
        }

        pass.renderPassEvent2D = injectionPoint2D;
        pass.renderPassSortingLayerID = sortingLayerID;
        pass.Setup(outlineColor, outlineWidth, maxOutlinePixels, alphaCutoff);
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
    // The outline pass reads every texel within the radius, so keep it small. The offset
    // table in the outline shader covers this radius.
    public const float MaxRadiusPixels = 8f;
    // Must match MAX_RECTS in the outline shader.
    const int MaxRectsPerDraw = 64;

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
        public RendererListHandle sprites;
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

    readonly Material keyMaterial;
    readonly Material outlineMaterial;
    readonly MaterialPropertyBlock outlineProperties = new MaterialPropertyBlock();
    readonly Vector4[] rects = new Vector4[MaxRectsPerDraw];
    Color outlineColor;
    float outlineWidth;
    float maxRadiusPixels;
    float alphaCutoff;

    public EVVSilhouetteOutlinePass(Material keyMaterial, Material outlineMaterial)
    {
        this.keyMaterial = keyMaterial;
        this.outlineMaterial = outlineMaterial;
        profilingSampler = new ProfilingSampler("EVV Silhouette Outline");
    }

    public void Setup(Color color, float width, float maxPixels, float cutoff)
    {
        outlineColor = color;
        outlineWidth = width;
        maxRadiusPixels = Mathf.Clamp(maxPixels, 1f, MaxRadiusPixels);
        alphaCutoff = cutoff;
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
            if (rectCount == MaxRectsPerDraw || !character.TryGetBounds(out Bounds bounds))
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
        descriptor.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
        descriptor.depthBufferBits = 0;
        descriptor.msaaSamples = 1;
        TextureHandle key = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "_EVVSilhouetteOutlineKey", true);

        keyMaterial.SetFloat(CutoffId, alphaCutoff);

        // Sorted the way the layer itself is drawn (lane depth), so the nearest sprite wins in the key texture.
        DrawingSettings drawing = CreateDrawingSettings(ShaderTags, renderingData, cameraData, lightData, SortingCriteria.CommonTransparent);
        drawing.overrideMaterial = keyMaterial;
        drawing.overrideMaterialPassIndex = 0;

        short layerValue = (short)SortingLayer.GetLayerValueFromID(renderPassSortingLayerID);
        FilteringSettings filtering = new FilteringSettings(RenderQueueRange.all)
        {
            sortingLayerRange = new SortingLayerRange(layerValue, layerValue),
        };
        RendererListHandle sprites = renderGraph.CreateRendererList(new RendererListParams(renderingData.cullResults, drawing, filtering));

        using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("EVV Silhouette Outline Key", out KeyPassData data, profilingSampler))
        {
            data.sprites = sprites;
            builder.UseRendererList(sprites);
            builder.SetRenderAttachment(key, 0);
            builder.SetRenderFunc(static (KeyPassData passData, RasterGraphContext context) =>
            {
                context.cmd.DrawRendererList(passData.sprites);
            });
        }

        Camera camera = cameraData.camera;
        float pixelsPerUnit = camera.orthographic
            ? descriptor.height / (2f * camera.orthographicSize)
            : descriptor.height * 0.1f;
        float radiusPixels = Mathf.Min(outlineWidth * pixelsPerUnit, maxRadiusPixels);

        using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("EVV Silhouette Outline", out OutlinePassData data, profilingSampler))
        {
            data.key = key;
            data.material = outlineMaterial;
            data.properties = outlineProperties;
            data.color = outlineColor;
            data.parameters = new Vector4(1f / descriptor.width, 1f / descriptor.height, radiusPixels, 0f);
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
}
