using UnityEngine;

/// <summary>
/// 対象（トロッコのカメラなど）がこの箱の範囲に入ったら、指定のムードへ切り替える。
/// 物理の当たり判定は使わないので、Rigidbody や Collider は不要。
/// スプラインのギミックから切り替える場合は、UnityEvent から Fire() を呼んでもよい。
/// </summary>
[AddComponentMenu("hatake/Lighting/Light Mood Trigger")]
public sealed class LightMoodTrigger : MonoBehaviour
{
    [Tooltip("切り替え先のムード。")]
    [SerializeField] private LightMood m_mood;
    [Min(0), Tooltip("切り替えにかける秒数。")]
    [SerializeField] private float m_blendSeconds = 2;
    [Tooltip("範囲の大きさ（このオブジェクトのローカル、Scale も掛かる）。")]
    [SerializeField] private Vector3 m_size = new Vector3(4, 4, 4);
    [Tooltip("範囲に入るかを調べる対象。未指定ならメインカメラ。")]
    [SerializeField] private Transform m_target;
    [Tooltip("一度だけ切り替える。オフなら入るたびに切り替える。")]
    [SerializeField] private bool m_onceFlag = true;

    private bool m_insideFlag;
    private bool m_firedFlag;

    private void Update()
    {
        Transform target = m_target;
        if (target == null && Camera.main != null)
        {
            target = Camera.main.transform;
        }
        if (target == null)
        {
            return;
        }
        Vector3 local = transform.InverseTransformPoint(target.position);
        bool insideFlag = Mathf.Abs(local.x) <= m_size.x * 0.5f && Mathf.Abs(local.y) <= m_size.y * 0.5f && Mathf.Abs(local.z) <= m_size.z * 0.5f;
        if (insideFlag && !m_insideFlag && !(m_onceFlag && m_firedFlag))
        {
            Fire();
        }
        m_insideFlag = insideFlag;
    }

    /// <summary>ムードを切り替える。</summary>
    [ContextMenu("Fire")]
    public void Fire()
    {
        if (m_mood == null)
        {
            return;
        }
        m_firedFlag = true;
        LightMoodDirector.Instance.Request(m_mood, m_blendSeconds);
    }

    private void OnDrawGizmos()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1, 0.75f, 0.3f, 0.12f);
        Gizmos.DrawCube(Vector3.zero, m_size);
        Gizmos.color = new Color(1, 0.75f, 0.3f, 0.8f);
        Gizmos.DrawWireCube(Vector3.zero, m_size);
    }
}
