Shader "SingletonBungleton/Weather/Rain"
{
    Properties
    {
        _BaseColor ("Rain Color", Color) = (0.65, 0.76, 0.86, 0.65)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Rain"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                half fogFactor : TEXCOORD1;
                half nearFade : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.uv = input.uv;
                output.color = input.color * _BaseColor;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                output.nearFade = saturate((-position.positionVS.z - 0.3) / 0.7);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // 텍스처 없이 빗줄기의 양옆과 끝을 부드럽게 지웁니다.
                half width = 1.0h - smoothstep(0.15h, 1.0h, abs(input.uv.x * 2.0h - 1.0h));
                half ends = smoothstep(0.0h, 0.15h, input.uv.y) *
                    (1.0h - smoothstep(0.75h, 1.0h, input.uv.y));
                half alpha = input.color.a * width * ends * input.nearFade;
                return half4(MixFog(input.color.rgb, input.fogFactor), alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
