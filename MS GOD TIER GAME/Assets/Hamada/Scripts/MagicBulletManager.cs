using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;


public class MagicBulletManager : MonoBehaviour
{
    [SerializeField] List<MagicBulletController> m_magicBulletPrefabList;

    [SerializeField] AimScript m_aimScript;

    // Update is called once per frame
    void Update()
    {
        
    }

    //’e‚Ì”­ŽË
    public void FireBullet(MagicData magicData, Vector3 m_bulletSpawnPoint, GameObject target)
    {
        foreach (MagicBulletController prefab in m_magicBulletPrefabList) 
        {
            if (prefab == null) 
            {
                continue;
            }

            if (prefab.GetMagicData().DisplayName == magicData.DisplayName) 
            {
                MagicBulletController bullet = Instantiate(prefab, m_bulletSpawnPoint, Quaternion.identity);
                bullet.SetTarget(target);
                break;
            }
        }
    }
}
