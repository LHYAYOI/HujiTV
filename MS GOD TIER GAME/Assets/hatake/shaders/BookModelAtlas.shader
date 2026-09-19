
Shader "custom/hatake/Book/Model Atlas"
{
    Properties
    {
        _MainTex ("Original book atlas", 2D) = "white" {}
        _LeftTex ("Static left spread", 2D) = "white" {}
        _RightTex ("Static right spread", 2D) = "white" {}
        _FrontTex ("Turning sheet front spread", 2D) = "white" {}
        _BackTex ("Turning sheet back spread", 2D) = "white" {}
        [Toggle] _PageMode ("Turning sheet", Float) = 0
        [Toggle] _SwapFaces ("Swap front and back", Float) = 0
        _DepthBias ("Depth bias", Float) = 0
        _LeftUv ("Left atlas region XYWH", Vector) = (0.619184136,0.661075473,0.295944214,0.315901458)
        _RightUv ("Right atlas region XYWH", Vector) = (0.177338958,0.340298712,0.248702943,0.315900385)
        _PageUv ("Moving sheet atlas region XYWH", Vector) = (0.177276075,0.340249121,0.248828828,0.315999627)
        _LeftCrop ("Left capture region XYWH", Vector) = (0.1125,0.1,0.3875,0.8)
        _RightCrop ("Right capture region XYWH", Vector) = (0.5,0.1,0.3875,0.8)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Cull Off
            ZWrite On
            Offset [_DepthBias], [_DepthBias]
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex, _LeftTex, _RightTex, _FrontTex, _BackTex;
            float4 _LeftTex_TexelSize, _RightTex_TexelSize;
            float4 _FrontTex_TexelSize, _BackTex_TexelSize;
            float4 _LeftUv, _RightUv, _PageUv, _LeftCrop, _RightCrop;
            float _PageMode, _SwapFaces;

            struct VertexInput { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct VertexOutput { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };

            VertexOutput Vert(VertexInput input)
            {
                VertexOutput output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            bool Contains(float2 uv, float4 region)
            {
                return all(uv >= region.xy - 0.000001) &&
                       all(uv <= region.xy + region.zw + 0.000001);
            }

            float2 CropUv(float2 uv, float4 crop, float4 texelSize)
            {
                float2 inset = abs(texelSize.xy) * 0.5;
                return clamp(crop.xy + saturate(uv) * crop.zw,
                    crop.xy + inset, crop.xy + crop.zw - inset);
            }

            fixed4 Frag(VertexOutput input, fixed face : VFACE) : SV_Target
            {
                fixed4 color;
                if (_PageMode > 0.5)
                {
                    float2 uv = (input.uv - _PageUv.xy) / _PageUv.zw;
                    bool frontFlag = face > 0;
                    if (_SwapFaces > 0.5) frontFlag = !frontFlag;
                    if (frontFlag)
                        color = tex2D(_FrontTex, CropUv(uv, _RightCrop, _FrontTex_TexelSize));
                    else
                    {
                        uv.x = 1.0 - uv.x;
                        color = tex2D(_BackTex, CropUv(uv, _LeftCrop, _BackTex_TexelSize));
                    }
                }
                else if (Contains(input.uv, _LeftUv))
                {
                    float2 uv = 1.0 - (input.uv - _LeftUv.xy) / _LeftUv.zw;
                    color = tex2D(_LeftTex, CropUv(uv, _LeftCrop, _LeftTex_TexelSize));
                }
                else if (Contains(input.uv, _RightUv))
                {
                    float2 uv = (input.uv - _RightUv.xy) / _RightUv.zw;
                    color = tex2D(_RightTex, CropUv(uv, _RightCrop, _RightTex_TexelSize));
                }
                else color = tex2D(_MainTex, input.uv);
                color.a = 1;
                return color;
            }
            ENDCG
        }
    }
}
