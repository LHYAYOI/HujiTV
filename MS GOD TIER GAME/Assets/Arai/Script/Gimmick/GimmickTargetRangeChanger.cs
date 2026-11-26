using UnityEngine;

public class GimmickTargetRangeChanger : MonoBehaviour
{
    [SerializeField] private float m_range = 0.0f;


    public void OnEnter()
    {
        TargetSelector.Instance.SetTargetRange(m_range);
    }

    public void OnExit()
    {
        TargetSelector.Instance.Resetrange();
    }

}
