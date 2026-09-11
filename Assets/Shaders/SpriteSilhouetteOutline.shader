// Outline pass used by EVVSilhouetteOutlineFeature. Drawn as one world-space quad per
// outlined character (its bounds plus the outline width), so only the pixels around the
// characters do any work. Reads the key texture written by
// "Hidden/Sprites/Silhouette Outline Key" and draws the outline color around every
// outlined part, over the pixels that part is in front of: the background, sprites in
// farther lanes, and parts of the same character with a lower sorting order that belong
// to another limb group. Pixels of the same group never outline each other, so a rig's
// joint pieces stay seamless.
Shader "Hidden/Sprites/Silhouette Outline"
{
	SubShader
	{
		Tags { "RenderPipeline"="UniversalPipeline" }

		Cull Off
		ZWrite Off
		ZTest Always
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			Name "SilhouetteOutline"

			HLSLPROGRAM
			#pragma vertex OutlineVertex
			#pragma fragment OutlineFragment

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"

			// Must match EVVSilhouetteOutlinePass.MaxRectsPerDraw.
			#define MAX_RECTS 64
			// Must match EVVSilhouetteOutlinePass.MaxRadiusPixels: offsets cover a disc of radius 8.5 texels.
			#define OFFSET_COUNT 224
			// Half a step of the 8-bit key channels.
			#define KEY_STEP (0.5 / 255.0)

			TEXTURE2D_X(_SilhouetteKeyTex);

			half4 _OutlineColor;
			// xy: one key texel in UV space, z: outline width in pixels
			float4 _OutlineParams;
			// xy: min world position, zw: max world position of each character's quad
			float4 _OutlineRects[MAX_RECTS];
			float _OutlineDepth;

			// Texel offsets sorted by distance, so the first hit is the nearest one.
			static const int2 kOffsets[OFFSET_COUNT] =
			{
				int2(0, -1), int2(-1, 0), int2(1, 0), int2(0, 1), int2(-1, -1), int2(1, -1), int2(-1, 1), int2(1, 1),
				int2(0, -2), int2(-2, 0), int2(2, 0), int2(0, 2), int2(-1, -2), int2(1, -2), int2(-2, -1), int2(2, -1),
				int2(-2, 1), int2(2, 1), int2(-1, 2), int2(1, 2), int2(-2, -2), int2(2, -2), int2(-2, 2), int2(2, 2),
				int2(0, -3), int2(-3, 0), int2(3, 0), int2(0, 3), int2(-1, -3), int2(1, -3), int2(-3, -1), int2(3, -1),
				int2(-3, 1), int2(3, 1), int2(-1, 3), int2(1, 3), int2(-2, -3), int2(2, -3), int2(-3, -2), int2(3, -2),
				int2(-3, 2), int2(3, 2), int2(-2, 3), int2(2, 3), int2(0, -4), int2(-4, 0), int2(4, 0), int2(0, 4),
				int2(-1, -4), int2(1, -4), int2(-4, -1), int2(4, -1), int2(-4, 1), int2(4, 1), int2(-1, 4), int2(1, 4),
				int2(-3, -3), int2(3, -3), int2(-3, 3), int2(3, 3), int2(-2, -4), int2(2, -4), int2(-4, -2), int2(4, -2),
				int2(-4, 2), int2(4, 2), int2(-2, 4), int2(2, 4), int2(0, -5), int2(-3, -4), int2(3, -4), int2(-4, -3),
				int2(4, -3), int2(-5, 0), int2(5, 0), int2(-4, 3), int2(4, 3), int2(-3, 4), int2(3, 4), int2(0, 5),
				int2(-1, -5), int2(1, -5), int2(-5, -1), int2(5, -1), int2(-5, 1), int2(5, 1), int2(-1, 5), int2(1, 5),
				int2(-2, -5), int2(2, -5), int2(-5, -2), int2(5, -2), int2(-5, 2), int2(5, 2), int2(-2, 5), int2(2, 5),
				int2(-4, -4), int2(4, -4), int2(-4, 4), int2(4, 4), int2(-3, -5), int2(3, -5), int2(-5, -3), int2(5, -3),
				int2(-5, 3), int2(5, 3), int2(-3, 5), int2(3, 5), int2(0, -6), int2(-6, 0), int2(6, 0), int2(0, 6),
				int2(-1, -6), int2(1, -6), int2(-6, -1), int2(6, -1), int2(-6, 1), int2(6, 1), int2(-1, 6), int2(1, 6),
				int2(-2, -6), int2(2, -6), int2(-6, -2), int2(6, -2), int2(-6, 2), int2(6, 2), int2(-2, 6), int2(2, 6),
				int2(-4, -5), int2(4, -5), int2(-5, -4), int2(5, -4), int2(-5, 4), int2(5, 4), int2(-4, 5), int2(4, 5),
				int2(-3, -6), int2(3, -6), int2(-6, -3), int2(6, -3), int2(-6, 3), int2(6, 3), int2(-3, 6), int2(3, 6),
				int2(0, -7), int2(-7, 0), int2(7, 0), int2(0, 7), int2(-1, -7), int2(1, -7), int2(-5, -5), int2(5, -5),
				int2(-7, -1), int2(7, -1), int2(-7, 1), int2(7, 1), int2(-5, 5), int2(5, 5), int2(-1, 7), int2(1, 7),
				int2(-4, -6), int2(4, -6), int2(-6, -4), int2(6, -4), int2(-6, 4), int2(6, 4), int2(-4, 6), int2(4, 6),
				int2(-2, -7), int2(2, -7), int2(-7, -2), int2(7, -2), int2(-7, 2), int2(7, 2), int2(-2, 7), int2(2, 7),
				int2(-3, -7), int2(3, -7), int2(-7, -3), int2(7, -3), int2(-7, 3), int2(7, 3), int2(-3, 7), int2(3, 7),
				int2(-5, -6), int2(5, -6), int2(-6, -5), int2(6, -5), int2(-6, 5), int2(6, 5), int2(-5, 6), int2(5, 6),
				int2(0, -8), int2(-8, 0), int2(8, 0), int2(0, 8), int2(-1, -8), int2(1, -8), int2(-4, -7), int2(4, -7),
				int2(-7, -4), int2(7, -4), int2(-8, -1), int2(8, -1), int2(-8, 1), int2(8, 1), int2(-7, 4), int2(7, 4),
				int2(-4, 7), int2(4, 7), int2(-1, 8), int2(1, 8), int2(-2, -8), int2(2, -8), int2(-8, -2), int2(8, -2),
				int2(-8, 2), int2(8, 2), int2(-2, 8), int2(2, 8), int2(-6, -6), int2(6, -6), int2(-6, 6), int2(6, 6),
			};

			struct Attributes
			{
				uint vertexID : SV_VertexID;
				uint instanceID : SV_InstanceID;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
			};

			Varyings OutlineVertex(Attributes input)
			{
				float4 rect = _OutlineRects[input.instanceID];
				float2 corner = GetQuadVertexPosition(input.vertexID).xy;
				float3 positionWS = float3(lerp(rect.xy, rect.zw, corner), _OutlineDepth);

				Varyings o;
				o.positionCS = TransformWorldToHClip(positionWS);
				return o;
			}

			// True when the sprite stored in `other` is drawn in front of the one in `center`:
			// nearer lane, or same lane and higher sorting order.
			bool IsInFront(float4 other, float4 center)
			{
				if (other.r > center.r + KEY_STEP)
				{
					return true;
				}

				return abs(other.r - center.r) <= KEY_STEP && other.b > center.b + KEY_STEP;
			}

			half4 OutlineFragment(Varyings input) : SV_Target
			{
				// The key texture was rendered by this camera into a texture of the same size and
				// orientation as the current target, so pixel coordinates map to it directly.
				float2 uv = input.positionCS.xy * _OutlineParams.xy;
				float4 center = SAMPLE_TEXTURE2D_X_LOD(_SilhouetteKeyTex, sampler_PointClamp, uv, 0);
				float radius = _OutlineParams.z;

				// The nearest texel that belongs to an outlined part in front of this pixel
				// decides the coverage; texels near the edge of the radius fade out, which
				// keeps the outer edge of the outline smooth.
				half coverage = 0;
				[loop]
				for (int i = 0; i < OFFSET_COUNT; i++)
				{
					int2 offset = kOffsets[i];
					half weight = saturate(radius + 0.5 - length(float2(offset)));
					if (weight <= 0)
					{
						break;
					}

					float4 key = SAMPLE_TEXTURE2D_X_LOD(_SilhouetteKeyTex, sampler_PointClamp, uv + offset * _OutlineParams.xy, 0);
					bool outlined = key.g > KEY_STEP;
					bool otherGroup = abs(key.g - center.g) > KEY_STEP;
					if (outlined && otherGroup && IsInFront(key, center))
					{
						coverage = weight;
						break;
					}
				}

				return half4(_OutlineColor.rgb, _OutlineColor.a * coverage);
			}
			ENDHLSL
		}
	}
}
