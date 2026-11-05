using System.Collections.Generic;
using UnityEngine;

namespace hatake
{
    /// <summary>
    /// 燃焼・復活の燃えかすと粒を、1枚ずつ四角形として作る（モバイル用）。
    /// シェーダーの BookFxAsh / BookFxParticles は全画素で 15 区画ずつ調べるのでモバイルの GPU では重すぎる。
    /// 同じ区画・同じ乱数・同じ動きを CPU で求め、見える分だけを四角形にして BookFlipComposite のパス 13・14 で描く。
    /// 座標は本の座標（x：-1〜1、y：0〜ページの縦横比）。四角形の頂点は出力の UV（0〜1）
    /// </summary>
    internal sealed class BookFxQuads
    {
        public struct Settings
        {
            public int mode;            // 1 燃焼, 2 復活
            public float side;          // 1 右ページ, -1 左ページ, 0 両面
            public float progress;
            public Vector4 origin;      // xy 起点, z 最も遠い角までの距離, w 乱数の種
            public float ash;           // 燃えかすの密度
            public float particles;     // 粒の密度
            public float pageAspect;
            public Vector4 bookRect;    // 出力の UV での見開きの範囲
            public float time;
            public Color ember, revive;
        }

        private readonly Mesh m_ashMesh = new Mesh { name = "BookFxAsh" };
        private readonly Mesh m_sparkMesh = new Mesh { name = "BookFxSpark" };
        private readonly List<Vector3> m_positions = new List<Vector3>();
        private readonly List<Vector4> m_local = new List<Vector4>();
        private readonly List<Vector4> m_shape = new List<Vector4>();
        private readonly List<Vector4> m_look = new List<Vector4>();
        private readonly List<Vector4> m_seeds = new List<Vector4>();
        private readonly List<int> m_indices = new List<int>();

        // 演出ごとに変わらない区画の乱数と、生まれる位置での境界の値
        private struct Cell
        {
            public Vector2 id, spawn;
            public float h1, h2, h3, h4, field;
        }
        private readonly List<Cell> m_ashCells = new List<Cell>();
        private readonly List<Cell> m_sparkCells = new List<Cell>();
        private float m_seed = float.NaN;
        private int m_mode;

        public Mesh AshMesh => m_ashMesh;
        public Mesh SparkMesh => m_sparkMesh;

        public void Release()
        {
            Object.Destroy(m_ashMesh);
            Object.Destroy(m_sparkMesh);
        }

        // ---- シェーダーと同じ乱数・ノイズ（BookFlipLook.cginc） ----
        private static float Frac(float x) => x - Mathf.Floor(x);
        private static float Hash(Vector2 p) => Frac((float)System.Math.Sin(p.x * 127.1 + p.y * 311.7) * 43758.5453f);
        private static float Noise(Vector2 p)
        {
            Vector2 i = new Vector2(Mathf.Floor(p.x), Mathf.Floor(p.y));
            Vector2 f = p - i;
            f = new Vector2(f.x * f.x * (3 - 2 * f.x), f.y * f.y * (3 - 2 * f.y));
            float a = Hash(i), b = Hash(i + new Vector2(1, 0)), c = Hash(i + new Vector2(0, 1)), d = Hash(i + Vector2.one);
            return Mathf.Lerp(Mathf.Lerp(a, b, f.x), Mathf.Lerp(c, d, f.x), f.y);
        }
        private static float Fbm(Vector2 p) => Noise(p) * 0.5f + Noise(p * 2.03f) * 0.3f + Noise(p * 4.1f) * 0.2f;

        private static float Field(Vector2 p, in Settings s)
        {
            float d = Vector2.Distance(p, new Vector2(s.origin.x, s.origin.y)) / Mathf.Max(s.origin.z, 1e-3f);
            float n = Fbm(p * 5.5f + Vector2.one * s.origin.w);
            return d * 0.78f + n * 0.32f;
        }

        private static bool OnPage(Vector2 p, in Settings s)
        {
            bool side = s.side > 0.5f ? p.x >= 0f : s.side < -0.5f ? p.x < 0f : true;
            return side && p.x >= -1f && p.x <= 1f && p.y >= 0f && p.y <= s.pageAspect;
        }

        private static float SmoothStep(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3 - 2 * t);
        }

        // 演出の始めに、ページの上にある区画を集める
        private void Prepare(in Settings s)
        {
            if (s.origin.w == m_seed && s.mode == m_mode) return;
            m_seed = s.origin.w;
            m_mode = s.mode;
            m_ashCells.Clear();
            m_sparkCells.Clear();
            float x0 = s.side > 0.5f ? 0f : -1f, x1 = s.side < -0.5f ? 0f : 1f;
            // 燃えかす（燃焼だけ）
            if (s.mode == 1)
            {
                const float cell = 0.08f;
                for (int iy = Mathf.FloorToInt(0f / cell) - 1; iy <= Mathf.CeilToInt(s.pageAspect / cell); iy++)
                for (int ix = Mathf.FloorToInt(x0 / cell) - 1; ix <= Mathf.CeilToInt(x1 / cell); ix++)
                {
                    var id = new Vector2(ix, iy);
                    float h1 = Hash(id * 1.31f + Vector2.one * (s.origin.w + 5.7f));
                    float h2 = Hash(id * 2.11f + Vector2.one * 1.9f);
                    float h3 = Hash(id * 0.73f + Vector2.one * 9.4f);
                    float h4 = Hash(id * 3.07f + Vector2.one * 4.2f);
                    Vector2 spawn = (id + new Vector2(h2, h3)) * cell;
                    if (!OnPage(spawn, s)) continue;
                    m_ashCells.Add(new Cell { id = id, spawn = spawn, h1 = h1, h2 = h2, h3 = h3, h4 = h4, field = Field(spawn, s) });
                }
            }
            // 粒
            {
                const float cell = 0.045f;
                for (int iy = -1; iy <= Mathf.CeilToInt(s.pageAspect / cell); iy++)
                for (int ix = Mathf.FloorToInt(x0 / cell) - 1; ix <= Mathf.CeilToInt(x1 / cell); ix++)
                {
                    var id = new Vector2(ix, iy);
                    float h1 = Hash(id + Vector2.one * s.origin.w);
                    float h2 = Hash(id * 1.7f + Vector2.one * 3.1f);
                    float h3 = Hash(id * 2.3f + Vector2.one * 7.7f);
                    Vector2 spawn = (id + new Vector2(h2, h3)) * cell;
                    if (!OnPage(spawn, s)) continue;
                    m_sparkCells.Add(new Cell { id = id, spawn = spawn, h1 = h1, h2 = h2, h3 = h3, field = Field(spawn, s) });
                }
            }
        }

        private Vector3 ToOutput(Vector2 p, in Settings s)
        {
            float u = s.bookRect.x + (p.x + 1f) * 0.5f * (s.bookRect.z - s.bookRect.x);
            float v = s.bookRect.y + p.y / Mathf.Max(s.pageAspect, 1e-5f) * (s.bookRect.w - s.bookRect.y);
            return new Vector3(u, v, 0f);
        }

        private void AddQuad(Vector2 center, float radiusX, float radiusY, Vector4 shape, Vector4 look, Vector4 seed, in Settings s)
        {
            int start = m_positions.Count;
            for (int k = 0; k < 4; k++)
            {
                float dx = (k == 1 || k == 2) ? radiusX : -radiusX;
                float dy = (k >= 2) ? radiusY : -radiusY;
                m_positions.Add(ToOutput(center + new Vector2(dx, dy), s));
                m_local.Add(new Vector4(dx, dy, 0f, 0f));
                m_shape.Add(shape);
                m_look.Add(look);
                m_seeds.Add(seed);
            }
            m_indices.Add(start); m_indices.Add(start + 1); m_indices.Add(start + 2);
            m_indices.Add(start); m_indices.Add(start + 2); m_indices.Add(start + 3);
        }

        private void Begin()
        {
            m_positions.Clear(); m_local.Clear(); m_shape.Clear(); m_look.Clear(); m_seeds.Clear(); m_indices.Clear();
        }

        private void End(Mesh mesh)
        {
            mesh.Clear();
            mesh.SetVertices(m_positions);
            mesh.SetUVs(0, m_local);
            mesh.SetUVs(1, m_shape);
            mesh.SetUVs(2, m_look);
            mesh.SetUVs(3, m_seeds);
            mesh.SetTriangles(m_indices, 0);
            mesh.bounds = new Bounds(new Vector3(0.5f, 0.5f, 0f), new Vector3(4f, 4f, 1f));
        }

        /// <summary>今のフレームの燃えかす・粒の四角形を作る。何も無ければ false</summary>
        public bool Build(in Settings s, out bool hasAsh, out bool hasSpark)
        {
            Prepare(s);
            float t = s.time;
            float threshold = Mathf.Lerp(-0.12f, 1.15f, Mathf.Clamp01(s.progress));

            // 燃えかす（BookFxAsh）
            Begin();
            if (s.mode == 1 && s.ash > 0f)
            {
                const float rise = 0.32f;
                foreach (Cell c in m_ashCells)
                {
                    if (c.h1 > s.ash) continue;
                    float phase = Frac(t * (0.35f + c.h4 * 0.2f) + c.h1 * 5f);
                    float alive = Mathf.Exp(-Mathf.Abs(threshold - c.field) / 0.12f);
                    float life = SmoothStep(0f, 0.08f, phase) * (1f - SmoothStep(0.55f, 1f, phase));
                    float a = alive * life;
                    if (a < 0.004f) continue;
                    Vector2 pos = c.spawn + new Vector2(Mathf.Sin(phase * 4f + c.h2 * 6.2832f) * 0.03f + phase * 0.05f * (c.h3 - 0.5f), phase * rise);
                    float spin = t * (1.5f + c.h2 * 2.5f) * (c.h3 > 0.5f ? 1f : -1f) + c.h1 * 6.2832f;
                    float flip = Mathf.Max(Mathf.Abs(Mathf.Cos(spin * 0.7f + c.h4 * 3f)), 0.2f);
                    float size = 0.009f + 0.016f * c.h4;
                    float glow = (0.5f + 0.5f * Mathf.Sin(t * 12f + c.h2 * 30f)) * (1f - phase);
                    float r = size * 1.6f;
                    AddQuad(pos, r, r, new Vector4(Mathf.Cos(spin), Mathf.Sin(spin), flip, size), new Vector4(a, glow, 0f, 0f),
                        new Vector4(c.id.x * 3f, c.id.y * 3f, 0f, 0f), s);
                }
            }
            hasAsh = m_indices.Count > 0;
            End(m_ashMesh);

            // 粒（BookFxParticles）
            Begin();
            if (s.particles > 0f)
            {
                bool burn = s.mode == 1;
                float rise = burn ? 0.16f : 0.09f;
                foreach (Cell c in m_sparkCells)
                {
                    if (c.h1 > s.particles) continue;
                    float phase = Frac(t * (burn ? 1.1f : 0.55f) + c.h1 * 7f);
                    float alive = Mathf.Exp(-Mathf.Abs(threshold - c.field) / (burn ? 0.06f : 0.09f));
                    float life = SmoothStep(0f, 0.15f, phase) * (1f - SmoothStep(0.55f, 1f, phase));
                    float twinkle = burn ? 0.6f + 0.4f * Mathf.Sin(t * 30f + c.h2 * 50f) : 0.5f + 0.5f * Mathf.Sin(t * 9f + c.h3 * 40f);
                    float k = alive * life * twinkle;
                    if (k < 0.004f) continue;
                    Vector2 pos = c.spawn + new Vector2(Mathf.Sin((phase + c.h2) * 6.2832f) * 0.012f, phase * rise);
                    float r = (burn ? 0.0035f : 0.0045f) * (0.6f + c.h3);
                    Color color = burn ? Color.Lerp(s.ember, new Color(2f, 1.8f, 1.2f), c.h2 * 0.5f)
                                       : Color.Lerp(s.revive, new Color(2f, 2f, 2f), c.h3 * 0.5f);
                    float extent = burn ? r * 3f : r * 14f;
                    AddQuad(pos, extent, extent, new Vector4(0f, 0f, 1f, r), new Vector4(k, 0f, burn ? 0f : 1f, 0f),
                        new Vector4(color.r, color.g, color.b, 0f), s);
                }
            }
            hasSpark = m_indices.Count > 0;
            End(m_sparkMesh);
            return hasAsh || hasSpark;
        }
    }
}
