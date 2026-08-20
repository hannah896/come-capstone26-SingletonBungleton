Shader "Hidden/MiniMapNormalBaker"
{
    Properties {
        _MainTex ("Albedo", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0.1, 5.0)) = 1.5 // 노말맵 강도 조절
        _LightDir ("Light Direction", Vector) = (0, 0, 1, 0)
        _LightColor ("Light Color", Color) = (1, 1, 1, 1)
        _FillLightDir ("Fill Light Direction", Vector) = (0, 0, 1, 0)
        _FillLightColor ("Fill Light Color", Color) = (0, 0, 0, 1)
    }
    SubShader {
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            sampler2D _NormalMap;
            float _BumpScale;
            float4 _LightDir;
            float4 _LightColor;
            float4 _FillLightDir;
            float4 _FillLightColor;

            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                fixed4 albedo = tex2D(_MainTex, i.uv);
                
                // 노말맵 Unpack 및 강도(BumpScale) 조절
                float3 normal = UnpackNormal(tex2D(_NormalMap, i.uv));
                normal.xy *= _BumpScale; 
                normal = normalize(normal);

                // 빛 방향
                float3 L1 = normalize(_LightDir.xyz);
                float3 L2 = normalize(_FillLightDir.xyz);

                // 빛 연산
                float ndotl1 = max(0.0, dot(normal, L1));
                float ndotl2 = max(0.0, dot(normal, L2));

                // 최종 색상 계산 (주광 + 보조광)
                fixed3 finalColor = albedo.rgb * (_LightColor.rgb * ndotl1 + _FillLightColor.rgb * ndotl2);
                
                // 그림자 영역이 새까맣게 타지 않도록 기본 환경광(Ambient) 약간 추가
                finalColor += albedo.rgb * 0.15;

                return fixed4(finalColor, albedo.a);
            }
            ENDCG
        }
    }
}