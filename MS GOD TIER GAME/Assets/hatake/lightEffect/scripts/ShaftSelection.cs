using System.Collections.Generic;
using UnityEngine;

namespace LightShaftLab
{
    // スロット数は固定。外れる候補はウェイトを0までフェードさせてからスロットを再利用する。
    // セレクターはカメラごとに持つため、Scene ビューが Game ビューの履歴を変えることはない。
    public sealed class ShaftSelection<T> where T : Component
    {
        public struct Candidate
        {
            public T m_source;
            public float m_score;
            public Candidate(T source, float score) { this.m_source = source; this.m_score = score; }
        }
        public sealed class Slot { public T m_source; public float m_weight; }
        public readonly Slot[] m_slots;
        readonly List<Candidate> m_ranked = new List<Candidate>();
        readonly HashSet<T> m_wanted = new HashSet<T>();
        readonly HashSet<T> m_assigned = new HashSet<T>();
        public ShaftSelection(int capacity)
        {
            m_slots = new Slot[capacity];
            for (int i = 0; i < capacity; i++) m_slots[i] = new Slot();
        }
        // 候補をスコア順に並べて上位をスロットへ割り当て、ウェイトをフェードさせる（割り当て済みの候補はヒステリシス分優遇）。
        public void Update(List<Candidate> candidates, float delta, float fadeSeconds, float hysteresis)
        {
            m_ranked.Clear(); m_assigned.Clear(); m_wanted.Clear();
            foreach (var slot in m_slots) if (slot.m_source) m_assigned.Add(slot.m_source);
            foreach (var candidate in candidates)
                if (candidate.m_source && candidate.m_score > 0)
                    m_ranked.Add(new Candidate(candidate.m_source, candidate.m_score * (m_assigned.Contains(candidate.m_source) ? 1 + hysteresis : 1)));
            m_ranked.Sort(Compare);
            for (int i = 0; i < Mathf.Min(m_slots.Length, m_ranked.Count); i++) m_wanted.Add(m_ranked[i].m_source);
            float step = fadeSeconds <= 0 ? 1 : Mathf.Max(0, delta) / fadeSeconds;
            foreach (var slot in m_slots)
            {
                if (!slot.m_source) { slot.m_source = null; slot.m_weight = 0; }
                else if (!m_wanted.Contains(slot.m_source))
                {
                    slot.m_weight = Mathf.MoveTowards(slot.m_weight, 0, step);
                    if (slot.m_weight <= 0) { m_assigned.Remove(slot.m_source); slot.m_source = null; }
                }
            }
            foreach (var slot in m_slots)
            {
                if (!slot.m_source)
                    foreach (var item in m_ranked)
                        if (m_wanted.Contains(item.m_source) && m_assigned.Add(item.m_source))
                        { slot.m_source = item.m_source; break; }
                if (slot.m_source && m_wanted.Contains(slot.m_source)) slot.m_weight = Mathf.MoveTowards(slot.m_weight, 1, step);
            }
        }
        // スコアの降順。同点ならインスタンスIDで順序を固定する。
        static int Compare(Candidate first, Candidate second)
        {
            int order = second.m_score.CompareTo(first.m_score);
            return order != 0 ? order : first.m_source.GetInstanceID().CompareTo(second.m_source.GetInstanceID());
        }
        // 画面上の占有率・距離・寄与・優先度から候補のスコアを計算する。
        public static float Score(Camera camera, Bounds bounds, float contribution, float priority)
        {
            float distance = Mathf.Sqrt(bounds.SqrDistance(camera.transform.position));
            float radius = bounds.extents.magnitude;
            float depth = Mathf.Max(camera.nearClipPlane, Vector3.Dot(bounds.center - camera.transform.position, camera.transform.forward));
            float height = camera.orthographic ? camera.orthographicSize : depth * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            float coverage = Mathf.Clamp01(radius * radius / Mathf.Max(0.01f, height * height));
            return Mathf.Max(0, priority) * Mathf.Max(0, contribution) * (0.05f + coverage) * (bounds.Contains(camera.transform.position) ? 2 : 1) / (1 + distance * 0.05f);
        }
    }
}
