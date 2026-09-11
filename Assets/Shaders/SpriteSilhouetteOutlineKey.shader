// Override shader used by EVVSilhouetteOutlineFeature to render the "key" texture the
// outline is computed from. Every sprite of the sorting layer writes how close it is to
// the camera (R, lane depth), its outline id (G, 0 for sprites without an outline), its
// sorting order (B) and whether it is seamless with the body of its character (A). Those
// three arrive per renderer through the MaterialPropertyBlock that EVVSilhouetteOutline sets.
Shader "Hidden/Sprites/Silhouette Outline Key"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		_Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
		[PerRendererData] _OutlineId ("Outline Id", Float) = 0
		[PerRendererData] _OutlineOrder ("Sorting Order", Float) = 0
		[PerRendererData] _OutlineSeamless ("Seamless With Body", Float) = 0
	}

	SubShader
	{
		Tags { "RenderPipeline"="UniversalPipeline" }

		Cull Off
		ZWrite Off
		ZTest Always
		Blend Off

		Pass
		{
			Name "Key"

			HLSLPROGRAM
			#pragma vertex KeyVertex
			#pragma fragment KeyFragment
			#pragma multi_compile_instancing
			#pragma multi_compile _ SKINNED_SPRITE

			#include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

			struct Attributes
			{
				COMMON_2D_INPUTS
				half4 color : COLOR;
				UNITY_SKINNED_VERTEX_INPUTS
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float2 uv : TEXCOORD0;
				// x: renderer alpha, y: world z (the lane depth the game sorts sprites by)
				float2 alphaDepth : TEXCOORD1;
				UNITY_VERTEX_OUTPUT_STEREO
			};

			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);

			CBUFFER_START(UnityPerMaterial)
				half _Cutoff;
				float _OutlineId;
				float _OutlineOrder;
				float _OutlineSeamless;
			CBUFFER_END

			// Same vertex setup as URP's Sprite-Unlit-Default, so skinned and flipped sprites match the main render.
			Varyings KeyVertex(Attributes input)
			{
				UNITY_SKINNED_VERTEX_COMPUTE(input);
				SetUpSpriteInstanceProperties();
				input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

				Varyings o = (Varyings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				float3 positionWS = TransformObjectToWorld(input.positionOS);
				o.positionCS = TransformWorldToHClip(positionWS);
				o.uv = input.uv;
				o.alphaDepth = float2(input.color.a * unity_SpriteColor.a, positionWS.z);
				return o;
			}

			half4 KeyFragment(Varyings input) : SV_Target
			{
				half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * input.alphaDepth.x;
				clip(alpha - _Cutoff);

				// Closeness to the camera: larger is nearer, 0 means nothing was drawn.
				// World z -10..10 maps to 1..0, which covers the lane depths (0.5..5.5).
				half closeness = saturate(0.5 - input.alphaDepth.y * 0.05);
				half id = _OutlineId / 255.0;
				half order = (clamp(_OutlineOrder, -128.0, 127.0) + 128.0) / 255.0;
				return half4(closeness, id, order, _OutlineSeamless);
			}
			ENDHLSL
		}
	}
}
