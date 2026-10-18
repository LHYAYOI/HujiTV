using UnityEngine;
using UnityEngine.Splines;

public class MiniGhostSpawnState : MiniGhostState
{
    [Header("ƒXƒvƒ‰ƒCƒ“‚Ü‚Å‚ÌˆÚ“®ŽžŠÔ")]
    [SerializeField] private float m_spawnDuration = 3.0f;

    private Vector3 m_spawnTargetPos;
    private float m_spawnTimer;
    private bool m_hasSpawnTarget;

    public override void EnterState()
    {
        m_spawnTimer = 0f;
        m_hasSpawnTarget = false;

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

        m_hasSpawnTarget = true;
    }

    public override void UpdateState()
    {
        if (!m_hasSpawnTarget || m_owner.GetIsDying())
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
            m_owner.SetMoveDirection(1f);
            m_owner.StartTimeLimit();
            m_owner.ChangeState(MINI_GHOST_STATE_TYPE.MOVE);
        }

    }

    public override void ExitState()
    {
        m_hasSpawnTarget = false;
    }

}
