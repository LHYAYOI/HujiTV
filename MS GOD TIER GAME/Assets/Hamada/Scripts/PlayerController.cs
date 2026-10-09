using UnityEngine;

public class PlayerController : MonoBehaviour
{

    [SerializeField] private MagicBulletManager m_bulletManager;
    [SerializeField] private AimScript m_aimScript;

    [SerializeField] private Transform m_bulletSpawnPoint;

    [SerializeField] private MagicData magicData;
    [SerializeField] private GameObject m_target;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        m_aimScript.UpdateAimScript();

        if (Input.GetKeyDown(KeyCode.P)) 
        {
            FireBullet(magicData);
        }
    }

    //íeÇÃî≠éÀ
    public void FireBullet(MagicData magicData)
    {
        //åÇÇƒÇÈÇ©Ç«Ç§Ç©ÇÃèåèï™Ç‡ó~ÇµÇ¢Ç©Ç‡ÅH

        //GameObject target = m_aimScript.GetLockedOnTarget();

        //if (target == null)
        //{
        //    return;
        //}

        m_bulletManager.FireBullet(magicData, m_bulletSpawnPoint.position, m_target);
    }
}
