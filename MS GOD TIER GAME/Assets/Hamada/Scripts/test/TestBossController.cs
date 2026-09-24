using UnityEngine;

public class TestBossController : MonoBehaviour
{
    [SerializeField] GameObject m_hitCube;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.H)) 
        {
            Instantiate(m_hitCube, transform.position + new Vector3(0, 3, 0), Quaternion.identity);
        }
    }

    public void SayHello() 
    {
        Debug.Log("Hello from TestBossController!");
    }

    public void SayHitObjectInformation(HitData hitData) 
    {
        Debug.Log("Damage:" + hitData.GetDamage);
        Debug.Log("MagicType:" + hitData.GetMagicType);
        Debug.Log("hitObjectTag:" + hitData.GetTag);
    }
}
