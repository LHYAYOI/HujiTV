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

    //’e‚Ì”­ŽË
    public void FireBullet()
    {
        MagicBulletController bullet = Instantiate(m_magicBulletPrefabList[0], m_bulletSpawnPoint.position, Quaternion.identity);
        bullet.SetTarget(m_aimScript.GetLockedOnTarget());
    }
}
