using UnityEngine;

public class PlayerController : MonoBehaviour
{

    [SerializeField] private MagicBulletManager m_bulletManager;
    [SerializeField] private AimScript m_aimScript;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        m_aimScript.UpdateAimScript();
    }
}
