using System.Collections;
using System.Net;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;

public class CameraLookTargetChanger : MonoBehaviour
{
    [SerializeField] private CinemachineCamera m_cinemachineCamera;

    [SerializeField] private GameObject m_anchor;

    [SerializeField] private GameObject m_resetLookPoint;

    Vector3 m_anchorDefaultPosition;

    GameObject m_currentLookTarget;

    GameObject m_lookTarget;

    Coroutine m_transitionCoroutine;

    private void Awake()
    {
        if (m_anchor != null)
        {
            m_anchorDefaultPosition = m_anchor.transform.position;
        }
    }

    public void ResetOrientation() 
    {
        m_cinemachineCamera.LookAt = null;

        m_currentLookTarget = null;

        m_cinemachineCamera.transform.rotation = m_resetLookPoint.transform.rotation;
    }

    public void RegisterLookTarget(GameObject target) 
    {
        m_lookTarget = target;
    }

    public void SetLookTarget(float duration = 0.5f)
    {
        if (m_cinemachineCamera == null)
        {
            return;
        }

        if (m_currentLookTarget == m_lookTarget) 
        {
            return;
        }

        //現在のトランジション停止
        if (m_transitionCoroutine != null)
        {
            StopCoroutine(m_transitionCoroutine);
            m_transitionCoroutine = null;
        }

        //アンカーない場合
        if (m_anchor == null)
        {
            m_cinemachineCamera.LookAt = (m_lookTarget != null) ? m_lookTarget.transform : null;
            m_currentLookTarget = m_lookTarget;
            return;
        }

        //ターゲット解除
        if (m_lookTarget == null)
        {
            m_cinemachineCamera.LookAt = null;
            m_currentLookTarget = m_lookTarget;
            return;
        }

        //はじめてアンカーに切り替える場合
        if (m_cinemachineCamera.LookAt != m_anchor.transform) 
        {
            //トランジション前のアンカーの初期位置設定
            {
                Transform cam = m_cinemachineCamera.transform;

                m_anchor.transform.position = cam.position + cam.forward * 10f;

                m_cinemachineCamera.LookAt = m_anchor.transform;
            }
        }

        m_currentLookTarget = m_lookTarget;

        if (duration <= 0) 
        {
            m_anchor.transform.position = m_lookTarget.transform.position;
            return;
        }

        m_transitionCoroutine = StartCoroutine(TargetTransitionRoutine(duration, m_lookTarget));
    }

    private IEnumerator TargetTransitionRoutine(float duration, GameObject target)
    {
        if (target == null)
        {
            yield break;
        }

        Vector3 anchorStartPosition = m_anchor.transform.position;

        float elapsedTime = 0;

        while (elapsedTime < duration)
        {
            //トランジション中にターゲットが消えたときの措置
            if (target == null) 
            {
                m_transitionCoroutine = null;
                yield break;
            }

            elapsedTime += Time.deltaTime;

            float rate = Easing.EaseInOutCubic(elapsedTime / duration);

            rate = Mathf.Clamp01(rate);

            m_anchor.transform.position = Vector3.Lerp(anchorStartPosition, target.transform.position, rate);
            
            yield return null;
        }

        if (target != null)
        {
            m_anchor.transform.position = target.transform.position;
            m_cinemachineCamera.LookAt = target.transform;
        }

        m_transitionCoroutine = null;
    }
}
