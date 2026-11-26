using UnityEngine;

public class BodyPartMover : MonoBehaviour
{
    [SerializeField] Transform m_parent;
    [SerializeField] Transform m_anchor;
    [SerializeField] float m_amplitudeX = 0.5f;
    [SerializeField] float m_amplitudeY = 0.5f;
    [SerializeField] float m_frequency = 0.5f;

    Vector3 m_startAnchorPosition;

    float m_seed;

    //目も
    //ノイズのレートとsinのレートをトランジションさせる

    private void Awake()
    {
        m_startAnchorPosition = m_anchor.transform.localPosition;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
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

        transform.localPosition = m_anchor.localPosition + offset;
    }

    public void TransitionRate(float fromRate, float ToRate, float transitionRate) 
    {
        fromRate = Mathf.Clamp01(fromRate - transitionRate);
        ToRate = Mathf.Clamp01(ToRate + transitionRate);
    }

    public void SetAnchorPosition(Vector3 position) 
    {
        m_anchor.localPosition = position;
    }

    public Vector3 GetAnchorPosition() 
    {
        return m_anchor.localPosition;
    }

    public Vector3 GetAnchorStartPosition() 
    {
        return m_startAnchorPosition;
    }
}
