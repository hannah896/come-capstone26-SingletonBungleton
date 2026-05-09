Shader "KGB/Terrain/URPStochasticTerrain4"
{
    Properties
    {
        [HideInInspector] _Control("Control", 2D) = "red" {}
        _Splat0("Layer 0", 2D) = "white" {}
        _Splat1("Layer 1", 2D) = "white" {}
        _Splat2("Layer 2", 2D) = "white" {}
        _Splat3("Layer 3", 2D) = "white" {}

        _LayerTiling0("Layer 0 Tiling", Float) = 0.08
        _LayerTiling1("Layer 1 Tiling", Float) = 0.08
        _LayerTiling2("Layer 2 Tiling", Float) = 0.08
        _LayerTiling3("Layer 3 Tiling", Float) = 0.08

        _StochasticCellSize("Stochastic Cell Size", Float) = 24.0
        _StochasticJitter("Stochastic Jitter", Range(0, 1)) = 0.35
        _Brightness("Brightness", Range(0.5, 2.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/02_Scripts/@@Test/KGB_Test/Shaders/Terrain/Includes/StochasticSampling.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            TEXTURE2D(_Control); SAMPLER(sampler_Control);
            TEXTURE2D(_Splat0); SAMPLER(sampler_Splat0);
            TEXTURE2D(_Splat1); SAMPLER(sampler_Splat1);
            TEXTURE2D(_Splat2); SAMPLER(sampler_Splat2);
            TEXTURE2D(_Splat3); SAMPLER(sampler_Splat3);

            CBUFFER_START(UnityPerMaterial)
                float _LayerTiling0;
                float _LayerTiling1;
                float _LayerTiling2;
                float _LayerTiling3;
                float _StochasticCellSize;
                float _StochasticJitter;
                float _Brightness;
            CBUFFER_END

            Varyings Vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs posInput = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs nrmInput = GetVertexNormalInputs(v.normalOS);

                o.positionCS = posInput.positionCS;
                o.positionWS = posInput.positionWS;
                o.normalWS = normalize(nrmInput.normalWS);
                o.uv = v.uv;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float4 control = SAMPLE_TEXTURE2D(_Control, sampler_Control, i.uv);
                control = max(control, 0.0001);
                control /= (control.r + control.g + control.b + control.a);

                float2 worldXZ = i.positionWS.xz;

                float4 c0 = SampleStochasticColor(TEXTURE2D_ARGS(_Splat0, sampler_Splat0), worldXZ * _LayerTiling0, worldXZ, _StochasticCellSize, _StochasticJitter);
                float4 c1 = SampleStochasticColor(TEXTURE2D_ARGS(_Splat1, sampler_Splat1), worldXZ * _LayerTiling1, worldXZ, _StochasticCellSize, _StochasticJitter);
                float4 c2 = SampleStochasticColor(TEXTURE2D_ARGS(_Splat2, sampler_Splat2), worldXZ * _LayerTiling2, worldXZ, _StochasticCellSize, _StochasticJitter);
                float4 c3 = SampleStochasticColor(TEXTURE2D_ARGS(_Splat3, sampler_Splat3), worldXZ * _LayerTiling3, worldXZ, _StochasticCellSize, _StochasticJitter);

                float3 albedo = (c0.rgb * control.r) + (c1.rgb * control.g) + (c2.rgb * control.b) + (c3.rgb * control.a);

                Light mainLight = GetMainLight();
                float3 n = normalize(i.normalWS);
                float ndl = saturate(dot(n, normalize(mainLight.direction)));
                float3 lit = albedo * (0.25 + (ndl * mainLight.color));

                return half4(lit * _Brightness, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}