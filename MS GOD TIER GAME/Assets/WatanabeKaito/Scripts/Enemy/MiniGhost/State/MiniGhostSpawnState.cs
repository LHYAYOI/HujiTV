using System.Collections;
using UnityEngine;
using UnityEngine.Splines;

public class MiniGhostSpawnState : MiniGhostState
{
    [Header("スプラインまでの移動時間")]
    [SerializeField] private float m_spawnDuration = 3.0f;

    private Vector3 m_spawnTargetPos;   // スプライン上の出現位置
    private float m_spawnTimer;         // スプラインまでの移動時間の経過時間

    public override void EnterState()
    {
        m_spawnTimer = 0f;
        SplineContainer targetSpline = m_owner.GetTargetSpline();
        if (targetSpline == null)
        {
            return;
        }

        m_spawnTargetPos = targetSpline.EvaluatePosition(0f);
        Vector3 initialTangent = targetSpline.EvaluateTangent(0f);
        if (initialTangent.sqrMagnitude > 0.0001f)
        {
            m_owner.transform.rotation = Quaternion.LookRotation(initialTangent);
        }

    }

    public override void UpdateState()
    {
        if (m_owner.GetIsDying())
        {
            return;
        }

        m_spawnTimer += Time.deltaTime;

        float t;
        if (m_spawnDuration <= 0f)
        {
            t = 1f;
        }
        else
        {
            t = Mathf.Clamp01(m_spawnTimer / m_spawnDuration);
        }

        float easeT = Easing.EaseOutSine(t);

        m_owner.transform.position = Vector3.Lerp(
            m_owner.GetSpawnPosition(),
            m_spawnTargetPos,
            easeT);

        if (t >= 1f)
        {
            m_owner.transform.position = m_spawnTargetPos;
            m_owner.SetDistanceTraveled(0f);

            m_owner.ChangeState(MINI_GHOST_STATE_TYPE.IDLE);
        }

    }

    public override void ExitState()
    {
        // 透明から元のマテリアルに戻す
        StartCoroutine(m_owner.SwitchMaterialBlendCoroutine(1, 0));
    }

    

}
