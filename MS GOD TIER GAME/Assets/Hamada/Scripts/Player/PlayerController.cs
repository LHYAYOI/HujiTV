using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private MagicBulletManager m_bulletManager;
    [SerializeField] private TargetSelector m_targetSelector;

    [SerializeField] private Transform m_bulletSpawnPoint;

    [SerializeField] private MagicData magicData;

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            FireBullet(magicData, 0);
        }

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            FireBullet(magicData, 1);
        }

        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            FireBullet(magicData, 2);
        }
    }

    //’e‚Ì”­ŽË
    public void FireBullet(MagicData magicData, int direction)
    {
        m_targetSelector.SortTargets();
        GameObject target = m_targetSelector.GetSelectedTarget(direction);

        m_bulletManager.FireBullet(magicData, m_bulletSpawnPoint.position, target);
    }
}
