using System.Collections.Generic;
using UnityEngine;

public class TargetLockOnUIManager : MonoBehaviour
{

    [SerializeField] List<LockOnTargetController> m_lockOnTargetUIList;

    public void ActiveLockOnUI(TargetableObject target) 
    {
        foreach (LockOnTargetController ui in m_lockOnTargetUIList) 
        {
            if (ui.gameObject.activeSelf) 
            {
                continue;
            }

            ui.gameObject.SetActive(true);
            ui.SetTarget(target);
            ui.AppearTargetUI();
            break;
        }
    }

    public void CancelLockOnUI(TargetableObject target) 
    {
    
    }
}
