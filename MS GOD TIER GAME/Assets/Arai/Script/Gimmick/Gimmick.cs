using UnityEngine;

public abstract class Gimmick : MonoBehaviour
{

    [SerializeField] private bool m_executeFlag;
    public bool ExecuteFlag { get { return m_executeFlag; } set { m_executeFlag = value; } }




    public virtual bool CanExecute(GimmickContext context)
    {
        return m_executeFlag;
    }
    public abstract void Execute(GimmickContext context);



    protected void OnGizmosOnOff()
    {
        // OnOff の描画
        if (m_executeFlag)
        {
            Gizmos.color = Color.white;
        }
        else
        {
            Gizmos.color = Color.black;
        }

        Gizmos.DrawCube(transform.position + Vector3.up, Vector3.one * 0.3f);
    }
}
