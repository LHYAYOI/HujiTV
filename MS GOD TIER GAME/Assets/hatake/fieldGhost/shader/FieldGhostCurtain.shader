// 白の半透明カーテン。細かいひだ・風のふくらみと裾のめくれ・裏から押されたふくらみ（顔と手）を頂点で作り、
// ひだの谷に陰を落とし、窓側（裏）からの光を透かす。影も同じ形で落とし、窓からの光や光の筋をさえぎる。形の値は FieldGhostCurtain が MaterialPropertyBlock で書く。
// 色や透け具合はマテリアルで調整する。
Shader "custom/hatake/FieldGhostCurtain"
{
    Properties
    {
        [MainColor] _BaseColor("布の色（αで濃さ）", Color) = (0.93,0.92,0.9,0.8)
        [MainTexture] _BaseMap("布の模様", 2D) = "white" {}
        _EdgeOpacity("斜めから見たときに濃くなる量", Range(0,1)) = 0.6
        _Wrap("光の回り込み（柔らかさ）", Range(0,1)) = 0.6
        _CreaseShade("ひだの谷の陰", Range(0,1)) = 0.5
        _Transmission("裏からの光の透け", Range(0,3)) = 1.0
        _TransmissionColor("透けた光の色", Color) = (1,0.97,0.92,1)
        _AmbientStrength("環境光の強さ", Range(0,2)) = 1
        _StretchClear("押されて薄くなる量", Range(0,1)) = 0.35
        _SilhouetteColor("押している影の色（αで強さ）", Color) = (0.25,0.3,0.45,0.5)
        [HDR] _GhostGlowColor("押している所の発光", Color) = (0.3,0.45,0.75,1)
        _ShadowOpacity("光をさえぎる強さ（影の濃さ）", Range(0,1)) = 0.7
        [HideInInspector] _CurtainSize("Width, height, pleat count, pleat depth", Vector) = (1.2,2.4,16,0.022)
        [HideInInspector] _Wind("Billow, lateral flap, wave phase, flutter", Vector) = (0.08,0,0,0.01)
        [HideInInspector] _Wind2("Twist, pleat irregularity, seed, large fold count", Vector) = (0.1,0.6,4.3,3.5)
        [HideInInspector] _Bulge0("Bulge 0: x, y, radius, depth", Vector) = (0,0,0.1,0)
        [HideInInspector] _Bulge1("Bulge 1: x, y, radius, depth", Vector) = (0,0,0.1,0)
        [HideInInspector] _Bulge2("Bulge 2: x, y, radius, depth", Vector) = (0,0,0.1,0)
        [HideInInspector] _Bulge3("Bulge 3: x, y, radius, depth", Vector) = (0,0,0.1,0)
        [HideInInspector] _BulgeAspect("Vertical scale of each bulge", Vector) = (1,1,1,1)
        [HideInInspector] _FaceDetail("Face dents on bulge 0", Float) = 0
        [HideInInspector] _GodRayBlock("Block god rays (set by FieldGhostCurtain)", Float) = 1
        [HideInInspector] _Fold0("Fold 0: axis, position, angle, radius", Vector) = (0,0,0,0.03)
        [HideInInspector] _Fold1("Fold 1: axis, position, angle, radius", Vector) = (0,0,0,0.03)
        [HideInInspector] _Fold2("Fold 2: axis, position, angle, radius", Vector) = (0,0,0,0.03)
        [HideInInspector] _Fold3("Fold 3: axis, position, angle, radius", Vector) = (0,0,0,0.03)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        LOD 300
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        half4 _BaseColor, _TransmissionColor, _SilhouetteColor, _GhostGlowColor;
        half _EdgeOpacity, _Wrap, _CreaseShade, _Transmission, _AmbientStrength, _StretchClear;
        float4 _CurtainSize, _Wind, _Wind2, _Bulge0, _Bulge1, _Bulge2, _Bulge3, _BulgeAspect;
        float _FaceDetail;
        float4 _Fold0, _Fold1, _Fold2, _Fold3;
        half _ShadowOpacity;
        half _GodRayBlock;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        // 中心で 1、半径の外で 0 になるなめらかな山。p は布のローカル座標（x 右、y 上）。
        float Dome(float2 p, float2 center, float radius, float aspect)
        {
            float2 d = (p - center) / max(radius, 1e-4);
            d.y /= max(aspect, 0.1);
            float s = saturate(1 - length(d));
            return s * s * (3 - 2 * s);
        }

        // 顔のふくらみ: 目と口の所を少しへこませる（布越しに顔だと分かる程度）。
        float FaceDome(float2 p, float4 bulge, float aspect)
        {
            float dome = Dome(p, bulge.xy, bulge.z, aspect);
            float r = bulge.z;
            float dent = Dome(p, bulge.xy + float2(-0.34, 0.16) * r, r * 0.2, 1)
                       + Dome(p, bulge.xy + float2(0.34, 0.16) * r, r * 0.2, 1)
                       + 0.7 * Dome(p, bulge.xy + float2(0, -0.38) * r, r * 0.26, 0.6);
            return max(dome - _FaceDetail * 0.35 * dent * dome, 0);
        }

        // ひだの形（ひだの深さ _CurtainSize.w を 1 とした倍率）。細かいひだ（レール側で寄せられて深い）と、裾ほど大きくなるゆるい折れ目を重ねる。
        // 風が強いと大きな折れ目が斜めにねじれる。amplitude は深さ（m）。
        float Pleat(float xn, float t, out float amplitude)
        {
            float irregular = _Wind2.y;
            float seed = _Wind2.z;
            float side = _Wind.y >= 0 ? 1 : -1;
            // 細かいひだ: 幅が不揃いで、下へ行くほど寄ったり離れたりする。
            float u = (xn + 0.5) * _CurtainSize.z;
            u += irregular * (0.3 * sin(u * 1.3 + seed) + 0.15 * sin(u * 2.9 + seed * 2.3));
            u += irregular * 0.25 * sin(t * 3.5 + u * 0.7 + seed);
            float s = sin(u * TWO_PI);
            float fine = sign(s) * pow(abs(s), 0.8);
            // 大きな折れ目: 風でねじれて斜めに流れ、ゆっくり形を変える。
            float v = xn * _Wind2.w + _Wind2.x * t * side + 0.25 * sin(t * 2.5 + seed) - _Wind.z * 0.08;
            float large = sin(v * TWO_PI + seed);
            float fineDepth = lerp(1.1, 0.85, t);
            float largeDepth = 3.2 * lerp(0.35, 1.2, t);
            amplitude = _CurtainSize.w;
            return 0.5 * (fine * fineDepth + large * largeDepth);
        }

        // 布の 1 点の動き（ローカル、m）。pressMask は押されている度合い、pleat はひだの形。
        float3 CurtainOffset(float2 p, out float pressMask, out float pleat)
        {
            float width = max(_CurtainSize.x, 1e-3);
            float height = max(_CurtainSize.y, 1e-3);
            float xn = p.x / width;
            float t = saturate(-p.y / height);
            // カーテンレールの近くは布が動けない。
            float hold = smoothstep(0, 0.1, t);

            float d0 = FaceDome(p, _Bulge0, _BulgeAspect.x);
            float d1 = Dome(p, _Bulge1.xy, _Bulge1.z, _BulgeAspect.y);
            float d2 = Dome(p, _Bulge2.xy, _Bulge2.z, _BulgeAspect.z);
            float d3 = Dome(p, _Bulge3.xy, _Bulge3.z, _BulgeAspect.w);
            float bulge = d0 * _Bulge0.w + d1 * _Bulge1.w + d2 * _Bulge2.w + d3 * _Bulge3.w;
            // 押しの深さ 8cm で「しっかり押されている」とみなす。
            float mask = d0 * saturate(_Bulge0.w / 0.08) + d1 * saturate(_Bulge1.w / 0.08)
                       + d2 * saturate(_Bulge2.w / 0.08) + d3 * saturate(_Bulge3.w / 0.08);
            pressMask = saturate(mask) * hold;

            // ひだ: 押されて張った所は伸びる。pleat は陰のため -1〜1 に収める。
            float amplitude;
            float folds = Pleat(xn, t, amplitude);
            pleat = clamp(folds, -1, 1);
            float pleatZ = amplitude * (1 - 0.75 * pressMask) * folds;

            // 風: 裾ほど部屋側へふくらみ、斜めの波が布を渡る。
            float phase = _Wind.z;
            float billowShape = pow(t, 1.3) * (0.7 + 0.3 * cos(xn * PI));
            float wave = sin(TWO_PI * (1.1 * t + 0.7 * xn) - phase);
            float wave2 = sin(TWO_PI * (2.3 * t - 1.3 * xn) - phase * 1.7 + 1.0);
            float forward = _Wind.x * billowShape * (1 + 0.35 * wave) + _Wind.w * t * wave2;
            // 突風で片側の裾がめくれて横へあおられる（_Wind.y の符号が向き）。
            float side = _Wind.y >= 0 ? 1 : -1;
            float corner = pow(t, 2.2) * smoothstep(-0.2, 0.5, xn * side);
            float sideways = _Wind.y * corner * (0.8 + 0.2 * wave);
            forward += abs(_Wind.y) * corner * 0.6;
            forward += bulge;
            forward *= hold;
            // 前や横へ出た分だけ裾が持ち上がる（布の長さを保つ近似）。
            float hang = max(height * t, 0.05);
            float lift = min(0.5 * (forward * forward + sideways * sideways) / hang, 0.5 * hang);
            return float3(sideways, lift, forward + pleatZ);
        }

        // ---- 折り曲げ（支点） ----
        // _FoldN = (向き 0:縦の折り目 / 1:横の折り目, 位置 m, 角度 rad（正で奥＝窓側へ）, 曲がりの半径 m)。角度 0 は無効。
        // 縦の折り目は中央から見て外側（左右）を、横の折り目はレールから見て下を曲げる。
        // FieldGhostCurtain が支点からの距離の近い順に並べて渡す。

        // 支点からの距離 s・手前への厚み z の点を、同じ向きの折り目に沿って曲げる。戻り値は (沿う方向, 手前方向) の位置。
        float2 FoldOne(float4 fold, float axis, float s, float z, inout float2 origin, inout float2 tangent, inout float2 normal, inout float start)
        {
            float hinge = axis > 0.5 ? -fold.y : abs(fold.y);
            bool active = abs(fold.x - axis) < 0.5 && abs(fold.z) > 1e-4 && s > hinge && hinge >= start;
            if (active)
            {
                float radius = max(fold.w, 1e-3);
                float bend = fold.z >= 0 ? 1 : -1;
                // 支点では半径 radius の円弧に沿って曲がり、曲がりきったら真っすぐ続く（布の長さは保つ）。
                float2 h = origin + tangent * (hinge - start);
                float2 center = h - normal * (radius * bend);
                float phi = min(s - hinge, radius * abs(fold.z)) / radius * bend;
                float2 t2 = tangent * cos(phi) - normal * sin(phi);
                float2 n2 = normal * cos(phi) + tangent * sin(phi);
                origin = center + n2 * (radius * bend);
                start = hinge + abs(phi) * radius;
                tangent = t2;
                normal = n2;
            }
            return origin + tangent * (s - start) + normal * z;
        }

        float2 FoldStrip(float s, float z, float axis)
        {
            float2 origin = float2(0, 0);
            float2 tangent = float2(1, 0);
            float2 normal = float2(0, 1);
            float start = 0;
            float2 result = FoldOne(_Fold0, axis, s, z, origin, tangent, normal, start);
            result = FoldOne(_Fold1, axis, s, z, origin, tangent, normal, start);
            result = FoldOne(_Fold2, axis, s, z, origin, tangent, normal, start);
            result = FoldOne(_Fold3, axis, s, z, origin, tangent, normal, start);
            return result;
        }

        // 平らな布の座標（x, y）と手前への動き z を、折り曲げたあとの位置にする。
        // 先に横の折り目（下を曲げる）、次に縦の折り目（左右を曲げる）。
        float3 FoldPoint(float3 p)
        {
            float2 down = FoldStrip(max(-p.y, 0), p.z, 1);
            float y = p.y > 0 ? p.y : -down.x;
            float z = p.y > 0 ? p.z : down.y;
            float side = p.x >= 0 ? 1 : -1;
            float2 across = FoldStrip(abs(p.x), z, 0);
            return float3(across.x * side, y, across.y);
        }

        // 布の 1 点の、動いて曲げたあとの位置と法線（ローカル）。メッシュは z=0 の平らな格子。
        void CurtainSurface(float3 positionOS, out float3 position, out float3 normal, out float pressMask, out float pleat)
        {
            float2 p = positionOS.xy;
            const float e = 0.004;
            float unusedMask, unusedPleat;
            float3 o = CurtainOffset(p, pressMask, pleat);
            float3 ox = CurtainOffset(p + float2(e, 0), unusedMask, unusedPleat);
            float3 oy = CurtainOffset(p + float2(0, e), unusedMask, unusedPleat);
            position = FoldPoint(float3(p, positionOS.z) + o);
            float3 du = FoldPoint(float3(p + float2(e, 0), positionOS.z) + ox) - position;
            float3 dv = FoldPoint(float3(p + float2(0, e), positionOS.z) + oy) - position;
            normal = normalize(cross(du, dv));
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CurtainVertex
            #pragma fragment CurtainFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                // x: 霧, yzw: 頂点ライト
                half4 fogAndVertexLight : TEXCOORD3;
                // x: 押されている所ほど 1, y: ひだ（-1 谷が部屋側から奥 〜 1 山が手前）
                half2 pressAndPleat : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings CurtainVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionOS, normalOS;
                float mask, pleat;
                CurtainSurface(input.positionOS.xyz, positionOS, normalOS, mask, pleat);
                float3 positionWS = TransformObjectToWorld(positionOS);
                half3 normalWS = (half3)TransformObjectToWorldNormal(normalOS);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.pressAndPleat = half2(mask, pleat);
                output.fogAndVertexLight = half4(ComputeFogFactor(output.positionCS.z), VertexLighting(positionWS, normalWS));
                return output;
            }

            // 表からの光（回り込みあり）と、裏から透ける光。押している影は裏からの光をさえぎる。
            half3 CurtainLight(Light light, half3 n, half3 albedo, half blocked)
            {
                half3 radiance = light.color * (light.distanceAttenuation * light.shadowAttenuation);
                half ndl = dot(n, light.direction);
                half front = saturate((ndl + _Wrap) / (1 + _Wrap));
                half back = saturate(-ndl) * _Transmission * (1 - blocked);
                return radiance * albedo * (front + _TransmissionColor.rgb * back);
            }

            half4 CurtainFragment(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                bool frontFace = IS_FRONT_VFACE(face, true, false);
                half3 n = SafeNormalize(input.normalWS);
                // 両面表示: 見ている側を表とする。
                n = frontFace ? n : -n;
                half3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half press = input.pressAndPleat.x;
                half blocked = _SilhouetteColor.a * press;
                // 見ている側から奥まったひだの谷ほど暗い（光が入りにくい）。
                half pleat = frontFace ? input.pressAndPleat.y : -input.pressAndPleat.y;
                half cavity = 1 - _CreaseShade * saturate(-pleat) * saturate(-pleat);

                InputData inputData = (InputData)0; // LIGHT_LOOP_BEGIN needs this exact variable.
                inputData.positionWS = input.positionWS;
                inputData.normalWS = n;
                inputData.viewDirectionWS = v;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
#if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                inputData.shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
#else
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
#endif
                half4 shadowMask = half4(1, 1, 1, 1);
                Light mainLight = GetMainLight(inputData.shadowCoord, input.positionWS, shadowMask);
                half3 color = CurtainLight(mainLight, n, base.rgb, blocked);
#if USE_CLUSTER_LIGHT_LOOP
                UNITY_LOOP for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                    Light light = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
                    color += CurtainLight(light, n, base.rgb, blocked);
                }
#endif
#if defined(_ADDITIONAL_LIGHTS) || USE_CLUSTER_LIGHT_LOOP
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
                    color += CurtainLight(light, n, base.rgb, blocked);
                LIGHT_LOOP_END
#endif
#if defined(_ADDITIONAL_LIGHTS_VERTEX)
                color += base.rgb * input.fogAndVertexLight.yzw;
#endif
                color += base.rgb * SampleSH(n) * _AmbientStrength;
                color *= cavity;
                // 押している影の色をのせる。
                color *= lerp(half3(1, 1, 1), _SilhouetteColor.rgb, blocked);
                color = MixFog(color, input.fogAndVertexLight.x);

                // 斜めから見ると布が重なって濃く見える。押されて張った所は薄くなる。
                half fresnel = pow(1 - saturate(abs(dot(n, v))), 3);
                half alpha = saturate(lerp(base.a, 1, fresnel * _EdgeOpacity));
                alpha *= 1 - _StretchClear * press;
                half3 glow = MixFogColor(_GhostGlowColor.rgb * press, half3(0, 0, 0), input.fogAndVertexLight.x);
                // 出力は乗算済みアルファ。発光は加算として足す。
                return half4(max(color, 0) * alpha + glow, alpha);
            }
            ENDHLSL
        }
        // 影: 揺れ・ひだ・折り曲げをそのまま反映して影を落とす。窓からの光の溜まりや光の筋（LightShaft）をさえぎる。
        // 半透明の布なので、_ShadowOpacity の割合だけ網目状に影を落とし、ソフトシャドウでならす。
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CurtainShadowVertex
            #pragma fragment CurtainShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            ShadowVaryings CurtainShadowVertex(Attributes input)
            {
                ShadowVaryings output = (ShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 positionOS, normalOS;
                float mask, pleat;
                CurtainSurface(input.positionOS.xyz, positionOS, normalOS, mask, pleat);
                float3 positionWS = TransformObjectToWorld(positionOS);
                float3 normalWS = TransformObjectToWorldNormal(normalOS);
#if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirection = SafeNormalize(_LightPosition - positionWS);
#else
                float3 lightDirection = _LightDirection;
#endif
                output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection)));
                return output;
            }

            half4 CurtainShadowFragment(ShadowVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                // 4x4 の網目で、濃さの割合だけ残す（影の画素の位置で決まるので動かない）。
                uint2 cell = uint2(input.positionCS.xy) % 4;
                static const float bayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
                float threshold = (bayer[cell.y * 4 + cell.x] + 0.5) / 16;
                clip(_ShadowOpacity - threshold);
                return 0;
            }
            ENDHLSL
        }
        // God Ray（LightShaft の空から出る放射状の光）を遮る。LightShaftFeature がこの LightMode のパスを集めて描き、
        // 空の判定から差し引く。カーテンは深度に書かないので、この分を別に数える。
        Pass
        {
            Name "GodRayOccluder"
            Tags { "LightMode"="GodRayOccluder" }
            ZWrite Off ZTest Always Cull Off
            Blend One One
            BlendOp Max
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CurtainOccluderVertex
            #pragma fragment CurtainOccluderFragment
            #pragma multi_compile_instancing

            struct OccluderVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            OccluderVaryings CurtainOccluderVertex(Attributes input)
            {
                OccluderVaryings output = (OccluderVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionOS, normalOS;
                float mask, pleat;
                CurtainSurface(input.positionOS.xyz, positionOS, normalOS, mask, pleat);
                output.positionCS = TransformObjectToHClip(positionOS);
                return output;
            }

            half4 CurtainOccluderFragment(OccluderVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // 影と同じ「光をさえぎる強さ」だけ空を隠す。
                return half4(saturate(_ShadowOpacity * _GodRayBlock), 0, 0, 0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
