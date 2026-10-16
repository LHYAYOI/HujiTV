using UnityEngine;
using UnityEngine.Splines;

[CreateAssetMenu(fileName = "NewMiniGhostSpawnState", menuName = "States/MiniGhostStates/MiniGhostSpawnState")]
public class MiniGhostSpawnState : StateBase
{
    [Header("スプラインまでの移動時間")]
    [SerializeField] private float m_spawnDuration = 3.0f;

    MiniGhostEnemy m_miniGhostEnemy;

    private bool m_isSpawningFlag = false; // 現在出現中かどうかのフラグ
    private Vector3 m_spawnTargetPos;  // 出現目標地点（スプラインの始点）
    private float m_spawnTimer = 0f;   // 出現用のタイマー

    public override void Awake()
    {
       
    }

    public override void Start()
    {
        m_miniGhostEnemy = m_stateOwner.GetComponent<MiniGhostEnemy>();
    }

    public override void EnterState()
    {
        StartSpawn();
    }

    public override void UpdateState()
    {
        UpdateSpawning();
    }

    public override void ExitState()
    {

    }

    private void StartSpawn()
    {
        SplineContainer targetSpline = m_miniGhostEnemy.GetTargetSpline();

        if (targetSpline != null)
        {
            // スプラインの始点座標を取得
            m_spawnTargetPos = targetSpline.EvaluatePosition(0f);

            // 向きの調整
            Vector3 initialTangent = targetSpline.EvaluateTangent(0f);
            if (initialTangent != Vector3.zero)
            {
                m_stateOwner.transform.rotation = Quaternion.LookRotation(initialTangent);
            }

            m_spawnTimer = 0f;
            m_isSpawningFlag = true; // 出現中フラグをON
        }
    }

    // 出現処理
    private void UpdateSpawning()
    {
        m_spawnTimer += Time.deltaTime;

        // 1.0を超えないようにクランプ
        float t = Mathf.Clamp01(m_spawnTimer / m_spawnDuration);

        // イージング関数を使用して補間値を計算
        float easeT = Easing.EaseOutSine(t);

        // 線形補間で座標を更新
        m_stateOwner.transform.position = Vector3.Lerp(m_miniGhostEnemy.GetSpawnPosition(), m_spawnTargetPos, easeT);

        // 出現完了判定
        if (t >= 1.0f)
        {
            m_stateOwner.transform.position = m_spawnTargetPos;
            m_isSpawningFlag = false;

            // ここからスプライン移動を開始
            m_miniGhostEnemy.SetDistanceTraveled(0f);
            m_miniGhostEnemy.SetMoveDirection(1f);

            m_stateMachine.ChangeState("MiniGhostMoveState");
        }
    }
}
