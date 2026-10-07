using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

public class AimScript : MonoBehaviour
{
    [Header("参照")]

    [SerializeField] private PhoneAttitudeController m_phoneAttitudeController;

    [SerializeField] private RectTransform m_reticle;

    [SerializeField] private float m_lockOnRange = 10.0f;

    [SerializeField] private LayerMask m_lockOnLayerMask;

    //List<GameObject> m_targets = new List<GameObject>();

    GameObject m_target;

    [Header("縦横エイムのレンジ")]

    [SerializeField] private float m_horizontalAngleRange = 30.0f;

    [SerializeField] private float m_verticalAngleRange = 20.0f;


    // Update is called once per frame
    void Update()
    {
        Vector2 normalizedAngle = m_phoneAttitudeController.GetNormalizedAngles();
        UpdateReticlePosition(-normalizedAngle.x, normalizedAngle.y);
        LockOnTargetProcess();
    }

    private void UpdateReticlePosition(float horizontal, float vertical)
    {
        //画面サイズの基準とする親UI
        RectTransform parent = m_reticle.parent as RectTransform;

        if (parent == null)
        {
            return;
        }

        // 角度 を-1 ～ +1の範囲に正規化する
        float normalizedX = Mathf.Clamp(horizontal / m_horizontalAngleRange, -1.0f, 1.0f);

        float normalizedY = Mathf.Clamp(vertical / m_verticalAngleRange, -1.0f, 1.0f);

        // 画面サイズの基準とする親UIのサイズを取得
        Vector2 size = parent.rect.size;

        Vector2 parentUIHalfSize = new Vector2(size.x * 0.5f, size.y * 0.5f);

        m_reticle.anchoredPosition = new Vector2(normalizedX * parentUIHalfSize.x, normalizedY * parentUIHalfSize.y);
    }

    private void LockOnTargetProcess() 
    {
        Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(null, m_reticle.position);

        Ray ray = Camera.main.ScreenPointToRay(screenPosition);

        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, m_lockOnRange, m_lockOnLayerMask))
        {
            if (m_target != hit.collider.gameObject)
            {
                m_target = hit.collider.gameObject;
                Debug.Log("Lock on target: " + m_target.name);
            }
        }
    }

    //public List<GameObject> GetLockedOnTargets() 
    //{
    //    return m_targets;
    //}

    public GameObject GetLockedOnTarget() 
    {
        return m_target;
    }
}
