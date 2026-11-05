Shader "YasudaHaruto/Book Target"
{
    Properties
    {
        [PerRendererData] _MainTex ("Target Texture", 2D) = "black" {}
        _Distortion ("Distortion (UV)", Range(0, 0.05)) = 0.023
        _Frequency ("Distortion Frequency", Range(8, 80)) = 30
        _FlowSpeed ("Flow Speed", Range(0, 5)) = 2
        _Intensity ("Glow Intensity", Range(0, 3)) = 1.2
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend One One
        ColorMask RGB

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            sampler2D _MainTex;
            float _Distortion;
            float _Frequency;
            float _FlowSpeed;
            half _Intensity;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                // Frame the spiral tightly and center its ink on the sprite pivot.
                output.uv = (input.uv - 0.5) * 0.6 + float2(0.475, 0.465);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 phase = input.uv * _Frequency;
                float time = _Time.y * _FlowSpeed;
                float2 broad = sin(phase.yx + float2(time, -time * 0.83));
                float2 detail = sin(phase.yx * 2.37 + phase * 0.7 + float2(-time * 1.36, time));
                float2 uv = input.uv + (0.7 * broad + 0.3 * detail) * _Distortion;

                half glow = tex2D(_MainTex, uv).r * _Intensity * input.color.a;
                return half4(input.color.rgb * glow, 0);
            }
            ENDHLSL
        }
    }
}
