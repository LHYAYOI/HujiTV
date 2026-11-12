using System.Collections.Generic;
using UnityEngine;

public class GimmickAnimPlayer : MonoBehaviour
{
    // ―――――――――――――――――――――――――――――――――――――
    // Propaty
    // ―――――――――――――――――――――――――――――――――――――

    [System.Serializable]
    public class AnimData
    {
        [SerializeField] private Animator m_animator;
        [SerializeField] private string m_triggerName;

        public Animator Animator { get { return m_animator; } }
        public string TriggerName {  get { return m_triggerName; } }
    }

    [SerializeField] private List<AnimData> m_anims;


    // ―――――――――――――――――――――――――――――――――――――
    // Public Event
    // ―――――――――――――――――――――――――――――――――――――  
    public void Play()
    {
        foreach (var anim in m_anims)
        {
            anim.Animator.SetTrigger(anim.TriggerName);
        }
    }

    // ―――――――――――――――――――――――――――――――――――――
    // Gizmos
    // ―――――――――――――――――――――――――――――――――――――
    private void OnDrawGizmos()
    {

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(transform.position, 0.1f);

        foreach(var anim in m_anims)
        {
            Vector3 to = anim.Animator.transform.position;
            Gizmos.DrawLine(transform.position, to);

            Gizmos.DrawSphere(to, 0.08f);

        }
    }
}
