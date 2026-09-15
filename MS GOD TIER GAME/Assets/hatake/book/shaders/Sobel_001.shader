
Shader "Custom/Hatakeyama/Fix/Sobel" {
    Properties {
        [HideInInspector] _MainTex          ("Texture",              2D)             = "white" { }
        [HDR] _Color                        ("Color",                Color)          = (1, 1, 1, 1)
        _Contrast                           ("Contrast",             Range(0, 5))    = 1.0
        _Brightness                         ("Brightness",           Range(-2, 2))   = 0.0
        [Toggle(U_USE_MAINTEXTURE)] _uMainTextureEnabled        ("mainTextureEnabled",   Float)          = 1.0 
        [Toggle(U_EDGEDETECTION_ON)] _uEdgeDetectionEnabled     ("edgeDetectionEnabled", Float)          = 1.0
        [Toggle(U_MIRROR_ON)] _uMirrorTileEnabled               ("mirrorTileEnabled",    Float)          = 1.0
        [Toggle(U_MIRROR_Y)] _uMirrorTileY                      ("mirrorTileY",          Float)          = 0.0
        [Toggle(U_MIRROR_X)] _uMirrorTileX                      ("mirrorTileX",          Float)          = 0.0
        _uEdgeThickness                     ("edgeThickness",        Range(0.5, 5))  = 1.5
        [HDR] _uEdgeColor                   ("edgeColor",            Color)          = (0.58, 0.929, 1.0, 1.0)
        _uBlendOpacity                      ("blendOpacity",         Range(0, 1))    = 1.0

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

            #pragma shader_feature_local _ U_USE_MAINTEXTURE
            #pragma shader_feature_local _ U_EDGEDETECTION_ON
            #pragma shader_feature_local _ U_MIRROR_ON
            #pragma shader_feature_local _ U_MIRROR_Y
            #pragma shader_feature_local _ U_MIRROR_X

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
                float _uMainTextureEnabled;
                float _uEdgeDetectionEnabled;
                float _uMirrorTileEnabled;
                float _uEdgeThickness;
                float4 _uEdgeColor;
                float _uEdgeMixMode;
                float _uMirrorTileX;
                float _uMirrorTileY;
                float _uBlendOpacity;


            // Voronoi function
            float2 hash2(float2 p) {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }
            
            float voronoiDiamond(float2 uv, out float2 cellCenter) {
                float2 i = floor(uv);
                float2 f = frac(uv);
                float minDist = 1.0;
                cellCenter = float2(0.0, 0.0);
                for (int y = -1; y <= 1; y++) {
                    for (int x = -1; x <= 1; x++) {
                        float2 neighbor = float2(float(x), float(y));
                        float2 randomOffset = hash2(i + neighbor);
                        float2 diff = neighbor + randomOffset - f;
                        float dist = length(diff);
                        if (dist < minDist) {
                            minDist = dist;
                            cellCenter = i + neighbor + randomOffset;
                        }
                    }
                }
                return minDist;
            }
            
            float getLuminance(float3 c) {
                return dot(c, float3(0.299, 0.587, 0.114));
            }
            
            // Custom Shader PlaceHolder
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
            
                // mirror tile
                #if defined(U_MIRROR_ON)
                    #if defined(U_MIRROR_X)
                        uv.x = abs(fmod(uv.x, 2.0) - 1.0);
                    #endif // if defined(U_MIRROR_X)
            
                    #if defined(U_MIRROR_Y)
                        uv.y = abs(fmod(uv.y, 2.0) - 1.0);
                    #endif // if defined(U_MIRROR_Y)
                #endif // if defined(U_MIRROR_ON)
            
                float4 texColor = tex2D(_MainTex, uv);
                float3 color = texColor.rgb * _Color.rgb;
                float alpha = texColor.a * _Color.a;
                float edge = 0.0;
            
                // エッジのみ表示
                #if !defined(U_USE_MAINTEXTURE)
                    color = float3(0.0, 0.0, 0.0);
                    alpha = 0.0;
                #endif // if defined(U_USE_MAINTEXTURE)
            
                // edge detection
                #if defined(U_EDGEDETECTION_ON)
                    float2 texel = _uEdgeThickness / _uResolution;
                    float tl = getLuminance(tex2D(_MainTex, uv + float2(-texel.x,  texel.y)).rgb);
                    float tm = getLuminance(tex2D(_MainTex, uv + float2(     0.0,  texel.y)).rgb);
                    float tr = getLuminance(tex2D(_MainTex, uv + float2( texel.x,  texel.y)).rgb);
                    float ml = getLuminance(tex2D(_MainTex, uv + float2(-texel.x,      0.0)).rgb);
                    float mr = getLuminance(tex2D(_MainTex, uv + float2( texel.x,      0.0)).rgb);
                    float bl = getLuminance(tex2D(_MainTex, uv + float2(-texel.x, -texel.y)).rgb);
                    float bm = getLuminance(tex2D(_MainTex, uv + float2(     0.0, -texel.y)).rgb);
                    float br = getLuminance(tex2D(_MainTex, uv + float2( texel.x, -texel.y)).rgb);
                    float gx = -tl - 2.0 * ml - bl + tr + 2.0 * mr + br;
                    float gy = -tl - 2.0 * tm - tr + bl + 2.0 * bm + br;
                    edge = sqrt(gx * gx + gy * gy);
                    edge = smoothstep(0.02, 0.15, edge);
            
                    if (_uEdgeMixMode < 0.5) {
                        color = lerp(color, _uEdgeColor.rgb, edge);
                    } else if (_uEdgeMixMode < 1.5) {
                        color += _uEdgeColor.rgb * edge;
                    } else {
                        color = lerp(float3(0.0, 0.0, 0.0), _uEdgeColor.rgb, edge);
                    }
                #endif // if defined(U_EDGEDETECTION_ON)
            
                // アルファをエッジ強度で決定（エッジのみ表示）
                #if !defined(U_USE_MAINTEXTURE)
                    alpha = edge;
                #endif // if defined(U_USE_MAINTEXTURE)
            
                return fixed4(color, saturate(alpha * _uBlendOpacity));
            }
            ENDCG
        }// End of Pass
    }// End of SubShader
}// End of shader