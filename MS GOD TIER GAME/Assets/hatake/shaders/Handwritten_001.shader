
Shader "Custom/Hatakeyama/Fix/Handwritten" {
    Properties {
        [HideInInspector] _MainTex          ("Texture",             2D)             = "white" { }
        [HDR] _Color                        ("Color",               Color)          = (1, 1, 1, 1)
        _Contrast                           ("Contrast",            Range(0, 5))    = 1.2
        _Brightness                         ("Brightness",          Range(-2, 2))   = 0.0
        [Toggle(U_SKECH_ON)] _uSketchEnabled            ("sketchEnabled",       Float)          = 1.0
        _uStrokeThickness                   ("strokeThickness",     Range(0.5, 5))  = 1.5
        _uStrokeStrength                    ("strokeStrength",      Range(0, 5))    = 2.5
        [Toggle(U_HATCH_ON)] _uCrossHatchEnabled        ("crossHatchEnabled",   Float)          = 1.0
        _uHatchScale                        ("hatchScale",          Range(10, 200)) = 60.0
        _uHatchDensity                      ("hatchDensity",        Range(0, 1))    = 0.5
        _uHatchWobble                       ("hatchWobble",         Range(0, 1))    = 0.3
        [Toggle(U_PAPER_ON)] _uPaperEnabled             ("paperEnabled",        Float)          = 1.0
        [HDR] _uPaperColor                  ("paperColor",          Color)          = (0.96, 0.93, 0.88, 1.0)
        _uPaperGrain                        ("paperGrain",          Range(0, 1))    = 0.3
        [Toggle] _uColorEnabled             ("colorEnabled",        Float)          = 0.0
        _uBlendOpacity                      ("blendOpacity",        Range(0, 1))    = 1.0

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

            #pragma shader_feature_local _ U_SKECH_ON
            #pragma shader_feature_local _ U_HATCH_ON
            #pragma shader_feature_local _ U_PAPER_ON

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
            float _uSketchEnabled;
            float _uStrokeThickness;
            float _uStrokeStrength;
            float _uCrossHatchEnabled;
            float _uHatchScale;
            float _uHatchDensity;
            float _uHatchWobble;
            float _uPaperEnabled;
            float4 _uPaperColor;
            float _uPaperGrain;
            float _uColorEnabled;
            float _uBlendOpacity;

            float getLuminance(float3 c) {
                return dot(c, float3(0.299, 0.587, 0.114));
            }

            float hash(float2 p) {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float noise(float2 p) {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(hash(i + float2(0, 0)), hash(i + float2(1, 0)), u.x),
                    lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), u.x),
                    u.y);
            }

            // Sobel
            float sobelEdge(float2 uv, float2 texel) {
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
                return sqrt(gx * gx + gy * gy);
            }

            // Singlehatch
            float hatchLayer(float2 uv, float2 dir, float scale, float wobble) {
                float2 perturbedUV = uv + (noise(uv * 35.0) - 0.5) * wobble * 0.025;
                float projected = dot(perturbedUV, dir) * scale;
                return abs(frac(projected) - 0.5) * 2.0;
            }

            // Crosshatch
            float crossHatch(float2 uv, float luma, float scale, float density, float wobble) {
                float lineWidth = 0.08 + (1.0 - density) * 0.25;
                float hatch = 1.0;
                float s1 = 1.0 - smoothstep(0.65, 0.90, luma);
                hatch -= (1.0 - smoothstep(0.0, lineWidth, hatchLayer(uv, normalize(float2( 1.0,  1.0)), scale, wobble))) * s1;
                float s2 = 1.0 - smoothstep(0.35, 0.65, luma);
                hatch -= (1.0 - smoothstep(0.0, lineWidth, hatchLayer(uv, normalize(float2( 1.0, -1.0)), scale, wobble))) * s2;
                float s3 = 1.0 - smoothstep(0.10, 0.35, luma);
                hatch -= (1.0 - smoothstep(0.0, lineWidth, hatchLayer(uv, float2(0.0, 1.0), scale, wobble))) * s3;
                float s4 = 1.0 - smoothstep(0.0, 0.10, luma);
                hatch -= (1.0 - smoothstep(0.0, lineWidth, hatchLayer(uv, float2(1.0, 0.0), scale, wobble))) * s4;

                return saturate(hatch);
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
                float alpha = texColor.a * _Color.a;

                color = (color - 0.5) * _Contrast + 0.5 + _Brightness;
                color = saturate(color);

                float luma = getLuminance(color);
                float3 paperColor = _uPaperEnabled > 0.5 ? _uPaperColor.rgb : float3(1.0, 1.0, 1.0);
                float3 inkColor   = float3(0.05, 0.04, 0.03);
                float3 result     = paperColor;

                #if defined (U_HATCH_ON)
                    float hatch = crossHatch(uv, luma, _uHatchScale, _uHatchDensity, _uHatchWobble);
                    result = lerp(inkColor, paperColor, hatch);
                #endif // if defined (U_HATCH_ON)

                #if defined (U_SKECH_ON)
                    float2 texel = _uStrokeThickness / _uResolution;
                    float edge = sobelEdge(uv, texel) * _uStrokeStrength;
                    float wobble = noise(uv * 150.0 + float2(time * 1.5, time * 2.3)) * 0.18;
                    edge = saturate(edge + edge * wobble);
                    result = lerp(result, inkColor, saturate(edge));
                #endif // if defined (U_SKECH_ON)


                #if defined (U_PAPER_ON)
                if (_uPaperGrain > 0.0) {
                    float grain = (noise(uv * _uResolution * 0.25) - 0.5) * _uPaperGrain * 0.10;
                    result = saturate(result + grain);
                }
                #endif // if defined (U_PAPER_ON)

                if (_uColorEnabled > 0.5) {
                    result = lerp(result, result * saturate(color * 1.5), 0.6);
                }

                return fixed4(result, saturate(alpha * _uBlendOpacity));
            }
            ENDCG
        }// End of Pass
    }// End of SubShader
}// End of shader