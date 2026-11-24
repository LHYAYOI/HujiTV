using System.Collections.Generic;
using UnityEngine;

public class BunkiTargetManager : MonoBehaviour
{
    [Header("扉のターゲット")]
    [SerializeField] private List<GameObject> m_doorTargets;

    void Start()
    {
        foreach (GameObject door in m_doorTargets)
        {
            if (door != null)
            {
                door.SetActive(false);
            }
        }
    }

   
    public void SetActiveDoors()
    {
        foreach (GameObject door in m_doorTargets)
        {
            if (door != null)
            {
                door.SetActive(true);
            }
        }
    }

    public void SetInactiveDoors()
    {
        foreach (GameObject door in m_doorTargets)
        {
            if (door != null)
            {
                door.SetActive(false);
            }
        }
    }
}
