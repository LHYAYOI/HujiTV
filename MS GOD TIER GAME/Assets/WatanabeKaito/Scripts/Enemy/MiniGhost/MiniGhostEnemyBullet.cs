using UnityEngine;

public class MiniGhostEnemyBullet : MonoBehaviour
{

    private Vector3 m_moveDirection;

    // 計算された水平速度と垂直速度、および専用の重力
    private float m_horizontalSpeed;
    private float m_verticalVelocity;
    private float m_gravity;

    private bool m_isInitializedFlag = false;

    [Header("最小回転速度 (度/秒)")]
    [SerializeField] private float m_minRotationSpeed = 300f;

    [Header("最大回転速度 (度/秒)")]
    [SerializeField] private float m_maxRotationSpeed = 1000f;

    private Vector3 m_randomRotationAxis; // ランダムな回転軸
    private float m_randomRotationSpeed; // ランダムな回転速度


    // 引数に「発射角度（angle）」を追加
    public void SetTarget(Vector3 targetPosition, float bulletSpeed, float angle)
    {
        Vector3 currentPos = transform.position;
        Vector3 diff = targetPosition - currentPos;
        Vector3 diffXZ = new Vector3(diff.x, 0, diff.z);

        float distance = diffXZ.magnitude;
        m_moveDirection = diffXZ.normalized;

        // 角度（度数法）をラジアンに変換
        float rad = angle * Mathf.Deg2Rad;

        // スピードと角度から、最初の「水平方向の速さ」と「上方向の速さ」を計算
        m_horizontalSpeed = bulletSpeed * Mathf.Cos(rad);
        m_verticalVelocity = bulletSpeed * Mathf.Sin(rad);

        // 角度が90度（真上）など、水平に進まない設定にされた場合のエラー回避
        if (m_horizontalSpeed <= 0.001f)
        {
            m_horizontalSpeed = bulletSpeed;
            m_verticalVelocity = 0f;
            m_gravity = 0f;
            m_isInitializedFlag = true;
            // 回転を初期化
            InitializeRotation();
            return;
        }

        // ターゲットの真上に到達するまでの時間
        float timeToReach = distance / m_horizontalSpeed;

        // 指定したスピードと角度で、ターゲットの高さに到達するための専用の重力を計算
        float heightDiff = diff.y;
        m_gravity = 2f * (m_verticalVelocity * timeToReach - heightDiff) / (timeToReach * timeToReach);

        m_isInitializedFlag = true;
        Destroy(gameObject, 10.0f);

        // 回転を初期化
        InitializeRotation();
    }

    // 回転の初期化
    private void InitializeRotation()
    {
        m_randomRotationAxis = Random.onUnitSphere; // 単位球面上のランダムな点（軸）を生成
        m_randomRotationSpeed = Random.Range(m_minRotationSpeed, m_maxRotationSpeed); // ランダムな回転速度を決定
    }


    void Update()
    {
        if (!m_isInitializedFlag) return;

        // 逆算した専用の重力を使って落下させる
        m_verticalVelocity -= m_gravity * Time.deltaTime;

        // 水平方向と垂直方向の移動量を合成
        Vector3 moveDelta = (m_moveDirection * m_horizontalSpeed) + (Vector3.up * m_verticalVelocity);

        transform.position += moveDelta * Time.deltaTime;

        // ランダムに回転させる
        transform.Rotate(m_randomRotationAxis, m_randomRotationSpeed * Time.deltaTime, Space.Self);
    }

    private void OnTriggerEnter(Collider other)
    {
        Destroy(gameObject);
    }
}