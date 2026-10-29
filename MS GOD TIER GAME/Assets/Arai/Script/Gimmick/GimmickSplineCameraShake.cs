using UnityEngine;

public class GimmickSplineCameraShake : GimmickSpline
{

    // ―――――――――――――――――――――――――――――――――――――
    // Propaty
    // ―――――――――――――――――――――――――――――――――――――
    [SerializeField] private float m_duration;
    [SerializeField] private float m_horShake;
    [SerializeField] private float m_velShake;

    // ―――――――――――――――――――――――――――――――――――――
    // Public Event
    // ―――――――――――――――――――――――――――――――――――――  
    public void Shake()
    {
        CameraManager.Instance.ShakeCameraInVH(m_duration, m_horShake,m_velShake);
    }

    // ―――――――――――――――――――――――――――――――――――――
    // Gizmos
    // ―――――――――――――――――――――――――――――――――――――
    private void OnDrawGizmos()
    {
        OnGizmosMyPosition();


    }
}
