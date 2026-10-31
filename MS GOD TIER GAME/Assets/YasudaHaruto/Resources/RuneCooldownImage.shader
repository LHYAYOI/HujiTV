Shader "YasudaHaruto/UI/Rune Cooldown"
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
  _Recovery ("Recovery", Range(0,1)) = 1
  _Drain ("Drain", Range(0,1)) = 1
  _Darkness ("Remaining brightness", Range(0,1)) = 0.28
  _Flash ("Release light", Range(0,1)) = 0
 }
 SubShader
 {
  Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
  Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
  Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
  Blend SrcAlpha OneMinusSrcAlpha
  ColorMask [_ColorMask]
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
   #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
   #include "UnityCG.cginc"
   #include "UnityUI.cginc"
   struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 height:TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float4 local:TEXCOORD1; float height:TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
   sampler2D _MainTex;
   fixed4 _Color, _TextureSampleAdd;
   float4 _ClipRect;
   float _Recovery, _Drain, _Darkness, _Flash;
   v2f vert(appdata v)
   {
    v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.local=v.vertex; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.height=v.height.x; o.color=v.color*_Color; return o;
   }
   fixed4 frag(v2f i):SV_Target
   {
    fixed4 c=(tex2D(_MainTex,i.uv)+_TextureSampleAdd)*i.color;
    float y=saturate(i.height);
    float recovered=_Recovery<=0 ? 0 : (_Recovery>=1 ? 1 : 1-smoothstep(_Recovery-0.015,_Recovery+0.015,y));
    // Keep the restored area independent of the release flash and darkening.
    fixed3 original=c.rgb;
    fixed3 drained=lerp(original,original*_Darkness,_Drain);
    drained=lerp(drained,i.color.rgb,saturate(_Flash));
    c.rgb=lerp(drained,original,recovered);
    #ifdef UNITY_UI_CLIP_RECT
    c.a*=UnityGet2DClipping(i.local.xy,_ClipRect);
    #endif
    #ifdef UNITY_UI_ALPHACLIP
    clip(c.a-0.001);
    #endif
    return c;
   }
   ENDCG
  }
 }
}
