Shader "UniStorm/URP/Global Weather Lit"
{
    Properties
    {
        [MainTexture] _BaseMap("基础贴图", 2D) = "white" {}
        [MainColor] _BaseColor("基础颜色", Color) = (1,1,1,1)
        [Normal] _BumpMap("基础法线", 2D) = "bump" {}
        _BumpScale("基础法线强度", Range(0, 2)) = 1
        _Metallic("金属度", Range(0, 1)) = 0
        _Smoothness("基础光滑度", Range(0, 1)) = 0.25

        _SnowLayerTex("积雪贴图", 2D) = "white" {}
        [Normal] _SnowLayerBump("积雪法线", 2D) = "bump" {}
        _SnowLayerColor("积雪颜色", Color) = (1,1,1,1)
        _SnowDirection("积雪方向", Vector) = (0,1,0,0)
        _SnowSmoothness("积雪光滑度", Range(0, 1)) = 0.25
        _WetnessMultiplier("湿润倍率", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _BumpScale;
                half _Metallic;
                half _Smoothness;
                float4 _SnowLayerTex_ST;
                half4 _SnowLayerColor;
                float4 _SnowDirection;
                half _SnowSmoothness;
                half _WetnessMultiplier;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);
            TEXTURE2D(_SnowLayerTex);
            SAMPLER(sampler_SnowLayerTex);
            TEXTURE2D(_SnowLayerBump);
            SAMPLER(sampler_SnowLayerBump);

            // UniStorm 会在天气过渡时持续写入这两个全局参数。
            float _WetnessStrength;
            float _SnowStrength;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 tangentWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.tangentWS = half4(normalInputs.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 geometricNormalWS = NormalizeNormalPerPixel(input.normalWS);
                half3 tangentWS = NormalizeNormalPerPixel(input.tangentWS.xyz);
                half3 bitangentWS = input.tangentWS.w * cross(geometricNormalWS, tangentWS);
                half3x3 tangentToWorld = half3x3(tangentWS, bitangentWS, geometricNormalWS);

                float2 baseUv = TRANSFORM_TEX(input.uv, _BaseMap);
                float2 snowUv = TRANSFORM_TEX(input.uv, _SnowLayerTex);
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUv) * _BaseColor;
                half4 snowSample = SAMPLE_TEXTURE2D(_SnowLayerTex, sampler_SnowLayerTex, snowUv) * _SnowLayerColor;

                half3 baseNormalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, baseUv), _BumpScale);
                half3 snowNormalTS = UnpackNormal(SAMPLE_TEXTURE2D(_SnowLayerBump, sampler_SnowLayerBump, snowUv));

                half snowFacing = saturate(dot(geometricNormalWS, normalize(_SnowDirection.xyz)));
                half snowMask = smoothstep(0.4h, 0.8h, snowFacing) * saturate(_SnowStrength);
                half wetFacing = pow(saturate(geometricNormalWS.y * 0.5h + 0.5h), 2.0h);
                half wetMask = wetFacing * saturate(_WetnessStrength) * _WetnessMultiplier * (1.0h - snowMask);

                half3 normalTS = normalize(lerp(baseNormalTS, snowNormalTS, snowMask));
                half3 normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, tangentToWorld));
                half3 albedo = lerp(baseSample.rgb, snowSample.rgb, snowMask);
                half smoothness = lerp(_Smoothness, _SnowSmoothness, snowMask);
                smoothness = lerp(smoothness, 0.8h, wetMask);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.vertexLighting = VertexLighting(input.positionWS, normalWS);
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.metallic = _Metallic;
                surfaceData.specular = half3(0, 0, 0);
                surfaceData.smoothness = saturate(smoothness);
                surfaceData.normalTS = normalTS;
                surfaceData.occlusion = 1;
                surfaceData.emission = half3(0, 0, 0);
                surfaceData.alpha = baseSample.a;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return color;
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
        UsePass "Universal Render Pipeline/Lit/Meta"
    }
}
