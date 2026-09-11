// Full-screen pass used by EVVSilhouetteOutlineFeature. Reads the key texture written by
// "Hidden/Sprites/Silhouette Outline Key" and draws the outline color around the combined
// silhouette of every outlined character, but only over pixels that character is in front of.
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
			#pragma vertex Vert
			#pragma fragment OutlineFragment

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

			half4 _OutlineColor;
			// xy: one key texel in UV space, z: outline width in pixels
			float4 _OutlineParams;

			// A character has to be at least this much nearer to the camera than the pixel it
			// outlines. Lanes are 0.05 apart in the key texture (see the key shader).
			#define CLOSENESS_STEP 0.01

			half4 OutlineFragment(Varyings input) : SV_Target
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

				float2 uv = input.texcoord;
				float closeness = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv).r;
				float2 radius = _OutlineParams.xy * _OutlineParams.z;

				half coverage = 0;
				// 16 taps on the outline radius plus 8 taps at half the radius, so thin
				// features are not missed. Bilinear filtering keeps the edge smooth.
				[unroll]
				for (int i = 0; i < 24; i++)
				{
					float angle = i < 16 ? i * (TWO_PI / 16.0) : (i - 16 + 0.5) * (TWO_PI / 8.0);
					float2 offset = float2(cos(angle), sin(angle)) * radius * (i < 16 ? 1.0 : 0.5);
					half2 key = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + offset).rg;
					// key.g: outlined character, key.r: nearer than what is drawn at this pixel
					coverage = max(coverage, key.g * step(closeness + CLOSENESS_STEP, key.r));
				}

				return half4(_OutlineColor.rgb, _OutlineColor.a * coverage);
			}
			ENDHLSL
		}
	}
}
