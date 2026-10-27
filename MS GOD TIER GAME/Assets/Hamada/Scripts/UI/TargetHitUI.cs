using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class TargetHitUI : MonoBehaviour
{
    [SerializeField] Image m_image;
    [SerializeField] float m_animationDuration;

    GameObject m_target;
    Color m_defaultColor;
    Vector3 m_defaultSize;

    public void Init()
    {
        m_defaultColor = m_image.color;
        m_defaultSize = m_image.transform.localScale;
    }

    private void Update()
    {
        UpdateUI();
    }

    private void UpdateUI() 
    {
        if (m_target == null) 
        {
            return;
        }

        transform.position = m_target.transform.position;
        transform.LookAt(Camera.main.transform);
    }

    public void ShowHitUI(GameObject target) 
    {
        m_target = target;

        m_image.color = m_defaultColor;
        m_image.transform.localScale = m_defaultSize;

        StartCoroutine(ShowHitImageRoutine(m_animationDuration));
    }

    private IEnumerator ShowHitImageRoutine(float duration) 
    {
        float elapsedTime = 0.0f;

        Color startColor = m_defaultColor;
        Vector3 startSize = m_defaultSize;

        Color endColor = new Color(1, 1, 1, 0);
        Vector3 endSize = m_defaultSize * 1.5f;

        while (elapsedTime < duration)
        {
            float rate = elapsedTime / duration;

            float colorRate = Easing.EaseInOutQuart(rate);
            float sizeRate = Easing.EaseOutCubic(rate);

            rate = Mathf.Clamp01(rate);

            elapsedTime += Time.deltaTime;

            m_image.color = Color.Lerp(startColor, endColor, colorRate);
            m_image.transform.localScale = Vector3.Lerp(startSize, endSize, sizeRate);

            yield return null;
        }

        gameObject.SetActive(false);
    }
}
