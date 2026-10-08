using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;


public class MagicBulletManager : MonoBehaviour
{
    [SerializeField] List<MagicBulletController> m_magicBulletPrefabList;

    [SerializeField] Transform m_bulletSpawnPoint;

    [SerializeField] AimScript m_aimScript;

    // Update is called once per frame
    void Update()
    {
        
    }

    //íeÇÃî≠éÀ
    public void FireBullet(MagicData magicData)
    {
        foreach (MagicBulletController prefabs in m_magicBulletPrefabList) 
        {
            if (prefabs == null) 
            {
                continue;
            }



            //if(prefabs.GetMagicBulletData==m)
        }

        //switch (magicData.MagicType)
        //{
        //    case MAGIC_TYPE.FIRE:
        //        {
        //            //âäÇÃíeÇî≠éÀÇ∑ÇÈèàóù

        //            MagicBulletController bullet = Instantiate(m_magicBulletPrefabList[0], m_bulletSpawnPoint.position, Quaternion.identity);
        //            bullet.SetTarget(m_aimScript.GetLockedOnTarget());

        //            break;
        //        }
        //    case MAGIC_TYPE.ICE:
        //        {
        //            //ïXÇÃíeÇî≠éÀÇ∑ÇÈèàóù

        //            MagicBulletController bullet = Instantiate(m_magicBulletPrefabList[1], m_bulletSpawnPoint.position, Quaternion.identity);
        //            bullet.SetTarget(m_aimScript.GetLockedOnTarget());

        //            break;
        //        }
        //}

        MagicBulletController bullet = Instantiate(m_magicBulletPrefabList[0], m_bulletSpawnPoint.position, Quaternion.identity);
        bullet.SetTarget(m_aimScript.GetLockedOnTarget());
    }
}
