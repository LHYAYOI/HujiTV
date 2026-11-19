using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Hatake.ScreenFx
{
    /// <summary>
    /// 画面全体の縁取り演出(紫の星空ともや / ざらついた縁の暗がり)。
    /// シーンに 1 つ置くだけで動く。URP のレンダラー設定は変更しない(実行時にパスを差し込む)。
    /// 強さが 0 の間はパスを差し込まないので負荷は掛からない。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class ScreenEdgeFx : MonoBehaviour
    {
        [Serializable]
        public sealed class NebulaSettings
        {
            [Range(0f, 1f)] public float weight = 0f;

            [Header("縁の形")]
            [Tooltip("何も掛からない中央の箱の半分の大きさ(0〜1)")]
            public Vector2 boxSize = new Vector2(0.3f, 0.55f);
            [Tooltip("もやが出始める距離(箱の縁 = 0、画面の端 = 1)")]
            [Range(0f, 1.5f)] public float start = 0.4f;
            [Tooltip("もやが最も濃くなる距離")]
            [Range(0f, 2f)] public float end = 1.2f;
            [Tooltip("縁の境目をノイズで崩す量")]
            [Range(0f, 1.5f)] public float noiseAmount = 0.6f;

            [Header("もや")]
            public Color deepColor = new Color(0.10f, 0.03f, 0.26f, 0.85f);
            public Color glowColor = new Color(0.47f, 0.25f, 0.80f, 0.95f);
            public float noiseScale = 0.9f;
            public Vector2 scroll = new Vector2(0.010f, 0.004f);

            [Header("上下の雲")]
            public Color bandColor = new Color(0.36f, 0.20f, 0.72f, 0.8f);
            [Tooltip("雲が出始める高さ(画面中央 = 0、上下の端 = 1)")]
            [Range(0f, 1f)] public float bandStart = 0.7f;
            [Range(0.01f, 0.5f)] public float bandFeather = 0.1f;
            [Range(0f, 1f)] public float bandThreshold = 0.45f;
            [Range(1f, 40f)] public float bandSharpness = 12f;
            public Vector2 bandScale = new Vector2(0.8f, 3.5f);
            public float bandScroll = 0.012f;

            [Header("星")]
            [ColorUsage(false, true)] public Color starColor = new Color(0.85f, 0.80f, 1.0f, 1f);
            [Range(0f, 4f)] public float starIntensity = 1.6f;
            [Tooltip("星が出始める距離(0 にすると中央の箱の外すべて)")]
            [Range(0f, 1.5f)] public float starStart = 0.1f;
            [Range(0.01f, 1f)] public float starFeather = 0.3f;
            public float starScale = 1.6f;
            public Vector2 starScroll = new Vector2(0.004f, 0.010f);
        }

        [Serializable]
        public sealed class GrungeSettings
        {
            [Range(0f, 1f)] public float weight = 0f;

            [Header("縁の形")]
            public Vector2 boxSize = new Vector2(0.1f, 0.05f);
            [Range(0f, 1.5f)] public float start = 0.64f;
            [Range(0f, 2f)] public float end = 1.45f;
            [Range(0f, 1.5f)] public float noiseAmount = 0.3f;

            [Header("色")]
            public Color color = new Color(0f, 0f, 0f, 1f);

            [Header("ざらつき")]
            public float blotchScale = 0.7f;
            public float grainScale = 1.9f;
            [Range(0f, 2f)] public float grain = 0.7f;

            [Header("動き")]
            [Tooltip("1 秒あたりのコマ数。コマ送りのようにノイズを入れ替える(0 で止める)")]
            [Range(0f, 30f)] public float stepsPerSecond = 10f;
            [Tooltip("コマごとにノイズをずらす量(0 = 動かさない、1 = 毎回まったく別の模様)")]
            [Range(0f, 1f)] public float jitter = 0.06f;
        }

        [SerializeField] private Shader m_shader;
        [Tooltip("空なら実行時に作る。差し替える場合は R:もや G:ざらつき B:星 のリピート可能なテクスチャ")]
        [SerializeField] private Texture2D m_noiseTexture;
        [Tooltip("空なら、画面へ直接描くベースカメラすべてに掛ける。カメラスタックを使う場合は最後のオーバーレイカメラを指定する")]
        [SerializeField] private Camera m_targetCamera;
        [Tooltip("掛ける位置。既定の AfterRendering は最後の画面へ直接描く(追加のコピーが起きない)。AfterRenderingPostProcessing にすると Render Scale の解像度で描くが、URP が画面のコピーを 1 回足すことがある")]
        [SerializeField] private RenderPassEvent m_passEvent = RenderPassEvent.AfterRendering;
        [Tooltip("モバイルではテクスチャ読みを 5 回から 3 回に減らす")]
        [SerializeField] private bool m_liteOnMobile = true;
        [Tooltip("Time.timeScale を無視して動かす")]
        [SerializeField] private bool m_useUnscaledTime = false;
        [Tooltip("シーンビューにも表示する")]
        [SerializeField] private bool m_showInSceneView = false;

        [SerializeField] private NebulaSettings m_nebula = new NebulaSettings();
        [SerializeField] private GrungeSettings m_grunge = new GrungeSettings();

        private const int k_noiseSize = 256;
        private const string k_liteKeyword = "_EDGEFX_LITE";

        private static readonly int s_noiseTexId = Shader.PropertyToID("_NoiseTex");
        private static readonly int s_aspectId = Shader.PropertyToID("_EdgeFxAspect");
        private static readonly int s_nebBoxId = Shader.PropertyToID("_NebBox");
        private static readonly int s_nebMaskId = Shader.PropertyToID("_NebMask");
        private static readonly int s_nebSt0Id = Shader.PropertyToID("_NebSt0");
        private static readonly int s_nebSt1Id = Shader.PropertyToID("_NebSt1");
        private static readonly int s_nebSt2Id = Shader.PropertyToID("_NebSt2");
        private static readonly int s_nebSt3Id = Shader.PropertyToID("_NebSt3");
        private static readonly int s_nebSt4Id = Shader.PropertyToID("_NebSt4");
        private static readonly int s_nebColorAId = Shader.PropertyToID("_NebColorA");
        private static readonly int s_nebColorBId = Shader.PropertyToID("_NebColorB");
        private static readonly int s_nebBandColorId = Shader.PropertyToID("_NebBandColor");
        private static readonly int s_nebStarColorId = Shader.PropertyToID("_NebStarColor");
        private static readonly int s_nebBandId = Shader.PropertyToID("_NebBand");
        private static readonly int s_nebStarId = Shader.PropertyToID("_NebStar");
        private static readonly int s_gruBoxId = Shader.PropertyToID("_GruBox");
        private static readonly int s_gruMaskId = Shader.PropertyToID("_GruMask");
        private static readonly int s_gruSt0Id = Shader.PropertyToID("_GruSt0");
        private static readonly int s_gruSt1Id = Shader.PropertyToID("_GruSt1");
        private static readonly int s_gruColorId = Shader.PropertyToID("_GruColor");
        private static readonly int s_gruParamsId = Shader.PropertyToID("_GruParams");

        private ScreenEdgeFxPass m_pass;
        private Material m_material;
        private Texture2D m_generatedNoise;
        private Mesh m_mesh;
        private readonly Vector3[] m_vertices = new Vector3[16];
        private Vector2 m_meshHole = new Vector2(-1f, -1f);

        private float m_nebulaFadeTarget;
        private float m_nebulaFadeSpeed;
        private float m_grungeFadeTarget;
        private float m_grungeFadeSpeed;

        public NebulaSettings Nebula => m_nebula;
        public GrungeSettings Grunge => m_grunge;

        /// <summary>星空ともやの強さ(0〜1)。</summary>
        public float NebulaWeight
        {
            get => m_nebula.weight;
            set
            {
                m_nebula.weight = Mathf.Clamp01(value);
                m_nebulaFadeSpeed = 0f;
            }
        }

        /// <summary>ざらついた縁の強さ(0〜1)。</summary>
        public float GrungeWeight
        {
            get => m_grunge.weight;
            set
            {
                m_grunge.weight = Mathf.Clamp01(value);
                m_grungeFadeSpeed = 0f;
            }
        }

        /// <summary>星空ともやを seconds 秒かけて target の強さにする。</summary>
        public void FadeNebula(float target, float seconds)
        {
            m_nebulaFadeTarget = Mathf.Clamp01(target);
            if (seconds <= 0f)
            {
                NebulaWeight = m_nebulaFadeTarget;
                return;
            }

            m_nebulaFadeSpeed = Mathf.Abs(m_nebulaFadeTarget - m_nebula.weight) / seconds;
        }

        /// <summary>ざらついた縁を seconds 秒かけて target の強さにする。</summary>
        public void FadeGrunge(float target, float seconds)
        {
            m_grungeFadeTarget = Mathf.Clamp01(target);
            if (seconds <= 0f)
            {
                GrungeWeight = m_grungeFadeTarget;
                return;
            }

            m_grungeFadeSpeed = Mathf.Abs(m_grungeFadeTarget - m_grunge.weight) / seconds;
        }

        private void Reset()
        {
            FindShader();
        }

        private void OnValidate()
        {
            FindShader();
        }

        private void FindShader()
        {
            // ビルドに含めるため、シェーダーは参照として持つ(Shader.Find はエディターでの初期設定だけ)
            if (m_shader == null)
            {
                m_shader = Shader.Find("Hatake/ScreenEdgeFx");
            }
        }

        private void OnEnable()
        {
            m_pass = new ScreenEdgeFxPass();
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            DestroyObject(m_material);
            DestroyObject(m_generatedNoise);
            DestroyObject(m_mesh);
            m_material = null;
            m_generatedNoise = null;
            m_mesh = null;
            m_meshHole = new Vector2(-1f, -1f);
            m_pass = null;
        }

        private static void DestroyObject(UnityEngine.Object obj)
        {
            if (obj == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(obj);
            }
            else
            {
                DestroyImmediate(obj);
            }
        }

        private void Update()
        {
            // フェードの進行
            if (!Application.isPlaying)
            {
                return;
            }

            float dt = m_useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (m_nebulaFadeSpeed > 0f)
            {
                m_nebula.weight = Mathf.MoveTowards(m_nebula.weight, m_nebulaFadeTarget, m_nebulaFadeSpeed * dt);
                if (Mathf.Approximately(m_nebula.weight, m_nebulaFadeTarget))
                {
                    m_nebulaFadeSpeed = 0f;
                }
            }

            if (m_grungeFadeSpeed > 0f)
            {
                m_grunge.weight = Mathf.MoveTowards(m_grunge.weight, m_grungeFadeTarget, m_grungeFadeSpeed * dt);
                if (Mathf.Approximately(m_grunge.weight, m_grungeFadeTarget))
                {
                    m_grungeFadeSpeed = 0f;
                }
            }
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            bool drawNebula = m_nebula.weight > 0.001f;
            bool drawGrunge = m_grunge.weight > 0.001f;

            // 強さが 0 なら何も差し込まない
            if ((!drawNebula && !drawGrunge) || !IsTargetCamera(cam))
            {
                return;
            }

            if (!EnsureResources())
            {
                return;
            }

            UpdateMaterial(cam, drawNebula, drawGrunge);
            UpdateMesh(drawNebula, drawGrunge);

            m_pass.renderPassEvent = m_passEvent;
            m_pass.Setup(m_mesh, m_material, drawNebula, drawGrunge);
            cam.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(m_pass);
        }

        private bool IsTargetCamera(Camera cam)
        {
            // 指定があればそのカメラだけ
            if (m_targetCamera != null)
            {
                return cam == m_targetCamera;
            }

            if (cam.cameraType == CameraType.SceneView)
            {
                return m_showInSceneView;
            }

            if (cam.cameraType != CameraType.Game)
            {
                return false;
            }

            // テクスチャへ撮影するカメラ(本のページ撮影など)には掛けない
            if (cam.targetTexture != null)
            {
                return false;
            }

            // オーバーレイカメラは、指定されたときだけ
            if (!cam.TryGetComponent(out UniversalAdditionalCameraData data))
            {
                return true;
            }

            return data.renderType == CameraRenderType.Base;
        }

        private bool EnsureResources()
        {
            if (m_shader == null)
            {
                return false;
            }

            if (m_material == null || m_material.shader != m_shader)
            {
                DestroyObject(m_material);
                m_material = new Material(m_shader)
                {
                    name = "ScreenEdgeFx (Runtime)",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            if (m_noiseTexture == null && m_generatedNoise == null)
            {
                m_generatedNoise = ScreenEdgeFxNoise.Create(k_noiseSize);
            }

            if (m_mesh == null)
            {
                m_mesh = CreateMesh();
                m_meshHole = new Vector2(-1f, -1f);
            }

            return true;
        }

        private void UpdateMaterial(Camera cam, bool drawNebula, bool drawGrunge)
        {
            float time = Application.isPlaying ? (m_useUnscaledTime ? Time.unscaledTime : Time.time) : 0f;
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;

            m_material.SetTexture(s_noiseTexId, m_noiseTexture != null ? m_noiseTexture : m_generatedNoise);
            m_material.SetVector(s_aspectId, new Vector4(cam.aspect, 0f, 0f, 0f));

            bool lite = m_liteOnMobile && Application.isMobilePlatform;
            if (lite != m_material.IsKeywordEnabled(k_liteKeyword))
            {
                if (lite)
                {
                    m_material.EnableKeyword(k_liteKeyword);
                }
                else
                {
                    m_material.DisableKeyword(k_liteKeyword);
                }
            }

            if (drawNebula)
            {
                NebulaSettings s = m_nebula;
                m_material.SetVector(s_nebBoxId, BoxVector(s.boxSize));
                m_material.SetVector(s_nebMaskId, new Vector4(s.start, InvRange(s.start, s.end), Mathf.Max(s.noiseAmount, 0f), s.weight));
                m_material.SetVector(s_nebSt0Id, ScaleOffset(s.noiseScale, s.noiseScale, s.scroll.x * time, s.scroll.y * time));
                m_material.SetVector(s_nebSt1Id, ScaleOffset(s.noiseScale * 1.7f, s.noiseScale * 1.7f, 0.37f - s.scroll.x * 1.3f * time, 0.11f + s.scroll.y * 1.6f * time));
                m_material.SetVector(s_nebSt2Id, ScaleOffset(s.bandScale.x, s.bandScale.y, s.bandScroll * time, 0.23f));
                m_material.SetVector(s_nebSt3Id, ScaleOffset(s.starScale, s.starScale, s.starScroll.x * time, s.starScroll.y * time));
                m_material.SetVector(s_nebSt4Id, ScaleOffset(s.starScale * 0.62f, s.starScale * 0.62f, 0.53f - s.starScroll.x * 0.6f * time, 0.29f + s.starScroll.y * 0.5f * time));
                m_material.SetColor(s_nebColorAId, ToShaderColor(s.deepColor, linear));
                m_material.SetColor(s_nebColorBId, ToShaderColor(s.glowColor, linear));
                m_material.SetColor(s_nebBandColorId, ToShaderColor(s.bandColor, linear));
                m_material.SetColor(s_nebStarColorId, ToShaderColor(s.starColor, linear) * s.starIntensity);
                m_material.SetVector(s_nebBandId, new Vector4(s.bandStart, 1f / Mathf.Max(s.bandFeather, 0.001f), s.bandThreshold, s.bandSharpness));
                m_material.SetVector(s_nebStarId, new Vector4(s.starStart, 1f / Mathf.Max(s.starFeather, 0.001f), 0.35f, 1.3f));
            }

            if (drawGrunge)
            {
                GrungeSettings s = m_grunge;

                // コマ送り: コマごとに決まった乱数でノイズをずらす
                Vector2 jump = Vector2.zero;
                if (s.stepsPerSecond > 0f && s.jitter > 0f)
                {
                    int step = Mathf.FloorToInt(time * s.stepsPerSecond);
                    jump = new Vector2(ScreenEdgeFxNoise.Hash01(step, 17, 3), ScreenEdgeFxNoise.Hash01(step, 31, 7)) * s.jitter;
                }

                m_material.SetVector(s_gruBoxId, BoxVector(s.boxSize));
                m_material.SetVector(s_gruMaskId, new Vector4(s.start, InvRange(s.start, s.end), Mathf.Max(s.noiseAmount, 0f), s.weight));
                m_material.SetVector(s_gruSt0Id, ScaleOffset(s.blotchScale, s.blotchScale, jump.x, jump.y));
                m_material.SetVector(s_gruSt1Id, ScaleOffset(s.grainScale, s.grainScale, 0.41f + jump.y * 3f, 0.67f + jump.x * 3f));
                m_material.SetColor(s_gruColorId, ToShaderColor(s.color, linear));
                m_material.SetVector(s_gruParamsId, new Vector4(s.grain, 0f, 0f, 0f));
            }
        }

        private static Vector4 BoxVector(Vector2 box)
        {
            float bx = Mathf.Clamp(box.x, 0f, 0.98f);
            float by = Mathf.Clamp(box.y, 0f, 0.98f);
            return new Vector4(bx, by, 1f / (1f - bx), 1f / (1f - by));
        }

        private static float InvRange(float start, float end)
        {
            return 1f / Mathf.Max(end - start, 0.001f);
        }

        private static Vector4 ScaleOffset(float scaleX, float scaleY, float offsetX, float offsetY)
        {
            // ずらしは 0〜1 に折り返して、長時間動かしても精度が落ちないようにする
            return new Vector4(scaleX, scaleY, Mathf.Repeat(offsetX, 1f), Mathf.Repeat(offsetY, 1f));
        }

        private static Color ToShaderColor(Color c, bool linear)
        {
            return linear ? c.linear : c;
        }

        // ------------------------------------------------------------------
        // メッシュ: 4x4 頂点の格子から中央の 1 マスを抜いた枠。
        // 中央の「確実に何も描かない範囲」を穴にして、その画素の計算を省く。
        // ------------------------------------------------------------------

        private static Mesh CreateMesh()
        {
            Mesh mesh = new Mesh
            {
                name = "ScreenEdgeFx Frame",
                hideFlags = HideFlags.HideAndDontSave
            };
            mesh.MarkDynamic();
            mesh.vertices = new Vector3[16];

            int[] indices = new int[8 * 6];
            int n = 0;
            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    // 中央のマスは描かない
                    if (x == 1 && y == 1)
                    {
                        continue;
                    }

                    int i0 = y * 4 + x;
                    indices[n++] = i0;
                    indices[n++] = i0 + 4;
                    indices[n++] = i0 + 1;
                    indices[n++] = i0 + 1;
                    indices[n++] = i0 + 4;
                    indices[n++] = i0 + 5;
                }
            }

            mesh.SetIndices(indices, MeshTopology.Triangles, 0, false);
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(4f, 4f, 4f));
            return mesh;
        }

        private void UpdateMesh(bool drawNebula, bool drawGrunge)
        {
            Vector2 hole = new Vector2(1f, 1f);
            if (drawNebula)
            {
                NebulaSettings s = m_nebula;
                float e0 = Mathf.Min(s.start - 0.5f * Mathf.Max(s.noiseAmount, 0f), s.starStart);
                Vector2 h = HoleHalf(s.boxSize, e0);
                h.y = Mathf.Min(h.y, s.bandStart);
                hole = Vector2.Min(hole, h);
            }

            if (drawGrunge)
            {
                GrungeSettings s = m_grunge;
                hole = Vector2.Min(hole, HoleHalf(s.boxSize, s.start - 0.5f * Mathf.Max(s.noiseAmount, 0f)));
            }

            // 少し内側に取って、境目の欠けを防ぐ
            hole *= 0.97f;
            if (hole.x < 0.02f || hole.y < 0.02f)
            {
                hole = Vector2.zero;
            }

            if (hole == m_meshHole)
            {
                return;
            }

            m_meshHole = hole;
            float[] xs = { -1f, -hole.x, hole.x, 1f };
            float[] ys = { -1f, -hole.y, hole.y, 1f };
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    m_vertices[y * 4 + x] = new Vector3(xs[x], ys[y], 0f);
                }
            }

            m_mesh.vertices = m_vertices;
        }

        // 「縁からの距離が e0 以下」の範囲に内接する長方形の半分の大きさ。
        // e0 <= 0 のときは中央の箱の中でも描く可能性があるので、穴は開けない。
        private static Vector2 HoleHalf(Vector2 box, float e0)
        {
            if (e0 <= 0f)
            {
                return Vector2.zero;
            }

            float bx = Mathf.Clamp(box.x, 0f, 0.98f);
            float by = Mathf.Clamp(box.y, 0f, 0.98f);
            float r = Mathf.Min(e0, 1f) * 0.7071f;
            return new Vector2(bx + (1f - bx) * r, by + (1f - by) * r);
        }
    }
}
