using UnityEngine;

using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine.PlayerLoop;

public class ThreadSprayEffect : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform m_mouth;
    [SerializeField] private Material m_threadMaterial;

    [Header("Emission")]
    [SerializeField] private int m_strandCount = 100;
    [SerializeField] private int m_pointCount = 20;
    [SerializeField] private float m_spreadAngle = 40f;
    [SerializeField] private Vector2 m_distanceRange = new Vector2(5f, 10f);

    [Header("Timing")]
    [SerializeField] private float m_emitDuration = 0.25f;
    [SerializeField] private float m_extendDuration = 0.35f;
    [SerializeField] private float m_lifeTime = 1.5f;

    [Header("Shape")]
    [SerializeField] private float m_width = 0.012f;
    [SerializeField] private float m_waveAmplitude = 0.2f;
    [SerializeField] private float m_waveCount = 2.5f;

    [Header("Color")]
    [SerializeField]
    private Color m_threadColor =
        new Color(0.25f, 0.9f, 0.8f, 0.55f);

    [SerializeField] float fadeTime;

    private class ThreadData
    {
        public LineRenderer line;

        public Vector3 start;
        public Vector3 direction;
        public Vector3 axisA;
        public Vector3 axisB;

        public float distance;
        public float startTime;
        public float phaseA;
        public float phaseB;
        public float amplitude;
        public float widthScale;

        public bool fadeFlag;
        public float fadeTime;
    }

    private readonly List<ThreadData> m_threads = new();

    public void Play()
    {
        if (m_mouth == null || m_threadMaterial == null)
            return;

        for (int i = 0; i < m_strandCount; i++)
        {
            CreateThread();
        }
    }

    private void CreateThread()
    {
        // ランダムな噴射方向を作る
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float radius = Mathf.Sqrt(Random.value) *
                       Mathf.Tan(m_spreadAngle * Mathf.Deg2Rad);

        Vector3 localDirection = new Vector3(
            Mathf.Cos(angle) * radius,
            Mathf.Sin(angle) * radius,
            1f
        ).normalized;

        Vector3 direction =
            m_mouth.TransformDirection(localDirection).normalized;

        // 糸のうねりに使う軸
        Vector3 axisA = Vector3.Cross(direction, Vector3.up);

        if (axisA.sqrMagnitude < 0.001f)
            axisA = Vector3.Cross(direction, Vector3.right);

        axisA.Normalize();

        Vector3 axisB =
            Vector3.Cross(direction, axisA).normalized;

        // LineRenderer生成
        GameObject obj = new GameObject("Thread");
        obj.transform.SetParent(transform, true);

        LineRenderer line = obj.AddComponent<LineRenderer>();

        line.useWorldSpace = true;
        line.sharedMaterial = m_threadMaterial;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;

        line.positionCount = m_pointCount;
        line.numCapVertices = 2;
        line.numCornerVertices = 2;

        ThreadData data = new ThreadData();

        data.line = line;
        data.start = m_mouth.position;
        data.direction = direction;
        data.axisA = axisA;
        data.axisB = axisB;

        data.distance = Random.Range(
            m_distanceRange.x,
            m_distanceRange.y
        );

        data.startTime = Time.time +
                         Random.Range(0f, m_emitDuration);

        data.phaseA = Random.Range(0f, Mathf.PI * 2f);
        data.phaseB = Random.Range(0f, Mathf.PI * 2f);

        data.amplitude = Random.Range(0.5f, 1.5f);
        data.widthScale = Random.Range(0.5f, 1.5f);

        line.widthMultiplier = m_width * data.widthScale;

        m_threads.Add(data);
    }

    private void Update()
    {

        if (Input.GetKeyDown(KeyCode.O)) 
        {
            Play();
        }

        float now = Time.time;

        for (int i = m_threads.Count - 1; i >= 0; i--)
        {
            ThreadData data = m_threads[i];

            float elapsed = now - data.startTime;

            if (elapsed < 0f)
                continue;

            if (elapsed >= m_lifeTime)
            {
                Destroy(data.line.gameObject);
                m_threads.RemoveAt(i);
                continue;
            }

            UpdateThread(data, elapsed);
        }
    }

    private void UpdateThread(ThreadData data, float elapsed)
    {
        // 糸が伸びていく進行度
        float progress = Mathf.Clamp01(elapsed / m_extendDuration);

        progress = Easing.EaseOutQuint(progress);

        // 消えるときのフェード
        float fade = Mathf.Clamp01((m_lifeTime - elapsed) / 0.25f);

        float alpha = m_threadColor.a * fade;

        Color color = m_threadColor;
        color.a = alpha;

        data.line.startColor = color;
        data.line.endColor = color;

        for (int i = 0; i < m_pointCount; i++)
        {
            float t = i / (float)(m_pointCount - 1);

            // 基本となる直線
            Vector3 center =
                data.start +
                data.direction * data.distance * t * progress;

            // 糸のうねり
            float waveA = Mathf.Sin(t * m_waveCount * Mathf.PI * 2f + data.phaseA);

            float waveB = Mathf.Sin(t * m_waveCount * 1.37f * Mathf.PI * 2f + data.phaseB);

            Vector3 offset =
                (data.axisA * waveA + data.axisB * waveB)
                * m_waveAmplitude
                * data.amplitude
                * t
                * progress;

            data.line.SetPosition(i, center + offset);
        }
    }
}
