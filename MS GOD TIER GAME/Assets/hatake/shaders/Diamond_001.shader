
Shader "Custom/Hatakeyama/Fix/Diamond" {
    Properties {
        [HideInInspector] _MainTex      ("Texture",              2D)             = "white" { }
        [HDR] _Color                          ("Color",          Color)          = (1, 1, 1, 1)
        _Contrast                       ("Contrast",             Range(0, 5))    = 1.0
        _Brightness                     ("Brightness",           Range(-2, 2))   = 0.0
        _uGlassMap                      ("Glass Map",            2D)             = "white" {}
        [Toggle(U_BASE_ON)] _uBasicEnabled                  ("basicEnabled",         Float)          = 1.0
        [Toggle(U_GLASS_ON)] _uGlassEnabled                 ("glassEnabled",         Float)          = 1.0
        [Toggle(U_DEBUG_DISTOTION_ON)] _uDebugDistortion    ("debugDistortion",      Float)          = 0.0
        _uGlassDistortion               ("glassDistortion",      Range(0, 1.0))  = 0.0
        _uSparkleIntensity              ("sparkleIntensity",     Range(0, 10))   = 2.5
        _uFacetScale                    ("facetScale",           Range(1, 30))   = 8.5
        _uSparkleSharpness              ("sparkleSharpness",     Range(1, 100))  = 16.0
        _uFlickerSpeed                  ("flickerSpeed",         Range(0, 20))   = 3.0
        _uFresnelPower                  ("fresnelPower",         Range(0.1, 10)) = 2.7
        _uFresnelIntensity              ("fresnelIntensity",     Range(0, 2))    = 0.5
        _uRainbowStrength               ("rainbowStrength",      Range(0, 1))    = 0.3
        [HDR] _uSparkleColor            ("sparkleColor",         Color)          = (1.0, 1.0, 1.0, 1.0)
        _uBlendOpacity                  ("blendOpacity",         Range(0, 1))    = 1.0

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

            #pragma shader_feature_local _ U_BASE_ON
            #pragma shader_feature_local _ U_GLASS_ON
            #pragma shader_feature_local _ U_DEBUG_DISTOTION_ON

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
                float _uGlassEnabled;
                float _uDebugDistortion;
                float _uGlassDistortion;
                sampler2D _uGlassMap;
                float _uUseGlassMap;
                float _uSparkleIntensity;
                float _uFacetScale;
                float _uSparkleSharpness;
                float _uFlickerSpeed;
                float _uFresnelPower;
                float _uFresnelIntensity;
                float _uRainbowStrength;
                float3 _uSparkleColor;
                float _uBlendOpacity;


        // Voronoi
        float2 hash2(float2 p) {
            p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
            return frac(sin(p) * 43758.5453);
        }
        
        float voronoiDiamond(float2 uv, out float2 cellCenter, out float secondDist) {
            float2 i = floor(uv);
            float2 f = frac(uv);
            float minDist = 1.0;
            float minDist2 = 1.0;
            cellCenter = float2(0.0, 0.0);
            for (int y = -1; y <= 1; y++) {
                for (int x = -1; x <= 1; x++) {
                    float2 neighbor = float2(float(x), float(y));
                    float2 randomOffset = hash2(i + neighbor);
                    float2 diff = neighbor + randomOffset - f;
                    float dist = length(diff);
                    if (dist < minDist) {
                        minDist2 = minDist;
                        minDist = dist;
                        cellCenter = i + neighbor + randomOffset;
                    } else if (dist < minDist2) {
                        minDist2 = dist;
                    }
                }
            }
            secondDist = minDist2;
            return minDist;
        }
        
        float getLuminance(float3 c) {
            return dot(c, float3(0.299, 0.587, 0.114));
        }

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
                float time = _Time.y;
                float2 _uResolution = _ScreenParams.xy;
                float aspect = 1.0;
                if (length(ddx(uv)) > 0.0001 && length(ddy(uv)) > 0.0001) {
                    aspect = abs(ddy(uv.y)) / max(abs(ddx(uv.x)), 0.0001);
                } else {
                    aspect = _uResolution.x / max(_uResolution.y, 1.0);
                }

                // debug distortion
                float glassDistortion = _uGlassDistortion;
                #if defined(U_DEBUG_DISTOTION_ON)
                   glassDistortion = abs(frac(time * 0.25) * 2.0 - 1.0) * 0.4;
                #endif // if defined(U_DEBUG_DISTOTION_ON)

                // glass distortion
                #if defined(U_GLASS_ON)

                if (glassDistortion > 0.001) {
                    if (_uUseGlassMap > 0.5) {
                        float mapVal = getLuminance(tex2D(_uGlassMap, uv).rgb) * 2.0 - 1.0;
                        uv += mapVal * glassDistortion;
                    } else {
                        float2 facetUV = uv * _uFacetScale;
                        float2 dCellCenter;
                        float dSecondDist;
                        float vDist = voronoiDiamond(facetUV, dCellCenter, dSecondDist);
                        float2 cellRandom = hash2(dCellCenter);
                        float2 offset = (cellRandom - 0.5) * glassDistortion / _uFacetScale;
                        uv += offset;
                    }
                }
                #endif // if defined(U_GLASS_ON)

                // ここで歪んだUVを使ってテクスチャの色を取得する
                float2 texSize = float2(1024.0, 1024.0);
                float4 texColor = tex2D(_MainTex, uv);
                float3 color = texColor.rgb * _Color.rgb;
                float3 originalColor = color;
                float alpha = texColor.a * _Color.a;

                // basic
                #if defined(U_BASE_ON)
                    color *= _Color.rgb;
                    color = (color - 0.5) * _Contrast + 0.5 + _Brightness;
                #endif // if defined(U_BASE_ON)

                // glass color
                #if defined(U_GLASS_ON)
                    if (_uSparkleIntensity > 0.001) {
                        float2 facetUV = uv * _uFacetScale;
                        float2 sCellCenter;
                        float sSecondDist;
                        float vDist = voronoiDiamond(facetUV, sCellCenter, sSecondDist);
                        float2 cellRandom = hash2(sCellCenter);
                        float3 facetNormal = normalize(float3(cellRandom * 2.0 - 1.0, 1.0));
                        float sparkleTime = time * _uFlickerSpeed;
                        float flicker = sin(sparkleTime + cellRandom.x * 100.0) * 0.5 + 0.5;
                        flicker *= sin(sparkleTime * 1.3 + cellRandom.y * 50.0) * 0.5 + 0.5;
                        float3 lightDir = normalize(float3(0.5, 1.0, 0.5));
                        float3 viewDir = float3(0.0, 0.0, 1.0);
                        float3 halfVec = normalize(lightDir + viewDir);
                        float3 surfNormal = normalize(float3(0.0, 0.0, 1.0) + facetNormal * 0.5);
                        float specular = pow(max(dot(surfNormal, halfVec), 0.0), _uSparkleSharpness);
                        float sparkle = specular * flicker;
                        sparkle = smoothstep(0.3, 0.5, sparkle);
                        float fDist = length(uv - 0.5) * 2.0;
                        float fresnel = pow(clamp(fDist, 0.0, 1.0), _uFresnelPower) * _uFresnelIntensity;
                        float3 rainbow = float3(
                            sin(cellRandom.x * 6.28 + 0.0) * 0.5 + 0.5,
                            sin(cellRandom.x * 6.28 + 2.09) * 0.5 + 0.5,
                            sin(cellRandom.x * 6.28 + 4.18) * 0.5 + 0.5
                        );
                        float3 sparkleCol = lerp(_uSparkleColor, rainbow, _uRainbowStrength);
                        color += sparkleCol * fresnel * 0.5;
                        color += sparkleCol * sparkle * _uSparkleIntensity;
                    }
                #endif // if defined(U_GLASS_ON)

                // // glass crack lines
                // if (_uGlassEnabled > 0.5 && glassDistortion > 0.001) {
                //     float2 crackUV = uv * _uFacetScale;
                //     float2 crackCell;
                //     float crackSecond;
                //     float crackDist = voronoiDiamond(crackUV, crackCell, crackSecond);
                //     float crackWidth = 0.06;
                //     float crackEdge = smoothstep(0.0, crackWidth, crackSecond - crackDist);
                //     color *= crackEdge;
                // }

                return fixed4(color, saturate(alpha * _uBlendOpacity)/* 1.0f */);
            }
            ENDCG
        }// End of Pass
    }// End of SubShader
}// End of shader