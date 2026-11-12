using System;
using UnityEngine;

// Runtime state belongs to the owned spread, never to shared settings.
public sealed class PageObstacleState
{
    public const int SLOT_COUNT = 4;
    private readonly ObstacleInstance[] m_slots = new ObstacleInstance[SLOT_COUNT];
    private long m_sequence;
    public ObstacleInstance this[int index] => m_slots[index];
    public bool BlocksActions => m_slots[3] != null;

    public void Apply(ObstacleData data, int slotCount = 1)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (slotCount < 1 || slotCount > SLOT_COUNT) throw new ArgumentOutOfRangeException(nameof(slotCount));
        var selected = new bool[SLOT_COUNT];
        for (int n = 0; n < slotCount; n++)
        {
            int at = -1;
            for (int i = 0; i < SLOT_COUNT; i++)
                if (!selected[i] && m_slots[i] == null) { at = i; break; }
            if (at < 0)
                for (int i = 0; i < SLOT_COUNT; i++)
                    if (!selected[i] && (at < 0 || m_slots[i].Sequence < m_slots[at].Sequence)) at = i;
            selected[at] = true;
        }
        for (int i = 0; i < SLOT_COUNT; i++)
            if (selected[i]) m_slots[i] = new ObstacleInstance(data, ++m_sequence, i);
    }

    public bool Clear(int slot, ObstacleInstance expected)
    {
        if (slot < 0 || slot >= SLOT_COUNT || expected == null || !ReferenceEquals(m_slots[slot], expected)) return false;
        m_slots[slot] = null;
        return true;
    }
}

// One stain, one independent cleaning progress. No removal-type hierarchy.
public sealed class ObstacleInstance
{
    public ObstacleData Data { get; }
    public long Sequence { get; }
    public int Slot { get; }
    public Texture2D Texture => Data.GetTexture(Slot);
    private float m_distance;
    private Vector2 m_previous;
    private bool m_tracking;
    public float Progress => Mathf.Clamp01(m_distance / Data.RequiredDistance);
    public bool Complete => Progress >= 1f;

    public ObstacleInstance(ObstacleData data, long sequence, int slot = 0)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        Sequence = sequence;
        Slot = slot;
    }

    public void Begin(Vector2 point) { m_previous = point; m_tracking = true; }

    public void Move(Vector2 point)
    {
        if (!m_tracking || !float.IsFinite(point.x) || !float.IsFinite(point.y)) return;
        float distance = Vector2.Distance(m_previous, point);
        // Keep an anchor to avoid counting stationary touch jitter.
        if (distance < Data.MinimumMovement) return;
        m_previous = point;
        m_distance = Mathf.Min(Data.RequiredDistance, m_distance + distance);
    }

    public void Cancel() { m_tracking = false; }
}
