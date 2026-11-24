// ページの UI の一番上に重ねる覆い（BookPageView が使う）。ページの撮影に焼き込まれる。
//   ・白紙：未獲得のページの中身を紙の色で隠す
//   ・獲得の光：起点から見開き全体へ、ぎざぎざの境目で指定色の光が広がる（塗らずに足す）。
//     全部光ったら強まり、ぎざぎざに引いて光の中からページが出てくる。
//     1 を超える明るさで書く（撮影テクスチャは HDR）。本の後処理の Bloom で光る
//   ・暗転：黒を重ねる
// 1枚の四角形を1回描くだけ。テクスチャは読まない。
Shader "hatake/BookPageOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BlankColor ("Blank Color", Color) = (1,1,1,1)
        _Blank ("Blank", Float) = 0
        _Dim ("Dim", Float) = 0
        _FillColor ("Fill Color", Color) = (1,0.8,0.3,1)
        _Fill ("Fill", Float) = 0
        _FillAlpha ("Fill Alpha", Float) = 0
        _Origin ("Origin (spread 0-1)", Vector) = (0.5,0.5,0,0)
        _Side ("Side (0 left, 1 right)", Float) = 0
        _PageAspect ("Page height / width", Float) = 1.16
        _Edge ("Edge (x width, y noise scale, z light 0-1)", Vector) = (0.18,9,0,0)
        [NoScaleOffset] _NoiseTex ("Noise (repeat, bilinear)", 2D) = "gray" {}
        _Glow ("Glow (x fill, y rim, z flash gain, w whiten)", Vector) = (1.6,4,2.5,0.6)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        // 乗算済みの合成：覆う層（白紙・暗転）はアルファで隠し、光は足す
        Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _BlankColor, _FillColor;
            float _Blank, _Dim, _Fill, _FillAlpha, _Side, _PageAspect;
            float4 _Origin, _Edge, _Glow, _ClipRect;
            sampler2D _NoiseTex;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                // 下地（覆う層）：白紙なら紙の色で中身を隠し、暗転なら黒を重ねる。乗算済みの色で持つ
                float3 color = _BlankColor.rgb * _Blank;
                float alpha = _Blank;
                color *= 1 - _Dim;
                alpha = 1 - (1 - alpha) * (1 - _Dim);

                // 獲得の光（足す層）：起点からぎざぎざの境目で広がり、見開き全体が指定色に光る。
                // 色で塗りつぶさず、下のページに光を足す（1 を超える明るさで本の Bloom が光らせる）
                if (_FillAlpha > 0.001)
                {
                    float2 p = float2(_Side + i.uv.x, i.uv.y * _PageAspect);
                    float2 o = float2(_Origin.x * 2, _Origin.y * _PageAspect);
                    float2 far = max(o, float2(2, _PageAspect) - o);
                    float reach = length(far) + _Edge.x;
                    // ぎざぎざ：小さな乱数のテクスチャを2つの細かさで読む（計算で作るより軽い。1テクセル = 模様1つ）
                    float2 nuv = p * _Edge.y / 64;
                    float n = tex2D(_NoiseTex, nuv).r * 0.65 + tex2D(_NoiseTex, nuv * 2.7 + 0.37).r * 0.35;
                    float field = length(p - o) + n * _Edge.x;
                    float inside = _Fill * reach - field;
                    float cover = saturate(inside / 0.006);
                    // 広がっていく境目のすぐ内側をいちばん強く光らせる
                    float rim = cover * saturate(1 - inside / max(_Edge.x * 0.35, 1e-3)) * (1 - step(1, _Fill));
                    // 全部光ったら強まる（光の中で白紙が中身に変わる）。Whiten で白へ寄せられる
                    float light = _Edge.z;
                    float3 hue = lerp(_FillColor.rgb, 1, light * _Glow.w);
                    // 光が引くときは、ぎざぎざに（広がりと同じ模様で）抜けて、光の中からページが出てくる
                    float remain = saturate(_FillAlpha * 1.6 - n * 0.6);
                    float3 glow = hue * _Glow.x * lerp(1, _Glow.z, light) + rim * _Glow.y * lerp(_FillColor.rgb, 1, 0.35);
                    // 白い紙の上でも光の色が分かるように、光っている所を少しだけ光の色へ寄せる（_Glow.x の 1 を超えない分）
                    float lit = cover * remain * _FillColor.a;
                    float tint = lit * 0.3;
                    color = color * (1 - tint) + hue * tint;
                    alpha = alpha + tint * (1 - alpha);
                    color += glow * lit;
                }

                float4 result = float4(color, alpha);
                #ifdef UNITY_UI_CLIP_RECT
                result *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
