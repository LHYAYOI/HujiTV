using UnityEngine;

public class ToiletLidController : MonoBehaviour
{
    [Header("ふたの開く角度")]
    [SerializeField] private float m_openAngle = 70.0f;

    private Quaternion m_closedRotation;    // 閉じたときの回転
    private Quaternion m_startRotation;     // 回転開始時の角度
    private Quaternion m_targetRotation;    // 回転終了時の角度

    private void Awake()
    {
        // 初期回転を閉じた状態として保存
        m_closedRotation = transform.localRotation;
    }

    // ふたを開く準備
    public void StartOpening()
    {
        // 現在の角度を開始位置にする
        m_startRotation = transform.localRotation;

        // 開いたときの角度を設定
        m_targetRotation = m_closedRotation * Quaternion.Euler(m_openAngle, 0f, 0f);
    }

    // ふたを閉じる準備
    public void StartClosing()
    {
        // 現在の角度を開始位置にする
        m_startRotation = transform.localRotation;

        // 閉じたときの角度を設定
        m_targetRotation = m_closedRotation;
    }

    // イージングを適用して回転
    public void UpdateRotation(float easeT)
    {
        transform.localRotation = Quaternion.Slerp(
            m_startRotation,
            m_targetRotation,
            easeT
        );
    }
}