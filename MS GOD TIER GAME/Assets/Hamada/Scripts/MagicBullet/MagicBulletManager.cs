using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;


public class MagicBulletManager : MonoBehaviour
{
    [SerializeField] TargetHitUIManager m_targetHitUIManager;
    [SerializeField] TargetLockOnUIManager m_lockOnUIManager;
    [SerializeField] List<MagicBulletController> m_magicBulletPrefabList;

    [SerializeField] GameObject m_kiranEffect;
    [SerializeField] Transform m_bulletSpawnPoint;

    //弾の発射
    public void FireBullet(MagicData magicData, MAGIC_CAST_DIRECTION direction, GameObject target)
    {
        Instantiate(m_kiranEffect, m_bulletSpawnPoint.transform.position, Quaternion.identity);
        Fire(magicData, direction, target);
    }

    private void Fire(MagicData magicData, MAGIC_CAST_DIRECTION direction, GameObject target) 
    {
        foreach (MagicBulletController prefab in m_magicBulletPrefabList)
        {
            if (prefab == null)
            {
                continue;
            }

            if (prefab.GetMagicData().DisplayName == magicData.DisplayName)
            {
                CameraManager.Instance.ChangeCameraFOV010(1.5f, 65);
                CameraManager.Instance.KickCameraRotation(new Vector3(-5, 0, 0), 0.8f);
                MagicBulletController bullet = Instantiate(prefab, m_bulletSpawnPoint.position, Quaternion.identity);
                bullet.SetHitUIManager(m_targetHitUIManager);
                bullet.FireBullet(target, direction);
                m_lockOnUIManager.PlaySelectedEffect(direction, bullet);
                break;
            }
        }
    }
}
