using Unity.Cinemachine;
using UnityEngine;

public class CinemachineRotationExtension:CinemachineExtension
{
    private Vector3 m_shakeRotationOffset;
    private Vector3 m_tiltRotationOffset;
    private Vector3 m_kickRotationOffset;

    public void SetShakeRotationOffset(Vector3 offset) 
    {
        m_shakeRotationOffset = offset;
    }

    public void SetTiltRotationOffset(Vector3 offset)
    {
        m_tiltRotationOffset = offset;
    }

    public void SetKickRotationOffset(Vector3 offset)
    {
        m_kickRotationOffset = offset;
    }

    //回転を合成し最終的な姿勢を決定する
    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage == CinemachineCore.Stage.Finalize) 
        {
            Quaternion rotation = Quaternion.Euler(m_shakeRotationOffset + m_tiltRotationOffset + m_kickRotationOffset);

            state.OrientationCorrection *= rotation;
        }
    }
}
