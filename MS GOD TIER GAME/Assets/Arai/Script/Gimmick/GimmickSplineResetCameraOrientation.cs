using UnityEngine;

public class GimmickSplineResetCameraOrientation : GimmickSpline
{
    public void Reset()
    {
        CameraManager.Instance.ResetCameraOrientation();
    }

    // ―――――――――――――――――――――――――――――――――――――
    // Gizmos
    // ―――――――――――――――――――――――――――――――――――――
    private void OnDrawGizmos()
    {
        OnGizmosMyPosition();


    }
}
