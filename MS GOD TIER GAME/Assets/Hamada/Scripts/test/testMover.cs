using UnityEngine;

public class testMover : MonoBehaviour
{
    [SerializeField] float m_amplitude = 2.0f; // ˆÚ“®•
    [SerializeField] float m_frequency = 1.0f; // ˆÚ“®‘¬“x

    Vector3 m_startPosition;
    float m_time;

    void Start()
    {
        m_startPosition = transform.position;
    }

    void Update()
    {
        m_time += Time.deltaTime;

        float x = Mathf.Sin(m_time * m_frequency) * m_amplitude;

        transform.position = m_startPosition + Vector3.right * x;
    }
}
