using System;
using UnityEngine;

// Runtime state belongs to the owned spread, never to a shared ScriptableObject.
public sealed class PageObstacleState
{
    public const int SLOT_COUNT = 4;
    private readonly ObstacleInstance[] m_slots = new ObstacleInstance[SLOT_COUNT];
    private long m_sequence;
    public ObstacleInstance this[int index] => m_slots[index];
    public bool BlocksActions => m_slots[3] != null;

    public void Apply(ObstacleData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        // Select all destinations before writing: new entries must never replace each other.
        var selected = new bool[SLOT_COUNT];
        for (int n = 0; n < data.SlotCount; n++)
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
            if (selected[i]) m_slots[i] = new ObstacleInstance(data, ++m_sequence);
    }

    public bool Clear(int slot, ObstacleInstance expected)
    {
        if (slot < 0 || slot >= SLOT_COUNT || expected == null || !ReferenceEquals(m_slots[slot], expected)) return false;
        m_slots[slot] = null;
        return true;
    }
}

public sealed class ObstacleInstance
{
    public ObstacleData Data { get; }
    public long Sequence { get; }
    public ObstacleInteraction Interaction { get; }
    public ObstacleInstance(ObstacleData data, long sequence)
    {
        Data = data;
        Sequence = sequence;
        Interaction = data.RemovalType == OBSTACLE_REMOVAL_TYPE.SCRATCH
            ? (ObstacleInteraction)new ScratchObstacleInteraction(data) : new RubObstacleInteraction(data);
    }
}

public abstract class ObstacleInteraction
{
    public abstract float Progress { get; }
    public abstract void Begin(Vector2 point);
    public abstract void Move(Vector2 point);
    public virtual void Cancel() { }
    public virtual float Coverage(int x, int y) => 1f - Progress;
    public bool Complete => Progress >= 1f;
}

public sealed class RubObstacleInteraction : ObstacleInteraction
{
    private readonly ObstacleData m_data;
    private float m_extreme;
    private int m_direction;
    private int m_passes;
    public RubObstacleInteraction(ObstacleData data) { m_data = data; }
    public override float Progress => Mathf.Clamp01((float)m_passes / m_data.RubPasses);
    public override void Begin(Vector2 point) { m_extreme = point.x; }
    public override void Move(Vector2 point)
    {
        float delta = point.x - m_extreme;
        if (m_direction > 0 && delta > 0 || m_direction < 0 && delta < 0) m_extreme = point.x;
        else if (Mathf.Abs(delta) >= m_data.RubDistance)
        {
            m_direction = delta > 0 ? 1 : -1;
            m_extreme = point.x;
            m_passes++;
        }
    }
}

public sealed class ScratchObstacleInteraction : ObstacleInteraction
{
    public const int RESOLUTION = 64;
    private readonly bool[] m_removed = new bool[RESOLUTION * RESOLUTION];
    private readonly ObstacleData m_data;
    private int m_total;
    private int m_cleared;
    private Vector2 m_previous;
    public ScratchObstacleInteraction(ObstacleData data)
    {
        m_data = data;
        for (int y = 0; y < RESOLUTION; y++)
            for (int x = 0; x < RESOLUTION; x++)
            {
                bool empty = false;
                if (data.Texture != null && data.Texture.isReadable)
                    empty = data.Texture.GetPixelBilinear((x + 0.5f) / RESOLUTION, (y + 0.5f) / RESOLUTION).a < 0.05f;
                m_removed[y * RESOLUTION + x] = empty;
                if (!empty) m_total++;
            }
    }
    public override float Progress => m_total == 0 ? 1f : Mathf.Clamp01(m_cleared / (m_total * m_data.ClearRatio));
    public override float Coverage(int x, int y) => m_removed[y * RESOLUTION + x] ? 0f : 1f;
    public override void Begin(Vector2 point) { m_previous = point; Paint(point); }
    public override void Move(Vector2 point)
    {
        // Distance to a segment covers fast movements without gaps or unbounded sampling.
        Vector2 delta = point - m_previous;
        float length = delta.sqrMagnitude;
        for (int y = 0; y < RESOLUTION; y++)
            for (int x = 0; x < RESOLUTION; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) / RESOLUTION, (y + 0.5f) / RESOLUTION);
                float t = length > 0 ? Mathf.Clamp01(Vector2.Dot(p - m_previous, delta) / length) : 0;
                if ((p - m_previous - delta * t).sqrMagnitude <= m_data.BrushRadius * m_data.BrushRadius) Remove(x, y);
            }
        m_previous = point;
    }
    private void Paint(Vector2 point) { m_previous = point; Move(point); }
    private void Remove(int x, int y)
    {
        int at = y * RESOLUTION + x;
        if (m_removed[at]) return;
        m_removed[at] = true;
        m_cleared++;
    }
}
