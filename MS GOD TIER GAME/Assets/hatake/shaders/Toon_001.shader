
Shader "Custom/Hatakeyama/Fix/Toon" {
    Properties {
        [HideInInspector] _MainTex      ("Texture",                 2D)             = "white" { }
        [HDR] _Color                    ("Color",                   Color)          = (1, 1, 1, 1)
        _Contrast                       ("Contrast",                Range(0, 5))    = 1.0
        _Brightness                     ("Brightness",              Range(-2, 2))   = 0.0
        [Toggle(U_BASIC_ON)]            _uBasicEnabled              ("basicEnabled",         Float) = 1.0
        [Toggle(U_POSTERIZATION_ON)]    _uPosterizationEnabled      ("posterizationEnabled", Float) = 1.0
        [Toggle(U_TOON_ON)]             _uToonEnabled               ("toonEnabled",          Float) = 1.0
        _uPosterizeLevels               ("posterizeLevels",         Range(2, 32))   = 11.0
        _uToonLevels                    ("toonLevels",              Range(2, 20))   = 4.0
        [HDR] _uToonEdgeColor           ("toonEdgeColor",           Color)          = (0.0, 0.0, 0.0, 1.0)
        _uToonEdgeThickness             ("toonEdgeThickness",       Range(0, 5))    = 0.2
        _uBlendOpacity                  ("blendOpacity",            Range(0, 1))    = 1.0

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

            #pragma shader_feature_local _ U_BASIC_ON
            #pragma shader_feature_local _ U_POSTERIZATION_ON
            #pragma shader_feature_local _ U_TOON_ON

            #define vec2 float2
            #define vec3 float3
            #define vec4 float4
            #define mat2 float2x2
            #define mat3 float3x3
            #define mat4 float4x4
            #define mix lerp
            #define fract frac
            #define texture2D tex2D

            float mod_impl(float x, float y) { return x - y * floor(x / y); }
            float2 mod_impl(float2 x, float y) { return x - y * floor(x / y); }
            float3 mod_impl(float3 x, float y) { return x - y * floor(x / y); }
            float4 mod_impl(float4 x, float y) { return x - y * floor(x / y); }
            float2 mod_impl(float2 x, float2 y) { return x - y * floor(x / y); }
            float3 mod_impl(float3 x, float3 y) { return x - y * floor(x / y); }
            float4 mod_impl(float4 x, float4 y) { return x - y * floor(x / y); }
            #define mod mod_impl
    

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

                sampler2D _MainTex;
                float _uTime;
                float2 _uResolution;
                float4 _Color;
                float _Contrast;
                float _Brightness;
                float _uBasicEnabled;
                float _uPosterizationEnabled;
                float _uToonEnabled;
                float _uPosterizeLevels;
                float _uToonLevels;
                float3 _uToonEdgeColor;
                float _uToonEdgeThickness;
                float _uBlendOpacity;

            #define CUSTOM_SHADER_AVAILABLE
            float4 customEffect(float4 color, float2 uv, float time) {
                return color;
            }


            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                float2 uv = i.uv;
                float time = _uTime;
                float2 _uResolution = _ScreenParams.xy;
                float aspect = 1.0;
                if (length(ddx(uv)) > 0.0001 && length(ddy(uv)) > 0.0001) {
                    aspect = abs(ddy(uv.y)) / max(abs(ddx(uv.x)), 0.0001);
                } else {
                    aspect = _uResolution.x / max(_uResolution.y, 1.0);
                }

                float2 texSize = float2(1024.0, 1024.0);
                float4 texColor = tex2D(_MainTex, uv);
                float3 color = texColor.rgb * _Color.rgb;
                float3 originalColor = color;
                float alpha = texColor.a * _Color.a;

                // basic
                #if defined(U_BASIC_ON)
                color *= _Color.rgb;
                color = (color - 0.5) * _Contrast + 0.5 + _Brightness;
                #endif

                // posterization
                #if defined(U_POSTERIZATION_ON)
                color = floor(color * _uPosterizeLevels) / _uPosterizeLevels;
                #endif // ifdefind posterization

                // toon
                #if defined(U_TOON_ON)
                float toonLevels = _uToonLevels;
                color = floor(color * toonLevels + 0.5) / toonLevels;
                float toonLuma = dot(color, float3(0.299, 0.587, 0.114));
                float toonEdge = length(float2(ddx(toonLuma), ddy(toonLuma))) * 50.0 * _uToonEdgeThickness;
                toonEdge = smoothstep(0.0, 1.0, toonEdge);
                color = lerp(color, _uToonEdgeColor, toonEdge);
                #endif// ifdefind toon

                return fixed4(color, saturate(alpha * _uBlendOpacity));
            }
            ENDCG
        }// End of Pass
    }// End of SubShader
}// End of shader