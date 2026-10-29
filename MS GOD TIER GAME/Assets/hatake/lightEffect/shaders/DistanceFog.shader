// 距離フォグ。不透明物と空を描き終えた画面に、深度から求めた距離でフォグの色を重ねる。
Shader "Hidden/hatake/DistanceFog"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Name "DistanceFog"
            ZWrite Off
            ZTest Always
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Assets/hatake/lightEffect/shaders/DistanceFogCommon.hlsl"

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float depth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    bool sky = depth <= 0.000001;
                #else
                    bool sky = depth >= 0.999999;
                    depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, depth);
                #endif
                if (sky && _HatakeFogHeight.w < 0.5)
                {
                    return 0;
                }
                float3 cameraPosition = GetCameraPositionWS();
                float3 positionWS = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                if (sky)
                {
                    // 空は遠く（1km先）にあるものとして扱う
                    positionWS = cameraPosition + normalize(positionWS - cameraPosition) * 1000;
                }
                float amount = HatakeFogAmount(cameraPosition, positionWS);
                return half4(HatakeFogColor(cameraPosition, positionWS), amount);
            }
            ENDHLSL
        }
    }
}
