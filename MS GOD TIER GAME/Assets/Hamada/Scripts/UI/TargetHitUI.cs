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

    private void Start()
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

        m_image.transform.position = m_target.transform.position;
        transform.LookAt(Camera.main.transform);
    }

    public void SetTarget(GameObject gameObject)
    {
        m_target = gameObject;
    }

    public void SetSprite(Sprite sprite) 
    {
        m_image.sprite = sprite;
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

        Color endColor = new Color(m_defaultColor.r, m_defaultColor.g, m_defaultColor.b, 0);
        Vector3 endSize = m_defaultSize * 1.5f;

        while (elapsedTime < duration)
        {
            float rate = Easing.EaseOutCubic(elapsedTime / duration);

            elapsedTime += Time.deltaTime;

            m_image.color = Color.Lerp(startColor, endColor, rate);
            m_image.transform.localScale = Vector3.Lerp(startSize, endSize, rate);

            yield return null;
        }

        gameObject.SetActive(false);
    }
}
