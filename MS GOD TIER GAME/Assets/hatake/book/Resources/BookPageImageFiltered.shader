// ページの画像（UI の Image）を撮影テクスチャへ描くときに、縮小しても細い線・小さな文字が途切れないように読む。
// 元の画像にミップマップが無いと、2倍以上縮小したときに1画素ごとに飛ばして読むことになり、線が欠ける。
// 画素の範囲（ddx/ddy）にわたって数点を読んで平均する（ミップの代わり）。縮小しないところは1回だけ読む。
// UI/Default と同じ使い方（ステンシル・RectMask2D・アルファの切り抜き）に対応している。
// Sprites/Default の画像を置き換えるときは _IgnoreClipRect = 1（もとと同じく切り抜かない）
// BookPageView が、ミップの無い画像に自動で付ける（Resources から読む）
Shader "hatake/BookPageImageFiltered"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
        // 1：RectMask2D の切り抜きを掛けない（Sprites/Default の画像の置き換え用。もとのシェーダーは切り抜かない）
        _IgnoreClipRect ("Ignore Clip Rect", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

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
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4 mask : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;
            int _UIVertexColorAlwaysGammaSpace;
            float _IgnoreClipRect;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                float4 vPosition = UnityObjectToClipPos(v.vertex);
                OUT.worldPosition = v.vertex;
                OUT.vertex = vPosition;

                float2 pixelSize = vPosition.w;
                pixelSize /= float2(1, 1) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));

                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord.xy, _MainTex);
                OUT.mask = float4(v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));

                if (_UIVertexColorAlwaysGammaSpace)
                {
                    if (!IsGammaSpace())
                    {
                        v.color.rgb = UIGammaToLinear(v.color.rgb);
                    }
                }
                OUT.color = v.color * _Color;
                return OUT;
            }

            // 平均は見た目の明るさ（ガンマ）の空間で、不透明度を掛けた色（乗算済み）で取る。リニアのまま平均すると、
            // 白地の細い黒線が薄く細くなる（縮小した文字が薄く、かすれて見える）。画像編集ソフトの縮小と同じ空間。
            // 乗算済みで平均するのは、透明な画素の色（白や黒のごみ）が文字の色に混ざらないようにするため
            half4 Tap(float2 uv)
            {
                half4 c = tex2Dlod(_MainTex, float4(uv, 0, 0));
                #ifndef UNITY_COLORSPACE_GAMMA
                c.rgb = LinearToGammaSpace(saturate(c.rgb));
                #endif
                c.rgb *= c.a;
                return c;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float2 dx = ddx(uv);
                float2 dy = ddy(uv);
                // 出力の1画素が覆う元の画像の画素の数（縦横の大きい方）
                float footprint = max(length(dx * _MainTex_TexelSize.zw), length(dy * _MainTex_TexelSize.zw));

                // p：ガンマの空間で乗算済みの色と不透明度
                half4 p;
                [branch] if (footprint <= 1.3)
                {
                    p = Tap(uv);
                }
                else [branch] if (footprint <= 2.2)
                {
                    // 2×2 点（1点がバイリニアで2×2画素を混ぜるので、画素の範囲をほぼ覆う）
                    p = (Tap(uv + (-0.25 * dx - 0.25 * dy)) + Tap(uv + (0.25 * dx - 0.25 * dy)) +
                         Tap(uv + (-0.25 * dx + 0.25 * dy)) + Tap(uv + (0.25 * dx + 0.25 * dy))) * 0.25;
                }
                else
                {
                    // 3×3 点
                    p = 0;
                    [unroll] for (int j = -1; j <= 1; j++)
                    {
                        [unroll] for (int i = -1; i <= 1; i++)
                            p += Tap(uv + (i / 3.0) * dx + (j / 3.0) * dy);
                    }
                    p /= 9.0;
                }

                // 縮小で半透明になった文字の縁を、描いた画像編集ソフトと同じ「ガンマの空間での重ね方」に合わせる。
                // ハードウェアの重ね方（リニア、One / OneMinusSrcAlpha）で、下地が黒のときと白のときの結果が
                // ガンマの空間で重ねたときと一致するように、出す色と不透明度を決める（その間の明るさの下地ではほぼ一致）。
                // リニアのまま重ねると、白い紙の上の細い黒い文字は薄く（灰色に）なる
                half4 color;
                #ifndef UNITY_COLORSPACE_GAMMA
                {
                    half3 overBlack = GammaToLinearSpace(p.rgb);
                    half3 overWhite = GammaToLinearSpace(saturate(p.rgb + (1 - p.a)));
                    half3 cover = 1 - (overWhite - overBlack);
                    color.a = saturate(dot(cover, half3(0.2126, 0.7152, 0.0722)));
                    color.rgb = overBlack;
                }
                #else
                color = p;
                #endif
                // 頂点色（ふつうは白）と _TextureSampleAdd（ふつうは 0）
                color.rgb = color.rgb * IN.color.rgb + _TextureSampleAdd.rgb * color.a;
                color *= IN.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw);
                if (_IgnoreClipRect < 0.5) color *= m.x * m.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                // 乗算済みで返す（Blend One OneMinusSrcAlpha）
                return color;
            }
        ENDCG
        }
    }
}
