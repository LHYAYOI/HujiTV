using System;
using UnityEngine;

[Serializable]
public sealed class RuneSpellSettings
{
    [Header("Dissolve / Gather (seconds)")]
    [Min(0.01f)] [SerializeField] private float m_floatDuration = 0.35f;
    public float FloatDuration => m_floatDuration;
    [Min(0.01f)] [SerializeField] private float m_gatherDuration = 0.55f;
    public float GatherDuration => m_gatherDuration;
    [Tooltip("Fraction of book height")]
    [Range(0f, 0.4f)] [SerializeField] private float m_floatHeight = 0.12f;
    public float FloatHeight => m_floatHeight;
    [Range(32, 512)] [SerializeField] private int m_traceParticleCount = 220;
    public int TraceParticleCount => m_traceParticleCount;
    [Range(0.001f, 0.025f)] [SerializeField] private float m_traceParticleSize = 0.009f;
    public float TraceParticleSize => m_traceParticleSize;
    [Header("Spell Core / Orbit")]
    [Range(0.01f, 0.12f)] [SerializeField] private float m_coreSize = 0.045f;
    public float CoreSize => m_coreSize;
    [Range(0.01f, 0.15f)] [SerializeField] private float m_orbitRadius = 0.055f;
    public float OrbitRadius => m_orbitRadius;
    [Range(8, 96)] [SerializeField] private int m_orbitParticleCount = 36;
    public int OrbitParticleCount => m_orbitParticleCount;
    [Min(0f)] [SerializeField] private float m_orbitSpeed = 1.3f;
    public float OrbitSpeed => m_orbitSpeed;
    [Range(0.001f, 0.02f)] [SerializeField] private float m_orbitParticleSize = 0.006f;
    public float OrbitParticleSize => m_orbitParticleSize;
    [SerializeField] private Color m_fallbackColor = new Color(0.35f, 0.8f, 1f);
    public Color FallbackColor => m_fallbackColor;
    [Header("Direct Drag / Launch")]
    [Range(0f, 1f)] [SerializeField] private float m_dragFollow = 0.4f;
    public float DragFollow => m_dragFollow;
    [Range(0.1f, 3f)] [SerializeField] private float m_dragLimit = 0.65f;
    public float DragLimit => m_dragLimit;
    [Min(0.03f)] [SerializeField] private float m_flightDuration = 0.4f;
    public float FlightDuration => m_flightDuration;
    [Range(8, 96)] [SerializeField] private int m_trailParticleCount = 40;
    public int TrailParticleCount => m_trailParticleCount;
    [Header("Image Direction Guides")]
    [SerializeField] private Sprite m_arrowSprite;
    public Sprite ArrowSprite => m_arrowSprite;
    [Range(0.1f, 0.5f)] [SerializeField] private float m_arrowLength = 0.24f;
    public float ArrowLength => m_arrowLength;
    [Range(0f, 1f)] [SerializeField] private float m_arrowIdleAlpha = 0.3f;
    public float ArrowIdleAlpha => m_arrowIdleAlpha;
}
