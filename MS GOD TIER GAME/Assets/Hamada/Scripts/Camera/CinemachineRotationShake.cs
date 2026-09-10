using Unity.Cinemachine;
using UnityEngine;

public class CinemachineRotationShake:CinemachineExtension
{
    private Vector3 m_rotationOffset;

    public void SetRotationOffset(Vector3 offset) 
    {
        m_rotationOffset = offset;
    }

    public void ResetOffset() 
    {
        m_rotationOffset = Vector3.zero;
    }

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage == CinemachineCore.Stage.Finalize) 
        {
            Quaternion shakeRotation = Quaternion.Euler(m_rotationOffset);

            state.RawOrientation = state.RawOrientation * shakeRotation;
        }
    }
}
