
Shader "Custom/Hatakeyama/Fix/BumpUI" {
    Properties {
        [HideInInspector] _MainTex  ("Texture",         2D)          = "white" { }
        _BumpMap                    ("Normal Map",       2D)          = "bump" { }
        _BumpScale                  ("Bump Scale",       Range(0, 2)) = 1.0
        [HDR] _Color                ("Color",            Color)       = (1, 1, 1, 1)
        _uLightDir                  ("Light Direction",  Vector)      = (0.5, 0.5, -1.0, 0.0)
        _uAmbient                   ("Ambient",          Range(0, 1)) = 0.2
        _uBlendOpacity              ("blendOpacity",     Range(0, 1)) = 1.0
    }
    SubShader {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
          
            struct appdata {
                float4 vertex   : POSITION;
                float2 uv       : TEXCOORD0;
                float3 normal   : NORMAL;
                float4 tangent  : TANGENT;
            };

            struct v2f {
                float2 uv         : TEXCOORD0;
                float4 vertex     : SV_POSITION;
                float3 tangentW   : TEXCOORD1;
                float3 bitangentW : TEXCOORD2;
                float3 normalW    : TEXCOORD3;
            };

            sampler2D _MainTex;
            sampler2D _BumpMap;
            float _BumpScale;
            float4 _Color;
            float4 _uLightDir;
            float _uAmbient;
            float _uBlendOpacity;

            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;

                float3 worldNormal    = UnityObjectToWorldNormal(v.normal);
                float3 worldTangent   = UnityObjectToWorldDir(v.tangent.xyz);
                float3 worldBitangent = cross(worldNormal, worldTangent)
                                        * v.tangent.w * unity_WorldTransformParams.w;
                o.normalW    = worldNormal;
                o.tangentW   = worldTangent;
                o.bitangentW = worldBitangent;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                float2 uv = i.uv;

                float4 texColor = tex2D(_MainTex, uv);
                float3 color = texColor.rgb * _Color.rgb;
                float alpha = texColor.a * _Color.a;

                // bump mapping
                float3 normalTS = UnpackNormal(tex2D(_BumpMap, uv));
                normalTS.xy *= _BumpScale;
                normalTS = normalize(normalTS);
                float3 N = normalize(
                    i.tangentW   * normalTS.x +
                    i.bitangentW * normalTS.y +
                    i.normalW    * normalTS.z
                );

                float3 L = normalize(-_uLightDir.xyz);
                float diffuse = max(0.0, dot(N, L));
                float lighting = _uAmbient + (1.0 - _uAmbient) * diffuse;
                color *= lighting;

                return fixed4(color, saturate(alpha * _uBlendOpacity));
            }
            ENDCG
        }// End of Pass
    }// End of SubShader
}// End of shader