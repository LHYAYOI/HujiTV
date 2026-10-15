using Unity.Cinemachine;
using UnityEngine;

public class CameraLookTargetChanger : MonoBehaviour
{
    [SerializeField] CinemachineCamera m_cinemachineCamera;

    public void SetLookTarget(GameObject target) 
    {
        if (m_cinemachineCamera == null) 
        {
            return;
        }

        m_cinemachineCamera.LookAt = target != null ? target.transform : null;
    }
}
