

Shader "custom/hatake/Toon"
{
    Properties
    {
        [MainTexture] _BaseMap("ベース画像", 2D) = "white" {}
        [MainColor] _BaseColor("ベース色", Color) = (0.8,0.65,0.5,1)
        _ShadowColor("影の色（乗算）", Color) = (0.62,0.57,0.72,1)
        _ShadowThreshold("影になる範囲", Range(-1,1)) = 0.1
        _ShadowSoftness("境界の柔らかさ", Range(0.001,0.5)) = 0.035
        _SpecularStrength("ツヤの強さ", Range(0,2)) = 0.25
        _ShadeMode("陰影方式", Float) = 0
        _DeepShadowColor("2影の色（乗算）", Color) = (0.35,0.32,0.48,1)
        _DeepThreshold("2影になる範囲", Range(-1,1)) = -0.3
        _DeepSoftness("2影の柔らかさ", Range(0.001,0.5)) = 0.035
        _RampMap("陰影Ramp画像", 2D) = "white" {}
        _SpecularMode("ツヤの方式", Float) = 1
        _SpecularColor("ツヤの色", Color) = (1,1,1,1)
        _SpecularWidth("ツヤの幅", Range(0.001,0.5)) = 0.08
        _SpecularSoftness("ツヤの境界幅", Range(0.001,0.2)) = 0.015
        _NormalEnabled("凹凸の画像を使用", Float) = 0
        [Normal] _NormalMap("凹凸の画像", 2D) = "bump" {}
        _NormalScale("凹凸の強さ", Range(0,2)) = 1
        _ControlEnabled("Toon制御画像を使用", Float) = 0
        _ControlMap("Toon制御画像", 2D) = "white" {}
        _ThresholdMaskStrength("画像による影範囲の補正", Range(0,1)) = 0
        _SurfaceEnabled("材質画像を使用", Float) = 0
        _SurfaceMap("材質画像", 2D) = "white" {}
        _Metallic("金属度", Range(0,1)) = 0
        _Roughness("表面の粗さ", Range(0.04,1)) = 0.5
        _Occlusion("素材の明るさ（AO）", Range(0,1)) = 1
        _ReceiveShadows("落ち影を受ける", Float) = 1
        _ReceiveShadowStrength("落ち影の強さ", Range(0,1)) = 1
        _AmbientStrength("環境光の強さ", Range(0,2)) = 1
        _AdditionalStrength("追加ライトの強さ", Range(0,2)) = 1
        _LightColorInfluence("ライト色の影響", Range(0,1)) = 1
        _LightIntensityInfluence("強いライトの影響", Range(0,1)) = 1
        _SSAOReceive("SSAOの影響", Range(0,1)) = 1
        _ColorPreservation("ベース色を保つ", Range(0,1)) = 0
        _RimEnabled("輪郭の光を使用", Float) = 0
        _RimColor("輪郭の光の色", Color) = (1,1,1,1)
        _RimStrength("輪郭の光の強さ", Range(0,2)) = 0.2
        _RimPower("輪郭の光の細さ", Range(0.1,12)) = 4
        _RimLightAlignment("輪郭光をライト方向へ寄せる", Range(0,1)) = 0
        _ReflectionEnabled("周囲の映り込みを使用", Float) = 0
        _ReflectionStrength("周囲の映り込みの強さ", Range(0,3)) = 1
        _EmissionEnabled("自己発光を使用", Float) = 0
        [HDR] _EmissionColor("自己発光の色", Color) = (0,0,0,1)
        _EmissionMapEnabled("自己発光の画像を使用", Float) = 0
        _EmissionMap("自己発光の画像", 2D) = "white" {}
        _FaceMode("顔の陰影", Float) = 0
        _FaceBlend("顔の陰影を整える強さ", Range(0,1)) = 0.65
        _FaceSDFEnabled("顔SDF画像を使用", Float) = 0
        _FaceSDFMap("顔SDF画像", 2D) = "white" {}
        [HideInInspector] _FaceForward("顔の前方向", Vector) = (0,0,1,0)
        [HideInInspector] _FaceRight("顔の右方向", Vector) = (1,0,0,0)
        _HairFlowEnabled("毛流れの画像を使用", Float) = 0
        _HairFlowMap("毛流れの画像", 2D) = "gray" {}
        _HairShift("髪のツヤの位置", Range(-1,1)) = 0.1
        _HairSecondaryStrength("髪の副ハイライト", Range(0,1)) = 0.25
        _HairBandStrength("髪の帯状のツヤ", Range(0,1)) = 0
        _OutlineEnabled("輪郭線を使用", Float) = 0
        _OutlineColor("輪郭線の色", Color) = (0.06,0.04,0.09,1)
        _OutlineWidth("輪郭線の幅（pixel）", Range(0,8)) = 1.5
        _OutlineMaskEnabled("輪郭線の画像を使用", Float) = 0
        _OutlineMask("輪郭線の幅画像", 2D) = "white" {}
        [HideInInspector] _HasFaceSDFMap("Face SDF available", Float) = 0
        [HideInInspector] _HasHairFlowMap("Hair flow available", Float) = 0
        [HideInInspector] _HasEmissionMap("Emission map available", Float) = 0
        [HideInInspector] _HasOutlineMask("Outline mask available", Float) = 0
        _IsHair("前髪越し表示の遮蔽物（髪）", Float) = 0
        _RevealEnabled("前髪越しに表示", Float) = 0
        _RevealOpacity("前髪越しの濃さ", Range(0,1)) = 0.5
        _RevealMaxDepth("透かす最大距離（m）", Range(0.001,0.5)) = 0.1
        _CharacterId("キャラクター識別番号", Range(1,255)) = 1
        _AlphaClip("透明部分を切り抜く", Float) = 0
        _Cutoff("切り抜きの境界値", Range(0,1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("裏面の表示", Float) = 2
    }


    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 300
        Cull [_Cull]

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ToonVertex
            #pragma fragment ToonFragment
            #pragma shader_feature_local_fragment _ _TOON_SHADE_THREE _TOON_SHADE_SMOOTH _TOON_SHADE_UNLIT _TOON_SHADE_RAMP
            #pragma shader_feature_local_fragment _ _TOON_SPECULAR _TOON_SPECULAR_HAIR
            #pragma shader_feature_local _ _TOON_NORMALMAP
            #pragma shader_feature_local_fragment _ _TOON_CONTROL_MAP
            #pragma shader_feature_local_fragment _ _TOON_SURFACE_MAP
            #pragma shader_feature_local_fragment _ _TOON_ALPHA_CLIP
            #pragma shader_feature_local_fragment _ _TOON_RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local_fragment _ _TOON_RIM
            #pragma shader_feature_local_fragment _ _TOON_REFLECTION
            #pragma shader_feature_local_fragment _ _TOON_EMISSION
            #pragma shader_feature_local_fragment _ _TOON_FACE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "TeamToonForward.hlsl"
            ENDHLSL
        }


        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ToonShadowVertex
            #pragma fragment ToonDepthFragment
            #pragma shader_feature_local_fragment _ _TOON_ALPHA_CLIP
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "TeamToonPasses.hlsl"
            ENDHLSL
        }


        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ToonPassVertex
            #pragma fragment ToonDepthFragment
            #pragma shader_feature_local_fragment _ _TOON_ALPHA_CLIP
            #pragma multi_compile_instancing
            #include "TeamToonPasses.hlsl"
            ENDHLSL
        }


        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ToonPassVertex
            #pragma fragment ToonDepthNormalsFragment
            #pragma shader_feature_local_fragment _ _TOON_ALPHA_CLIP
            #pragma shader_feature_local _ _TOON_NORMALMAP
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            #include "TeamToonPasses.hlsl"
            ENDHLSL
        }


        Pass
        {
            Name "Meta"
            Tags { "LightMode"="Meta" }
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ToonMetaVertex
            #pragma fragment ToonMetaFragment
            #pragma shader_feature_local_fragment _ _TOON_ALPHA_CLIP
            #pragma shader_feature_local_fragment _ _TOON_EMISSION
            #include "TeamToonPasses.hlsl"
            ENDHLSL
        }


        Pass
        {
            Name "TeamToonOutline"
            Tags { "LightMode"="TeamToonOutline" }
            Cull Front ZWrite Off ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ToonOutlineVertex
            #pragma fragment ToonOutlineFragment
            #pragma shader_feature_local_fragment _ _TOON_ALPHA_CLIP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "TeamToonPasses.hlsl"
            ENDHLSL
        }


        Pass
        {
            Name "TeamToonHairMask"
            Tags { "LightMode"="TeamToonHairMask" }
            ZWrite Off ZTest Equal
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ToonPassVertex
            #pragma fragment ToonHairMaskFragment
            #pragma shader_feature_local_fragment _ _TOON_ALPHA_CLIP
            #pragma multi_compile_instancing
            #include "TeamToonPasses.hlsl"
            ENDHLSL
        }


        Pass
        {
            Name "TeamToonReveal"
            Tags { "LightMode"="TeamToonReveal" }
            ZWrite Off ZTest Greater Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ToonPassVertex
            #pragma fragment ToonRevealFragment
            #pragma shader_feature_local_fragment _ _TOON_ALPHA_CLIP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "TeamToonPasses.hlsl"
            ENDHLSL
        }
    }

    CustomEditor "TeamToon.Editor.TeamToonShaderGUI"

    FallBack Off
}

