using Unity.Cinemachine;
using UnityEngine;

namespace YasudaHaruto.RollerCoaster
{
    /// <summary>既存Noiseのカメラ上下方向だけを増幅する。横揺れと車体姿勢は変えない。</summary>
    public sealed class RollerCoasterVerticalNoiseExtension : CinemachineExtension
    {

        public float VerticalScale { get; set; } = 1f;

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase camera,
            CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Noise)
                return;
            Vector3 up = state.RawOrientation * Vector3.up;
            state.PositionCorrection += up * Vector3.Dot(state.PositionCorrection, up) * (Mathf.Max(0f, VerticalScale) - 1f);
        }
    }
}
