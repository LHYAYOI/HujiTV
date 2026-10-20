Shader "Hidden/Book/Page Flip Post"
{
    // BookFlipView の後処理。HDRで合成した本に、Bloom・暗転時のぼかし・トーンマップをかける。
    // 本の表示は Screen Space Overlay のUIなので、カメラのポストエフェクト（Volume）は効かない。
    // そのため本の中で完結させる。
    Properties
    {
        _MainTex ("Source", 2D) = "black" {}
    }
    CGINCLUDE
    #include "UnityCG.cginc"
    #include "BookFlipCore.cginc"
    #include "BookFlipLook.cginc"

    sampler2D _MainTex, _HigherTex, _BloomTex, _BlurTex;
    float4 _MainTex_TexelSize;
    float4 _BloomThreshold;   // x 閾値, y ニー, z 上限, w 散乱（上位レベルへの足し込み）
    float4 _BloomTint;        // rgb 色, a 強さ
    float _DimBlur, _Tonemap, _Exposure;

    struct VertexInput { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
    struct VertexOutput { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };

    VertexOutput Vert(VertexInput input)
    {
        VertexOutput output;
        output.vertex = UnityObjectToClipPos(input.vertex);
        output.uv = input.uv;
        return output;
    }

    float4 Tap(sampler2D tex, float2 uv) { return tex2Dlod(tex, float4(uv, 0, 0)); }

    // 縮小（Dual filter）：中央と斜め4点
    float4 Down(float2 uv)
    {
        float2 o = _MainTex_TexelSize.xy * 0.5;
        float4 c = Tap(_MainTex, uv) * 4;
        c += Tap(_MainTex, uv + float2(-o.x, -o.y));
        c += Tap(_MainTex, uv + float2(o.x, -o.y));
        c += Tap(_MainTex, uv + float2(-o.x, o.y));
        c += Tap(_MainTex, uv + float2(o.x, o.y));
        return c / 8;
    }

    // 拡大（Dual filter）：周囲8点
    float4 Up(float2 uv)
    {
        float2 o = _MainTex_TexelSize.xy * 0.5;
        float4 c = 0;
        c += Tap(_MainTex, uv + float2(-o.x * 2, 0));
        c += Tap(_MainTex, uv + float2(o.x * 2, 0));
        c += Tap(_MainTex, uv + float2(0, -o.y * 2));
        c += Tap(_MainTex, uv + float2(0, o.y * 2));
        c += Tap(_MainTex, uv + float2(-o.x, o.y)) * 2;
        c += Tap(_MainTex, uv + float2(o.x, o.y)) * 2;
        c += Tap(_MainTex, uv + float2(-o.x, -o.y)) * 2;
        c += Tap(_MainTex, uv + float2(o.x, -o.y)) * 2;
        return c / 12;
    }

    float4 FragPrefilter(VertexOutput i) : SV_Target
    {
        float4 c = Down(i.uv);
        // 本の外（透明）の色は使わない
        c.rgb *= saturate(c.a * 4);
        float bright = max(c.r, max(c.g, c.b));
        float knee = max(_BloomThreshold.y, 1e-4);
        float soft = clamp(bright - _BloomThreshold.x + knee, 0, 2 * knee);
        soft = soft * soft / (4 * knee + 1e-5);
        float contribution = max(soft, bright - _BloomThreshold.x) / max(bright, 1e-5);
        return float4(min(c.rgb * contribution, _BloomThreshold.z), 1);
    }

    float4 FragDown(VertexOutput i) : SV_Target { return Down(i.uv); }

    float4 FragUpAdd(VertexOutput i) : SV_Target
    {
        return Tap(_HigherTex, i.uv) + Up(i.uv) * _BloomThreshold.w;
    }

    float4 FragUp(VertexOutput i) : SV_Target { return Up(i.uv); }

    // 明るい部分だけを丸める（0.8 までは変えない）。紙の色はそのまま残す
    float3 SoftClip(float3 c)
    {
        const float shoulder = 0.8;
        float3 over = max(c - shoulder, 0);
        return min(c, shoulder) + over / (1 + over / (1 - shoulder));
    }

    float4 FragComposite(VertexOutput i) : SV_Target
    {
        float4 c = Tap(_MainTex, i.uv);
        if (_DimBlur > 0)
        {
            float blur = saturate(BookDimBlurMask(BookFromUV(i.uv)) * _DimBlur);
            float4 b = Tap(_BlurTex, i.uv);
            c = lerp(c, b, blur);
        }
        float3 bloom = Tap(_BloomTex, i.uv).rgb * _BloomTint.rgb * _BloomTint.a;
        c.rgb += bloom;
        // 光が本の外へにじむ分は不透明度に足す
        c.a = saturate(max(c.a, max(bloom.r, max(bloom.g, bloom.b))));
        c.rgb *= _Exposure;
        if (_Tonemap > 0.5) c.rgb = SoftClip(c.rgb);
        return c;
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always Blend Off
        Pass { CGPROGRAM
            #pragma vertex Vert
            #pragma fragment FragPrefilter
            #pragma target 3.5
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDown
            #pragma target 3.5
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex Vert
            #pragma fragment FragUpAdd
            #pragma target 3.5
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex Vert
            #pragma fragment FragUp
            #pragma target 3.5
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite
            #pragma target 3.5
            ENDCG }
    }
}
