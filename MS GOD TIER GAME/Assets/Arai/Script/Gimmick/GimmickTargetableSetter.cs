using System.Collections.Generic;
using UnityEngine;

public class GimmickTargetableSetter : MonoBehaviour
{
    [SerializeField] List<TargetableObject> m_targetableObjectList;

    public void SetTargetableObjectFlag(bool flag) 
    {
        foreach (TargetableObject target in m_targetableObjectList) 
        {
            if (target == null) 
            {
                continue;
            }

            target.SetTargetable(flag);
        }
    }

}
