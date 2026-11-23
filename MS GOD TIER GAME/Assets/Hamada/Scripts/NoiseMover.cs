using UnityEngine;

public class NoiseMover : MonoBehaviour
{
    [SerializeField] Transform m_parent;
    [SerializeField] float m_amplitudeX = 0.5f;
    [SerializeField] float m_amplitudeY = 0.5f;
    [SerializeField] float m_frequency = 0.5f;

    Vector3 m_anchorPosition;
    float m_seed;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        m_anchorPosition = transform.localPosition;
        m_seed = Random.Range(0, 1000);
    }

    // Update is called once per frame
    void Update()
    {
        float time = Time.time * m_frequency;

        // X方向のNoise
        float noiseX = Mathf.PerlinNoise(m_seed, time) * 2f - 1f;

        // Y方向のNoise
        float noiseY = Mathf.PerlinNoise(m_seed + 53.71f, time + 31.29f) * 2f - 1f;

        Vector3 offset = new Vector3(noiseX * m_amplitudeX, noiseY * m_amplitudeY, 0);

        transform.position = m_parent.transform.position + m_anchorPosition + offset;
    }
}
