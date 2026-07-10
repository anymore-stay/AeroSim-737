Shader "AeroSim/B737/Heat Haze Distortion"
{
    Properties
    {
        _DistortionStrength ("Distortion Strength", Range(0, 0.08)) = 0.018
        _BlurPixels ("Blur Pixels", Range(0, 6)) = 1.4
        _NoiseScale ("Noise Scale", Range(1, 32)) = 8
        _NoiseSpeed ("Noise Speed", Range(0, 8)) = 1.6
        _Opacity ("Opacity", Range(0, 1)) = 0.18
        _Tint ("Tint", Color) = (0.82, 0.92, 1.0, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "HeatHaze"
            Tags { "LightMode" = "UniversalForward" }

            Blend One Zero
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            CBUFFER_START(UnityPerMaterial)
                float _DistortionStrength;
                float _BlurPixels;
                float _NoiseScale;
                float _NoiseSpeed;
                float _Opacity;
                float4 _Tint;
            CBUFFER_END

            float4 _CameraOpaqueTexture_TexelSize;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                screenUV = UnityStereoTransformScreenSpaceTex(screenUV);

                float2 centered = input.uv * 2.0 - 1.0;
                float radialMask = saturate(1.0 - dot(centered, centered));
                radialMask *= radialMask;

                float time = _Time.y * _NoiseSpeed;
                float n1 = ValueNoise(input.uv * _NoiseScale + float2(time, time * 0.37));
                float n2 = ValueNoise(input.uv * (_NoiseScale * 1.9) + float2(-time * 0.61, time * 0.83));
                float2 flow = float2(n1 - 0.5, n2 - 0.5);
                flow += centered.yx * float2(0.13, -0.08);

                float heat = saturate(input.color.a * _Opacity * radialMask);
                float2 offset = flow * _DistortionStrength * heat * 8.0;
                float2 blur = _CameraOpaqueTexture_TexelSize.xy * _BlurPixels * heat * 8.0;
                float2 uv = screenUV + offset;

                half3 scene = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv).rgb;
                scene += SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv + float2(blur.x, 0)).rgb;
                scene += SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv - float2(blur.x, 0)).rgb;
                scene += SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv + float2(0, blur.y)).rgb;
                scene += SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv - float2(0, blur.y)).rgb;
                scene *= 0.2;

                scene = lerp(scene, scene * _Tint.rgb, heat * 0.18);
                return half4(scene, 1.0);
            }
            ENDHLSL
        }
    }
}
