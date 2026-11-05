// BookFlipComposite のパス 13・14（燃えかす・粒を四角形で描く）で使う
#ifndef BOOK_FLIP_FX_QUADS_INCLUDED
#define BOOK_FLIP_FX_QUADS_INCLUDED
        #include "UnityCG.cginc"
        float4 _BurnCharColor, _BurnEmberColor, _ReviveColor;
        float _FxMode;
        struct FxVertexInput { float4 vertex : POSITION; float4 local : TEXCOORD0; float4 shape : TEXCOORD1; float4 look : TEXCOORD2; float4 seed : TEXCOORD3; };
        struct FxVertexOutput { float4 vertex : SV_POSITION; float4 local : TEXCOORD0; float4 shape : TEXCOORD1; float4 look : TEXCOORD2; float4 seed : TEXCOORD3; };
        FxVertexOutput FxVert(FxVertexInput input)
        {
            FxVertexOutput output;
            output.vertex = UnityObjectToClipPos(input.vertex);
            output.local = input.local;
            output.shape = input.shape;
            output.look = input.look;
            output.seed = input.seed;
            return output;
        }
        float FxHash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
        float FxNoise(float2 p)
        {
            float2 i = floor(p), f = frac(p);
            f = f * f * (3 - 2 * f);
            return lerp(lerp(FxHash(i), FxHash(i + float2(1, 0)), f.x), lerp(FxHash(i + float2(0, 1)), FxHash(i + 1), f.x), f.y);
        }
#endif
