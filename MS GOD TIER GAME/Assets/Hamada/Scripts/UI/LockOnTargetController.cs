using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class LockOnTargetController : MonoBehaviour
{
    [SerializeField] Image m_targetImage;

    [SerializeField] private StateMachine m_stateMachine;

    [SerializeField] GameObject m_target;

    [SerializeField] float m_defaultRotateSpeed = 45f;
    [SerializeField] Vector2 m_defaultScale;
    [SerializeField] Color m_defaultColor;

    float m_rotateSpeed;

    private void Update()
    {
        //ターゲットを追従
        UpdateTargetImagePosition();
        UpdateTargetImageRotation();

        if (Input.GetKeyDown(KeyCode.K)) 
        {
            AppearTargetUI();
        }

        if (Input.GetKeyDown(KeyCode.J))
        {
            CancelTargetUI();
        }

        if (Input.GetKeyDown(KeyCode.H))
        {
            SelectedTargetUI();
        }
    }

    public void ChangeState(string stateName)
    {
        if (m_stateMachine == null)
        {
            return;
        }

        m_stateMachine.ChangeState(stateName);
    }

    public void SetTarget(GameObject target)
    {
        m_target = target;
    }

    private void UpdateTargetImagePosition()
    {
        if (m_target == null || m_targetImage == null)
        {
            return;
        }

        Vector3 screenPos = Camera.main.WorldToScreenPoint(m_target.transform.position);
        m_targetImage.transform.position = screenPos;
    }

    private void UpdateTargetImageRotation()
    {
        if (m_targetImage == null)
        {
            return;
        }

        m_targetImage.transform.Rotate(0, 0, m_rotateSpeed * Time.deltaTime);
    }

    public void AppearTargetUI() 
    {
        StartCoroutine(AppearRoutine(0.3f));
    }

    public void CancelTargetUI() 
    {
        StartCoroutine(CancellationRoutine(0.3f));
    }

    public void SelectedTargetUI() 
    {
        StartCoroutine(SelectedRoutine(0.3f));
    }

    private IEnumerator AppearRoutine(float duration) 
    {
        float elapsedTime = 0;

        float startSpeed = -3000;

        Vector2 startScale = new Vector2(2.0f, 2.0f);

        Color startColor = new Color(1, 1, 1, 0);

        while (elapsedTime < duration) 
        {
            elapsedTime += Time.deltaTime;

            float rate = Mathf.Clamp01(elapsedTime / duration);

            rate = Easing.EaseOutQuad(rate);

            m_rotateSpeed = Mathf.Lerp(startSpeed, m_defaultRotateSpeed, rate);
            m_targetImage.color = Color.Lerp(startColor, m_defaultColor, rate);
            m_targetImage.transform.localScale = Vector2.Lerp(startScale, m_defaultScale, rate);

            yield return null;
        }
    }

    private IEnumerator CancellationRoutine(float duration)
    {
        float elapsedTime = 0;

        Color endColor = new Color(1, 1, 1, 0);

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;

            float rate = Mathf.Clamp01(elapsedTime / duration);

            rate = Easing.EaseOutQuad(rate);

            m_targetImage.color = Color.Lerp(m_defaultColor, endColor, rate);

            yield return null;
        }
    }

    private IEnumerator SelectedRoutine(float duration)
    {
        float elapsedTime = 0;

        Vector2 endScale = new Vector2(2.0f, 2.0f);

        Color endColor = new Color(1, 1, 1, 0);

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;

            float rate = Mathf.Clamp01(elapsedTime / duration);

            rate = Easing.EaseOutQuad(rate);

            m_targetImage.color = Color.Lerp(m_defaultColor, endColor, rate);
            m_targetImage.transform.localScale = Vector2.Lerp(m_defaultScale, endScale, rate);

            yield return null;
        }
    }

}
