using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;


public class MagicBulletManager : MonoBehaviour
{
    [SerializeField] List<MagicBulletController> m_magicBulletPrefabList;

    [SerializeField] private Transform m_bulletSpawnPoint;

    //弾の発射
    public void FireBullet(MagicData magicData, MAGIC_CAST_DIRECTION direction, GameObject target)
    {
        foreach (MagicBulletController prefab in m_magicBulletPrefabList) 
        {
            if (prefab == null) 
            {
                continue;
            }

            if (prefab.GetMagicData().DisplayName == magicData.DisplayName) 
            {
                MagicBulletController bullet = Instantiate(prefab, m_bulletSpawnPoint.position, Quaternion.identity);
                bullet.FireBullet(target, direction);
                break;
            }
        }
    }
}
