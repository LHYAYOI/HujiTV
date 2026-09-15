
Shader "Custom/Hatakeyama/Fix/Grudge" {
    Properties {
        [HideInInspector] _MainTex              ("Texture",                     2D)             = "white" { }
        [HDR] _Color                            ("Color",                 Color)          = (1, 1, 1, 1)
        _Contrast                               ("Contrast",                    Range(0, 5))    = 1.0
        _Brightness                             ("Brightness",                  Range(-2, 2))   = 0.0
            _uDisplacementMap                   ("Displacement Map",            2D)             = "black" {} 
        [Toggle(U_BASE_ON)] _uBasicEnabled                      ("basicEnabled",                Float)          = 1.0 
        [Toggle(U_CHROMATIC_ON)] _uChromaticAberrationEnabled   ("chromaticAberrationEnabled",  Float)          = 1.0 
        [Toggle(U_PIXEL_ON)] _uPixelationEnabled                ("pixelationEnabled",           Float)          = 1.0 
        [Toggle(U_GLITCH_ON)] _uGlitchEnabled                   ("glitchEnabled",               Float)          = 1.0 
        [Toggle(U_DISPLACEMENT_ON)] _uDisplacementEnabled       ("displacementEnabled",         Float)          = 1.0 
        [Toggle(U_SHARPEN_ON)] _uSharpenEnabled                 ("sharpenEnabled",              Float)          = 1.0 
        [Toggle(U_MOSH_ON)] _uDataMoshEnabled                   ("dataMoshEnabled",             Float)          = 1.0 
        _uChromaticAberration                   ("chromaticAberration",         Range(0, 0.05)) = 0.0030 
        _uPixelSize                             ("pixelSize",                   Range(1, 64))   = 5.0000 
        _uGlitchStrength                        ("glitchStrength",              Range(0, 1))    = 1.0000 
        _uGlitchSpeed                           ("glitchSpeed",                 Range(0, 20))   = 13.0000 
        _uGlitchBlockSize                       ("glitchBlockSize",             Range(1, 50))   = 24.0000 
        _uDisplacementStrength                  ("displacementStrength",        Range(0, 0.5))  = 0.5000 
        _uSharpenStrength                       ("sharpenStrength",             Range(0, 5))    = 1.0000 
        _uBlendOpacity                          ("blendOpacity",                Range(0, 1))    = 1.0000 
        _uDataMoshStrength                      ("dataMoshStrength",            Range(0, 1))    = 0.0500 
        _uDataMoshBlockSize                     ("dataMoshBlockSize",           Range(2, 64))   = 61.0000 
        _uDataMoshSpeed                         ("dataMoshSpeed",               Range(0.1, 10)) = 3.0000 
        _uDataMoshChannelShift                  ("dataMoshChannelShift",        Range(0, 0.05)) = 0.0090 

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
            #pragma shader_feature_local _ U_CHROMATIC_ON
            #pragma shader_feature_local _ U_PIXEL_ON
            #pragma shader_feature_local _ U_GLITCH_ON
            #pragma shader_feature_local _ U_DISPLACEMENT_ON
            #pragma shader_feature_local _ U_SHARPEN_ON
            #pragma shader_feature_local _ U_MOSH_ON

            #define vec2 float2
            #define vec3 float3
            #define vec4 float4
            #define mat2 float2x2
            #define mat3 float3x3
            #define mat4 float4x4
            #define mix lerp
            #define fract frac
            #define texture2D tex2D

            // GLSLの mod(x, y) は x - y * floor(x/y)
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
                float2 _uResolution;
                float4 _Color;
                float _Contrast;
                float _Brightness;
                float _uBasicEnabled;
                float _uChromaticAberrationEnabled;
                float _uPixelationEnabled;
                float _uGlitchEnabled;
                float _uDisplacementEnabled;
                float _uSharpenEnabled;
                float _uCRTEnabled;
                float _uDataMoshEnabled;
                float _uChromaticAberration;
                float _uDissolveNoiseType;
                float _uPixelSize;
                float _uGlitchStrength;
                float _uGlitchSpeed;
                float _uGlitchBlockSize;
                float _uDisplacementStrength;
                sampler2D _uDisplacementMap;
                float _uUseDisplacementMap;
                float _uSharpenStrength;
                float _uCRTCurvature;
                float _uDataMoshStrength;
                float _uDataMoshBlockSize;
                float _uDataMoshSpeed;
                float _uDataMoshChannelShift;
                float _uBlendOpacity;


            // Noize
            float3 permute(float3 x) {
                return fmod(((x * 34.0) + 1.0) * x, 289.0);
            }
            
            float snoise(float2 v) {
                const float4 C = float4(0.211324865405187, 0.366025403784439,
                                        -0.577350269189626, 0.024390243902439);
                float2 i  = floor(v + dot(v, C.yy));
                float2 x0 = v - i + dot(i, C.xx);
                float2 i1 = (x0.x > x0.y) ? float2(1.0, 0.0) : float2(0.0, 1.0);
                float4 x12 = x0.xyxy + C.xxzz;
                x12.xy -= i1;
                i = fmod(i, 289.0);
                float3 p = permute(permute(i.y + float3(0.0, i1.y, 1.0)) + i.x + float3(0.0, i1.x, 1.0));
                float3 m = max(0.5 - float3(dot(x0, x0), dot(x12.xy, x12.xy), dot(x12.zw, x12.zw)), 0.0);
                m = m * m;
                m = m * m;
                float3 x_ = 2.0 * frac(p * C.www) - 1.0;
                float3 h = abs(x_) - 0.5;
                float3 ox = floor(x_ + 0.5);
                float3 a0 = x_ - ox;
                m *= 1.79284291400159 - 0.85373472095314 * (a0 * a0 + h * h);
                float3 g;
                g.x = a0.x * x0.x + h.x * x0.y;
                g.yz = a0.yz * x12.xz + h.yz * x12.yw;
                return 130.0 * dot(m, g);
            }
            
            float voronoi(float2 p) {
                float2 n = floor(p);
                float2 f = frac(p);
                float minDist = 1.0;
                for (int j = -1; j <= 1; j++) {
                    for (int i = -1; i <= 1; i++) {
                        float2 g = float2(float(i), float(j));
                        float2 o = float2(frac(sin(dot(n+g, float2(127.1,311.7)))*43758.5453),
                                          frac(sin(dot(n+g, float2(269.5,183.3)))*43758.5453));
                        float d = dot(g+o-f, g+o-f);
                        minDist = min(minDist, d);
                    }
                }
                return sqrt(minDist);
            }
            
            float cellular(float2 p) {
                float2 n = floor(p);
                float2 f = frac(p);
                float d1 = 1.0, d2 = 1.0;
                for (int j = -1; j <= 1; j++) {
                    for (int i = -1; i <= 1; i++) {
                        float2 g = float2(float(i), float(j));
                        float2 o = float2(frac(sin(dot(n+g, float2(127.1,311.7)))*43758.5453),
                                          frac(sin(dot(n+g, float2(269.5,183.3)))*43758.5453));
                        float d = dot(g+o-f, g+o-f);
                        if (d < d1) {
                            d2 = d1;
                            d1 = d;
                        } else if (d < d2) {
                            d2 = d;
                        }
                    }
                }
                return d2 - d1;
            }
            
            float getNoiseValue(float2 p) {
                if (_uDissolveNoiseType < 0.5) {
                    float n = snoise(p);
                    return (n+1.0)*0.5;
                } else if (_uDissolveNoiseType < 1.5) {
                    return voronoi(p);
                } else {
                    return cellular(p);
                }
            }
            
            float rand(float2 n) {
                return frac(sin(dot(n, float2(12.9898, 4.1414))) * 43758.5453);
            }
            
            #define CUSTOM_SHADER_AVAILABLE
            float4 customEffect(float4 color, float2 uv, float time) {
                return color;
            }
            // EndNoizefunction


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

                float2 texSize = float2(1024.0, 1024.0);
                float4 texColor = tex2D(_MainTex, uv);
                float3 color = texColor.rgb * _Color.rgb;
                float3 originalColor = color;
                float alpha = texColor.a * _Color.a;

                // curvature
                if (_uCRTEnabled > 0.5 && _uCRTCurvature > 0.001) {
                    float2 centered = uv - 0.5;
                    float barrelDist = dot(centered, centered);
                    uv = uv + centered * barrelDist * _uCRTCurvature;
                }

                // pixelation
                #if defined(U_PIXEL_ON)
                    float dx = _uPixelSize / _uResolution.x;
                    float dy = _uPixelSize / _uResolution.y;
                    float2 p = float2(dx, dy);
                    uv = floor(uv / p) * p;
                #endif // ifdef pixelation

                #if defined(U_DISPLACEMENT_ON)
                if (_uUseDisplacementMap > 0.5) {
                    float2 dispVal = texture2D(_uDisplacementMap, uv).rg * 2.0 - 1.0;
                    uv += dispVal * _uDisplacementStrength;
                }
                #endif// ifdef displacement

                // chromatic
                float chromaAmount = _uChromaticAberration;

                // glitch
                #if defined(U_GLITCH_ON)
                    float glitchTimeBlock = floor(time * _uGlitchSpeed);
                    float glitchTrigger = step(0.8, rand(float2(glitchTimeBlock, 0.0)));

                    if (glitchTrigger > 0.5) {
                        float blockY = floor(uv.y * _uGlitchBlockSize);
                        float shift = (rand(float2(blockY, glitchTimeBlock)) - 0.5) * _uGlitchStrength * 0.1;
                        uv.x += shift;

                        float blockNoise = rand(float2(blockY, glitchTimeBlock + 1.0));
                        if (blockNoise > 0.95) {
                            uv.y += (rand(float2(glitchTimeBlock, blockY)) - 0.5) * 0.02 * _uGlitchStrength;
                        }
                    }

                    chromaAmount += glitchTrigger * _uGlitchStrength * 0.01;
                #endif // ifdef glitch

                #if defined(U_GLITCH_ON)
                    float timeBlock = floor(time * _uGlitchSpeed);
                    float trigger = step(0.8, rand(float2(timeBlock, 0.0)));
                    chromaAmount += trigger * _uGlitchStrength * 0.01;
                #endif// ifdef glitch


                #if defined(U_CHROMATIC_ON)
                if (chromaAmount > 0.001) {
                    float r = texture2D(_MainTex, uv + float2(chromaAmount, 0.0)).r;
                    float g = texture2D(_MainTex, uv).g;
                    float b = texture2D(_MainTex, uv - float2(chromaAmount, 0.0)).b;
                    texColor = float4(r, g, b, 1.0);
                } else {
                    texColor = texture2D(_MainTex, uv);
                }
                #endif // ifdef chromatic

                color = texColor.rgb;
                originalColor = color;

                // sharpen
                #if defined(U_SHARPEN_ON)
                if (_uSharpenStrength > 0.001) {
                    float2 texel = 1.0 / _uResolution;
                    float4 neighbors = texture2D(_MainTex, uv + float2(texel.x, 0.0))
                        + texture2D(_MainTex, uv - float2(texel.x, 0.0))
                        + texture2D(_MainTex, uv + float2(0.0, texel.y))
                        + texture2D(_MainTex, uv - float2(0.0, texel.y));
                    color = color * (1.0 + 4.0 * _uSharpenStrength) - neighbors.rgb * _uSharpenStrength;
                    color = clamp(color, 0.0, 1.0);
                }
                #endif // ifdef sharpen

                // basic
                #if defined(U_BASE_ON)
                    color *= _Color.rgb;
                    color = (color - 0.5) * _Contrast + 0.5 + _Brightness;
                #endif // ifdef basic

                // color
                if (_uCRTEnabled > 0.5) {
                    float subpixel = fmod(float4(uv * _uResolution, 0.0, 1.0).x, 3.0);
                    float3 mask = float3(1.0, 1.0, 1.0);
                    if (subpixel < 1.0) {
                        mask = float3(1.0, 0.7, 0.7);
                    } else if (subpixel < 2.0) {
                        mask = float3(0.7, 1.0, 0.7);
                    } else {
                        mask = float3(0.7, 0.7, 1.0);
                    }
                    color *= lerp(float3(1.0, 1.0, 1.0), mask, 0.3);
                    float crtLine = sin(float4(uv * _uResolution, 0.0, 1.0).y * 1.5) * 0.5 + 0.5;
                    color *= lerp(1.0, crtLine, 0.15);
                    float2 crtUV = i.uv - 0.5;
                    float edgeDark = 1.0 - dot(crtUV, crtUV) * _uCRTCurvature * 2.0;
                    color *= clamp(edgeDark, 0.0, 1.0);
                }

                // mosh
                #if defined(U_MOSH_ON)
                if (_uDataMoshStrength > 0.001) {
                    float dmTime = floor(time * _uDataMoshSpeed);
                    float blockW = _uDataMoshBlockSize / _uResolution.x;
                    float blockH = _uDataMoshBlockSize / _uResolution.y;
                    float2 blockCoord = floor(uv / float2(blockW, blockH));
                    float blockSeed = rand(blockCoord + dmTime * 0.1);
                    float trigger = step(1.0 - _uDataMoshStrength, blockSeed);
                    float2 shiftDir = float2(
                        rand(blockCoord + float2(dmTime, 0.0)) - 0.5,
                        rand(blockCoord + float2(0.0, dmTime)) - 0.5
                    ) * 2.0;
                    float2 shiftedUV = uv + shiftDir * _uDataMoshStrength * 0.15 * trigger;
                    shiftedUV = clamp(shiftedUV, 0.0, 1.0);
                    float chShift = _uDataMoshChannelShift * trigger;
                    float dmR = texture2D(_MainTex, shiftedUV + float2(chShift, 0.0)).r;
                    float dmG = texture2D(_MainTex, shiftedUV).g;
                    float dmB = texture2D(_MainTex, shiftedUV - float2(chShift, 0.0)).b;
                    float3 dmColor = float3(dmR, dmG, dmB);
                    float blockNoise = rand(blockCoord * 3.7 + dmTime * 0.3);
                    float bandTrigger = step(0.92, blockNoise) * trigger;
                    dmColor = lerp(dmColor, dmColor * float3(0.9, 1.1, 0.85), bandTrigger);
                    color = lerp(color, dmColor, trigger);
                }
                #endif // ifdef mosh

                return fixed4(color, saturate(alpha * _uBlendOpacity));
            }// End of frag
            ENDCG
        }// End of Pass
    }// FallBack "Diffuse"
}// End of shader
    