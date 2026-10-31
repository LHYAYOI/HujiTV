using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class RuneSpellEffect : MonoBehaviour
{
    private enum PHASE { HIDDEN, GATHERING, READY, FLYING }
    private sealed class Layer
    {
        public ParticleSystem System;
        public RuneParticleGraphic Graphic;
        public ParticleSystem.Particle[] Particles;
        public void Submit(int count)
        {
            System.SetParticles(Particles, count);
            Graphic.Refresh();
        }
    }

    private PHASE m_phase;
    private RuneSpellSettings m_settings;
    private Func<Rect?> m_sourceRect, m_bookRect;
    private Func<Vector2> m_origin;
    private Vector2[] m_tracePoints;
    private Layer m_trace, m_core, m_orbit, m_trail;
    private Texture2D m_glow;
    private float m_elapsed, m_clock;
    private Vector2 m_drag, m_launchStart, m_launchDirection, m_center;
    private float m_bookHeight;
    private float m_sideAngle;
    public bool IsReady => m_phase == PHASE.READY;
    public bool IsFlying => m_phase == PHASE.FLYING;
    public bool IsVisible => m_phase != PHASE.HIDDEN && gameObject.activeSelf;
    public Vector2 Center => m_center;
    public Color SpellColor { get; private set; }

    public void Play(IReadOnlyList<RuneStrokeData> strokes, Func<Rect?> sourceRect, Func<Rect?> bookRect,
        Func<Vector2> origin, RuneSpellSettings settings, Color color, float sideAngle)
    {
        EnsureLayers();
        m_settings = settings;
        m_sourceRect = sourceRect;
        m_bookRect = bookRect;
        m_origin = origin;
        SpellColor = color;
        m_sideAngle = sideAngle;
        m_tracePoints = SampleStrokes(strokes, Mathf.Clamp(settings.TraceParticleCount, 32, 512));
        m_elapsed = m_clock = 0f;
        m_drag = Vector2.zero;
        m_phase = PHASE.GATHERING;
        gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        Advance(0f);
    }

    public void SetDrag(Vector2 delta, float radius)
    {
        m_drag = IsReady ? Vector2.ClampMagnitude(delta * m_settings.DragFollow, radius * m_settings.DragLimit) : Vector2.zero;
    }

    public void Launch(MAGIC_CAST_DIRECTION direction)
    {
        if (!IsReady || direction == MAGIC_CAST_DIRECTION.NONE) return;
        float angle = ((int)direction - 1) * m_sideAngle * Mathf.Deg2Rad;
        m_launchDirection = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
        m_launchStart = m_center;
        m_elapsed = 0f;
        m_phase = PHASE.FLYING;
    }

    private void LateUpdate() => Advance(Time.unscaledDeltaTime);

    // Explicit progression also permits deterministic Editor validation.
    public void Advance(float deltaTime)
    {
        if (m_phase == PHASE.HIDDEN) return;
        Rect? book = m_bookRect?.Invoke();
        Rect? source = m_phase == PHASE.GATHERING ? m_sourceRect?.Invoke() : book;
        if (!book.HasValue || !source.HasValue || book.Value.height <= 0f) return;
        m_bookHeight = book.Value.height;
        m_elapsed += Mathf.Max(0f, deltaTime);
        m_clock += Mathf.Max(0f, deltaTime);
        Vector2 origin = m_origin != null ? m_origin() : new Vector2(0.5f, 0.25f);
        Vector2 home = book.Value.min + Vector2.Scale(origin, book.Value.size);
        m_center = home + m_drag;
        float gatherTime = Mathf.Max(0.01f, m_settings.FloatDuration) + Mathf.Max(0.01f, m_settings.GatherDuration);
        if (m_phase == PHASE.GATHERING && m_elapsed >= gatherTime) m_phase = PHASE.READY;
        if (m_phase == PHASE.FLYING)
        {
            float t = m_elapsed / Mathf.Max(0.03f, m_settings.FlightDuration);
            if (t >= 1f) { Hide(); return; }
            // Sufficient distance to leave either a portrait or landscape viewport.
            float distance = Mathf.Sqrt(Screen.width * (float)Screen.width + Screen.height * (float)Screen.height) + m_bookHeight;
            m_center = m_launchStart + m_launchDirection * distance * t * t;
        }
        RenderTrace(source.Value, home);
        float formed = m_phase == PHASE.GATHERING
            ? Mathf.SmoothStep(0f, 1f, (m_elapsed - m_settings.FloatDuration) / Mathf.Max(0.01f, m_settings.GatherDuration)) : 1f;
        RenderCore(formed);
        RenderOrbit(formed);
        RenderTrail();
    }

    private void RenderTrace(Rect source, Vector2 home)
    {
        if (m_phase != PHASE.GATHERING) { m_trace.Submit(0); return; }
        float rise = Mathf.Clamp01(m_elapsed / Mathf.Max(0.01f, m_settings.FloatDuration));
        float gather = Mathf.Clamp01((m_elapsed - m_settings.FloatDuration) / Mathf.Max(0.01f, m_settings.GatherDuration));
        for (int i = 0; i < m_tracePoints.Length; i++)
        {
            float seed = Hash(i);
            Vector2 start = source.min + Vector2.Scale(m_tracePoints[i], source.size);
            Vector2 floating = start + new Vector2(Mathf.Sin(i * 2.4f + m_clock * 2f) * 0.018f,
                m_settings.FloatHeight * (0.7f + seed * 0.6f)) * m_bookHeight;
            Vector2 position = Vector2.Lerp(start, floating, Mathf.SmoothStep(0f, 1f, rise));
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(gather * 1.25f - seed * 0.25f));
            Vector2 control = (floating + home) * 0.5f + new Vector2((seed - 0.5f) * 0.2f, 0.16f) * m_bookHeight;
            Vector2 curve = (1f - t) * (1f - t) * floating + 2f * (1f - t) * t * control + t * t * home;
            position = Vector2.Lerp(position, curve, Mathf.Clamp01(gather * 8f));
            SetParticle(m_trace, i, position, m_bookHeight * m_settings.TraceParticleSize * (0.5f + seed),
                Tint(i, 1f - t * 0.8f));
        }
        m_trace.Submit(m_tracePoints.Length);
    }

    private void RenderCore(float formed)
    {
        for (int i = 0; i < 12; i++)
        {
            float angle = i * 2.39996f + m_clock * (i % 2 == 0 ? 0.9f : -0.7f);
            float offset = i == 0 ? 0f : m_bookHeight * m_settings.CoreSize * 0.14f;
            Vector2 point = m_center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * offset;
            float size = m_bookHeight * m_settings.CoreSize * (i == 0 ? 2.4f : 0.7f + Hash(i) * 0.65f);
            Color color = i == 0 ? SpellColor : Color.Lerp(SpellColor, Color.white, 0.3f + Hash(i) * 0.6f);
            color.a = formed * (i == 0 ? 0.3f : 0.78f);
            SetParticle(m_core, i, point, size * (0.94f + Mathf.Sin(m_clock * 3f) * 0.06f), color);
        }
        m_core.Submit(12);
    }

    private void RenderOrbit(float formed)
    {
        int count = Mathf.Clamp(m_settings.OrbitParticleCount, 8, 96);
        for (int i = 0; i < count; i++)
        {
            float seed = Hash(i + 140);
            float angle = i * 2.39996f + m_clock * m_settings.OrbitSpeed * (0.55f + seed);
            float radius = m_bookHeight * m_settings.OrbitRadius * (0.45f + seed * 0.65f);
            Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.75f) * radius;
            offset.y += Mathf.Sin(m_clock * 1.5f + i) * radius * 0.2f;
            float alpha = formed * (0.4f + 0.55f * Mathf.Sin(m_clock * 2f + i) * Mathf.Sin(m_clock * 2f + i));
            SetParticle(m_orbit, i, m_center + offset, m_bookHeight * m_settings.OrbitParticleSize * (0.5f + seed), Tint(i, alpha));
        }
        m_orbit.Submit(count);
    }

    private void RenderTrail()
    {
        if (!IsFlying) { m_trail.Submit(0); return; }
        int count = Mathf.Clamp(m_settings.TrailParticleCount, 8, 96);
        float duration = Mathf.Max(0.03f, m_settings.FlightDuration);
        float distance = Mathf.Sqrt(Screen.width * (float)Screen.width + Screen.height * (float)Screen.height) + m_bookHeight;
        for (int i = 0; i < count; i++)
        {
            float age = i / (float)count;
            float t = Mathf.Max(0f, m_elapsed - age * duration * 0.28f) / duration;
            Vector2 point = m_launchStart + m_launchDirection * distance * t * t;
            Vector2 normal = new Vector2(-m_launchDirection.y, m_launchDirection.x);
            point += normal * (Hash(i) - 0.5f) * m_bookHeight * 0.025f * age;
            SetParticle(m_trail, i, point, m_bookHeight * m_settings.OrbitParticleSize * (1.7f - age), Tint(i, (1f - age) * 0.75f));
        }
        m_trail.Submit(count);
    }

    private Color Tint(int index, float alpha)
    {
        Color color = Color.Lerp(SpellColor, Color.white, Hash(index + 9) * 0.65f);
        color.a = alpha;
        return color;
    }

    private void SetParticle(Layer layer, int index, Vector2 screen, float size, Color tint)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, screen, null, out Vector2 point);
        layer.Particles[index] = new ParticleSystem.Particle
        {
            position = point, startSize = Mathf.Max(0.5f, size), startColor = tint,
            startLifetime = 1000f, remainingLifetime = 1000f, randomSeed = (uint)(index + 1)
        };
    }

    private static float Hash(int index) => Mathf.Repeat(Mathf.Sin(index * 127.1f + 311.7f) * 43758.5453f, 1f);

    private static Vector2[] SampleStrokes(IReadOnlyList<RuneStrokeData> strokes, int count)
    {
        var starts = new List<Vector2>();
        var ends = new List<Vector2>();
        var cumulative = new List<float>();
        float total = 0f;
        foreach (var stroke in strokes)
            for (int i = 1; i < stroke.Points.Count; i++)
            {
                float length = Vector2.Distance(stroke.Points[i - 1], stroke.Points[i]);
                if (length <= 0f) continue;
                starts.Add(stroke.Points[i - 1]); ends.Add(stroke.Points[i]);
                total += length; cumulative.Add(total);
            }
        if (total <= 0f) return Array.Empty<Vector2>();
        var result = new Vector2[count];
        int segment = 0;
        for (int i = 0; i < count; i++)
        {
            float at = total * i / Mathf.Max(1, count - 1);
            while (segment < cumulative.Count - 1 && cumulative[segment] < at) segment++;
            float previous = segment == 0 ? 0f : cumulative[segment - 1];
            result[i] = Vector2.Lerp(starts[segment], ends[segment], (at - previous) / (cumulative[segment] - previous));
        }
        return result;
    }

    private void EnsureLayers()
    {
        if (m_core != null) return;
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30500;
        m_glow = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = "Rune Particle Glow", wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color[32 * 32];
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float r = new Vector2((x - 15.5f) / 15.5f, (y - 15.5f) / 15.5f).magnitude;
                pixels[y * 32 + x] = new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - r), 2f));
            }
        m_glow.SetPixels(pixels); m_glow.Apply(false, true);
        m_trace = CreateLayer("Dissolving Rune", 512);
        m_trail = CreateLayer("Spell Trail", 96);
        m_core = CreateLayer("Spell Core", 12);
        m_orbit = CreateLayer("Orbiting Motes", 96);
    }

    private Layer CreateLayer(string name, int capacity)
    {
        var root = new GameObject(name, typeof(RectTransform));
        root.transform.SetParent(transform, false);
        var rect = (RectTransform)root.transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var particles = root.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.playOnAwake = false; main.maxParticles = capacity;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        var emission = particles.emission; emission.enabled = false;
        root.GetComponent<ParticleSystemRenderer>().enabled = false;
        // Paused systems retain SetParticles data across player-loop simulation frames.
        // A stopped system otherwise clears externally assigned particles on its next update.
        particles.Play();
        particles.Pause();
        var graphic = root.AddComponent<RuneParticleGraphic>();
        graphic.Configure(particles, m_glow, capacity);
        return new Layer { System = particles, Graphic = graphic, Particles = new ParticleSystem.Particle[capacity] };
    }

    public void Hide()
    {
        m_phase = PHASE.HIDDEN;
        m_drag = Vector2.zero;
        if (m_core != null)
            foreach (var layer in new[] { m_trace, m_core, m_orbit, m_trail }) layer.Submit(0);
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (m_glow != null)
        {
            if (Application.isPlaying) Destroy(m_glow); else DestroyImmediate(m_glow);
        }
    }
}
