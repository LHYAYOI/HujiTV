using System.Collections;
using System.Net;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;

public class CameraLookTargetChanger : MonoBehaviour
{
    [SerializeField] CinemachineCamera m_cinemachineCamera;

    [SerializeField] float m_defaultTargetTransitionTime = .5f;

    [SerializeField] GameObject m_anchor;

    float m_targetTransitionTime = 0;

    Vector3 m_anchorDefaultPosition;

    GameObject m_previousLookTarget;

    private void Start()
    {
        if (m_anchor != null) 
        {
            m_anchorDefaultPosition = m_anchor.transform.position;
        }

        m_targetTransitionTime = m_defaultTargetTransitionTime;
    }

    public void SetLookTarget(GameObject target)
    {
        if (m_cinemachineCamera == null)
        {
            return;
        }

        if (m_anchor == null)
        {
            NonAnchorTargetProcess(target);
        }
        else 
        {
            m_anchor.transform.position = (m_previousLookTarget != null) ? m_previousLookTarget.transform.position : m_anchorDefaultPosition;

            m_cinemachineCamera.LookAt = m_anchor.transform;

            StartCoroutine(TargetTransitionRoutine(m_targetTransitionTime, target));
        }
    }

    private void NonAnchorTargetProcess(GameObject target) 
    {
        if (target == null)
        {
            m_cinemachineCamera.LookAt = null;
        }
        else
        {
            m_cinemachineCamera.LookAt = target.transform;
            m_previousLookTarget = target;
        }
    }

    private IEnumerator TargetTransitionRoutine(float duration, GameObject target) 
    {
        if (target == null) 
        {
            yield break;
        }

        float elapsedTime = 0;

        while (elapsedTime < duration) 
        {
            float rate = elapsedTime / duration;

            elapsedTime += Time.deltaTime;

            if (m_previousLookTarget == null)
            {
                m_anchor.transform.position = Vector3.Lerp(m_anchorDefaultPosition, target.transform.position, rate);
            }
            else 
            {
                m_anchor.transform.position = Vector3.Lerp(m_previousLookTarget.transform.position, target.transform.position, rate);
            }

            yield return null;
        }

        m_cinemachineCamera.LookAt = target.transform;

        m_previousLookTarget = target;
    }
}
