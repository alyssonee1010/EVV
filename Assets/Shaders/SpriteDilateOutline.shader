// Draws a character part and its outline in a single batched sprite draw, instead of
// re-rendering the whole sorting layer into a key texture the way
// EVVSilhouetteOutlineFeature does. It costs no extra draw call at all: the outline is
// the same pass, taking a ring of alpha samples only on the pixels the art does not
// cover, so opaque pixels pay nothing.
//
// The outline lives in the transparent margin the sprite's mesh keeps around the art, so
// a part whose mesh hugs the art too tightly gets its outline clipped. The margin the
// PSD importer leaves (padding + extrude) is enough for a width of about 4 texels.
Shader "Sprites/Dilate Outline"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		_Color ("Tint", Color) = (1,1,1,1)
		_OutlineColor ("Outline Colour", Color) = (0,0,0,1)
		_OutlineWidth ("Outline Width (screen pixels)", Range(0, 8)) = 3
		_Cutoff ("Alpha Cutoff", Range(0.01, 1)) = 0.5
	}

	SubShader
	{
		Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }

		Cull Off
		ZWrite Off
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			Name "SpriteWithOutline"
			Tags { "LightMode"="Universal2D" }

			HLSLPROGRAM
			#pragma vertex OutlineVertex
			#pragma fragment OutlineFragment
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
				half4 color : COLOR;
				UNITY_VERTEX_OUTPUT_STEREO
			};

			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);

			// _MainTex_TexelSize is deliberately absent: declaring a _TexelSize or _ST for a
			// texture switches the 2D SRP Batcher off for every renderer using this material,
			// which is the batching this shader exists to keep. The ring offsets come from
			// screen-space derivatives of the UV instead, which also keeps the outline the same
			// thickness whatever scale the character is drawn at.
			CBUFFER_START(UnityPerMaterial)
				half4 _Color;
				half4 _OutlineColor;
				half _OutlineWidth;
				half _Cutoff;
			CBUFFER_END

			// 16 taps around a circle: enough for a 3-4 texel rim without visible banding.
			static const float2 kRing[16] =
			{
				float2( 1.000,  0.000), float2( 0.924,  0.383), float2( 0.707,  0.707), float2( 0.383,  0.924),
				float2( 0.000,  1.000), float2(-0.383,  0.924), float2(-0.707,  0.707), float2(-0.924,  0.383),
				float2(-1.000,  0.000), float2(-0.924, -0.383), float2(-0.707, -0.707), float2(-0.383, -0.924),
				float2( 0.000, -1.000), float2( 0.383, -0.924), float2( 0.707, -0.707), float2( 0.924, -0.383),
			};

			Varyings OutlineVertex(Attributes input)
			{
				UNITY_SKINNED_VERTEX_COMPUTE(input);
				SetUpSpriteInstanceProperties();
				input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

				Varyings o = (Varyings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				o.positionCS = TransformObjectToHClip(input.positionOS);
				o.uv = input.uv;
				o.color = input.color * unity_SpriteColor * _Color;
				return o;
			}

			half4 OutlineFragment(Varyings input) : SV_Target
			{
				// Derivatives have to be taken before the branch below, while the quad is still
				// uniform. uvPerPixel is how far the UV moves for one screen pixel on each axis.
				float2 duvdx = ddx(input.uv);
				float2 duvdy = ddy(input.uv);
				float2 uvPerPixel = float2(length(float2(duvdx.x, duvdy.x)), length(float2(duvdx.y, duvdy.y)));

				half4 art = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

				// Where the art covers the pixel, this is an ordinary sprite and costs nothing extra.
				if (art.a >= _Cutoff)
				{
					return art * input.color;
				}

				// Otherwise look for art within the outline width; if any is found this pixel is rim.
				half hit = 0;
				[unroll]
				for (int i = 0; i < 16; i++)
				{
					float2 uv = input.uv + kRing[i] * _OutlineWidth * uvPerPixel;
					hit = max(hit, step(_Cutoff, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a));
				}

				clip(hit - 0.5);
				return half4(_OutlineColor.rgb, _OutlineColor.a * input.color.a);
			}
			ENDHLSL
		}
	}

	Fallback "Sprites/Default"
}
