using UnityEngine;

public class AimScript : MonoBehaviour
{
    [Header("参照")]

    [SerializeField] private PhoneAttitudeController m_phoneAttitudeController;

    [SerializeField] private RectTransform m_reticle;

    [Header("縦横エイムのレンジ")]

    [SerializeField] private float m_horizontalAngleRange = 30.0f;

    [SerializeField] private float m_verticalAngleRange = 20.0f;


    // Update is called once per frame
    void Update()
    {
        Vector2 normalizedAngle = m_phoneAttitudeController.GetNormalizedAngles();
        UpdateReticlePosition(-normalizedAngle.x, normalizedAngle.y);
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
}
