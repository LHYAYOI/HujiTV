using UnityEngine;

public class EnemyBulletManager : MonoBehaviour
{
    [Header("弾のスピード")]
    [SerializeField] private float m_bulletSpeed = 5f;

    private Transform m_targetPosition; // 弾のターゲット位置


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // 弾をターゲットの方向に移動させる
        if (m_targetPosition != null)
        {
            Debug.Log("Moving bullet towards target");
            Vector3 direction = (m_targetPosition.position - transform.position).normalized;
            transform.position += direction * m_bulletSpeed * Time.deltaTime;
        }
    }

    // 弾を撃つメソッド
    public void Shot(Transform target)
    {
        m_targetPosition = target;
    }


    private void OnCollisionEnter(Collision collision)
    {
        Destroy(gameObject);
    }
}
