

Shader "Particles/Dissolve Emission"
{
    Properties
    {
        [Header(Main Texture)]
        _MainTex ("メインテクスチャ", 2D) = "white" {}
        [HDR] _EmissionColor ("発光カラー HDR 乗算", Color) = (1,1,1,1)
        _MainScroll ("UVスクロール速度 XY 1秒あたり", Vector) = (0,0,0,0)

        [Header(Dissolve)]
        _Progress ("進行度 0=全表示 1=全消失", Range(0,1)) = 0
        _NoiseScale ("シェーダ内ノイズの細かさ", Range(1,30)) = 6
        [Toggle(_CUSTOMDATA_ON)] _UseCustomData ("進行度にパーティクルのカスタムデータを加算 UV2.x", Float) = 0
        _DissolveAngle ("消える方向 角度 0=左から右 90=下から上", Range(0,360)) = 90
        [Toggle(_CUTOUT_ON)] _UseCutout ("消えた部分をアルファで抜く ハードエッジ ぼかし無効", Float) = 1
        _DissolveSoftness ("境界のぼかし", Range(0.001,1)) = 0.1
        _NoiseInfluence ("ノイズの強さ 0=直線的 1=ノイズのみ", Range(0,1)) = 0.5
        [Toggle(_NOISE_TEX)] _UseNoiseTex ("ノイズテクスチャを使う OFF=シェーダ内ノイズ", Float) = 0
        _NoiseTex ("ノイズテクスチャ", 2D) = "gray" {}
        _NoiseScroll ("ノイズのスクロール速度 XY", Vector) = (0,0,0,0)

        [Header(Dissolve Edge Glow)]
        [HDR] _EdgeColor ("境界の発光カラー HDR", Color) = (4,1.5,0.2,1)
        _EdgeWidth ("境界の発光幅", Range(0,0.5)) = 0.05

        [Header(Mask)]
        _MaskTex ("マスクテクスチャ 黒=消える 白=残る", 2D) = "white" {}
        [Toggle(_MASK_CUTOUT_ON)] _UseMaskCutout ("マスクで消える部分をアルファで抜く ハードエッジ ぼかし無効", Float) = 1
        _MaskThreshold ("マスクのしきい値", Range(0,1)) = 0
        _MaskSoftness ("マスク境界のぼかし", Range(0.001,1)) = 0.05

        [Header(Rendering)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("ソースブレンド 通常は SrcAlpha", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("デスティネーションブレンド One=加算 OneMinusSrcAlpha=通常", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("カリング Off=両面表示", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Blend [_SrcBlend] [_DstBlend]
        Cull [_Cull]
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma shader_feature _NOISE_TEX
            #pragma shader_feature _CUSTOMDATA_ON
            #pragma shader_feature _CUTOUT_ON
            #pragma shader_feature _MASK_CUTOUT_ON
            #include "UnityCG.cginc"

            sampler2D _MainTex;  float4 _MainTex_ST;
            sampler2D _NoiseTex; float4 _NoiseTex_ST;
            sampler2D _MaskTex;  float4 _MaskTex_ST;

            float4 _EmissionColor;
            float4 _MainScroll;
            float  _Progress;
            float  _DissolveAngle;
            float  _DissolveSoftness;
            float  _NoiseInfluence;
            float  _NoiseScale;
            float4 _NoiseScroll;
            float4 _EdgeColor;
            float  _EdgeWidth;
            float  _MaskThreshold;
            float  _MaskSoftness;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float4 custom : TEXCOORD1; // Particle Custom Vertex Stream (x = progress)
                fixed4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos    : SV_POSITION;
                float2 uv     : TEXCOORD0; // original UV (dissolve / noise)
                float2 mainUV : TEXCOORD1; // scrolled UV
                float2 maskUV : TEXCOORD2;
                float  custom : TEXCOORD3;
                fixed4 color  : COLOR;
            };

            // procedural value noise
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                return valueNoise(p) * 0.6 + valueNoise(p * 2.1 + 7.3) * 0.4;
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.mainUV = TRANSFORM_TEX(v.uv, _MainTex) + _MainScroll.xy * _Time.y;
                o.maskUV = TRANSFORM_TEX(v.uv, _MaskTex);
                o.custom = v.custom.x;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Main
                fixed4 col = tex2D(_MainTex, i.mainUV);
                col.rgb *= _EmissionColor.rgb;
                col *= i.color;               // particle color / alpha
                col.a *= _EmissionColor.a;

                // Dissolve gradient along the chosen axis
                float rad = radians(_DissolveAngle);
                float2 dir = float2(cos(rad), sin(rad));
                float2 p = i.uv - 0.5;
                float grad = dot(p, dir) / (abs(dir.x) + abs(dir.y)) + 0.5; // 0 - 1

                // Noise
                float2 nUV = _NoiseScroll.xy * _Time.y;
            #ifdef _NOISE_TEX
                float n = tex2D(_NoiseTex, TRANSFORM_TEX(i.uv, _NoiseTex) + nUV).r;
            #else
                float n = fbm(i.uv * _NoiseScale + nUV);
            #endif

                float d = lerp(grad, n, _NoiseInfluence);

                // Progress
                float progress = _Progress;
            #ifdef _CUSTOMDATA_ON
                progress += i.custom;
            #endif
                progress = saturate(progress);

                float soft = max(_DissolveSoftness, 0.001);
                float t = lerp(-soft, 1.0 + _EdgeWidth, progress);
            #ifdef _CUTOUT_ON
                clip(d - t);
                float visible = 1.0;
            #else
                float visible = saturate((d - t) / soft);
            #endif

                
                float edge = (_EdgeWidth > 0.0001) ? saturate(1.0 - (d - t) / _EdgeWidth) : 0.0;
                edge *= step(t, d) * saturate(progress * 50.0) * saturate((1.0 - progress) * 50.0);

                col.rgb += _EdgeColor.rgb * edge * col.a;
                col.a *= visible;

                // Mask
                float m = tex2D(_MaskTex, i.maskUV).r;
            #ifdef _MASK_CUTOUT_ON
                clip(m - _MaskThreshold);
            #else
                col.a *= saturate((m - _MaskThreshold) / max(_MaskSoftness, 0.001));
            #endif

                return col;
            }
            ENDCG
        }
    }
}
