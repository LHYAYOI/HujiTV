using UnityEngine;
using UnityEngine.UIElements;

public class minigorst_autoRotation : MonoBehaviour
{
    [SerializeField] private Vector2 m_rotationSpeed;
    private float m_speed;

    private void Awake()
    {
        m_speed = Random.Range(m_rotationSpeed.x, m_rotationSpeed.y);

        transform.rotation = Quaternion.LookRotation(Random.onUnitSphere);
    }

    private void Update()
    {
        transform.Rotate(m_speed, 0.0f, 0.0f);
    }

}
