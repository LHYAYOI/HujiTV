using Unity.Cinemachine;
using UnityEngine;

public class CinemachineRotationExtension:CinemachineExtension
{
    private Vector3 m_shakeRotationOffset;
    private Vector3 m_tiltRotationOffset;

    public void SetShakeRotationOffset(Vector3 offset) 
    {
        m_shakeRotationOffset = offset;
    }

    public void SetTiltRotationOffset(Vector3 offset)
    {
        m_tiltRotationOffset = offset;
    }

    //‰ñ“]‚ğ‡¬‚µÅI“I‚Èp¨‚ğŒˆ’è‚·‚é
    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage == CinemachineCore.Stage.Finalize) 
        {
            Quaternion rotation = Quaternion.Euler(m_shakeRotationOffset + m_tiltRotationOffset);

            state.OrientationCorrection *= rotation;
        }
    }
}
