using UnityEngine;

public class BoneHitReaction : MonoBehaviour
{
    [SerializeField] private float m_frequency = 25f;
    [SerializeField] private float m_damping = 8f;
    [SerializeField] private float m_intensity = 50f;

    private Vector3 m_angle;
    private Vector3 m_velocity;

    public void HitProcess(HitData data) 
    {
        

        AddHit(Vector3.right, m_intensity);
    }

    private void AddHit(Vector3 direction, float power) 
    {
        Vector3 localDirection = transform.parent.InverseTransformDirection(direction);

        Vector3 impulse = Vector3.Cross(Vector3.forward, localDirection.normalized);

        m_velocity += impulse * power;
    }
    
    private void LateUpdate()
    {
        m_velocity += -m_angle * (m_frequency * m_frequency) * Time.deltaTime;

        m_velocity *= Mathf.Exp(-m_damping * Time.deltaTime);

        m_angle += m_velocity * Time.deltaTime;

        Quaternion animationRotation = transform.localRotation;

        Quaternion hitRotation = Quaternion.Euler(m_angle);

        transform.localRotation = animationRotation * hitRotation;
    }


}
