using UnityEngine;

public class MiniGhostEnemyBullet : MonoBehaviour
{
    private Vector3 m_moveDirection;
    private float m_speed;
    private bool m_isInitializedFlag = false;

    public void SetDirection(Vector3 direction, float bulletSpeed)
    {
        m_moveDirection = direction;
        m_speed = bulletSpeed;
        m_isInitializedFlag = true;

        
        Destroy(gameObject, 15.0f);
    }

    void Update()
    {
        if (!m_isInitializedFlag) return;

        // Žw’è•ûŒü‚ÉˆÚ“®
        transform.position += m_moveDirection * m_speed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        Destroy(gameObject);
    }
}
