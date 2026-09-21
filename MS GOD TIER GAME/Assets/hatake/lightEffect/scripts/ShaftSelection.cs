using System.Collections.Generic;
using UnityEngine;

namespace LightShaftLab
{
    // Fixed slots: fade the outgoing item to zero before reusing its slot.
    // Each camera owns its selectors; Scene view cannot change Game view history.
    public sealed class ShaftSelection<T> where T : Component
    {
        public struct Candidate
        {
            public T source;
            public float score;
            public Candidate(T source, float score) { this.source = source; this.score = score; }
        }
        public sealed class Slot { public T source; public float weight; }
        public readonly Slot[] slots;
        readonly List<Candidate> m_ranked = new List<Candidate>();
        readonly HashSet<T> m_wanted = new HashSet<T>();
        readonly HashSet<T> m_assigned = new HashSet<T>();
        public ShaftSelection(int capacity)
        {
            slots = new Slot[capacity];
            for (int i = 0; i < capacity; i++) slots[i] = new Slot();
        }
        public void Update(List<Candidate> candidates, float delta, float fadeSeconds, float hysteresis)
        {
            m_ranked.Clear(); m_assigned.Clear(); m_wanted.Clear();
            foreach (var slot in slots) if (slot.source) m_assigned.Add(slot.source);
            foreach (var candidate in candidates)
                if (candidate.source && candidate.score > 0)
                    m_ranked.Add(new Candidate(candidate.source, candidate.score * (m_assigned.Contains(candidate.source) ? 1 + hysteresis : 1)));
            m_ranked.Sort(Compare);
            for (int i = 0; i < Mathf.Min(slots.Length, m_ranked.Count); i++) m_wanted.Add(m_ranked[i].source);
            float step = fadeSeconds <= 0 ? 1 : Mathf.Max(0, delta) / fadeSeconds;
            foreach (var slot in slots)
            {
                if (!slot.source) { slot.source = null; slot.weight = 0; }
                else if (!m_wanted.Contains(slot.source))
                {
                    slot.weight = Mathf.MoveTowards(slot.weight, 0, step);
                    if (slot.weight <= 0) { m_assigned.Remove(slot.source); slot.source = null; }
                }
            }
            foreach (var slot in slots)
            {
                if (!slot.source)
                    foreach (var item in m_ranked)
                        if (m_wanted.Contains(item.source) && m_assigned.Add(item.source))
                        { slot.source = item.source; break; }
                if (slot.source && m_wanted.Contains(slot.source)) slot.weight = Mathf.MoveTowards(slot.weight, 1, step);
            }
        }
        static int Compare(Candidate a, Candidate b)
        {
            int order = b.score.CompareTo(a.score);
            return order != 0 ? order : a.source.GetInstanceID().CompareTo(b.source.GetInstanceID());
        }
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
