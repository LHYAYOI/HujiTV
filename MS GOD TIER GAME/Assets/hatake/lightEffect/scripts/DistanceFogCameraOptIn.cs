using UnityEngine;

namespace LightShaftLab
{
    /// <summary>
    /// 反射用カメラ（CameraType.Reflection）にも距離フォグをかける目印。鏡のカメラに付ける。
    /// 付いていない反射用カメラ（水たまりのゴースト反射など）にはフォグをかけない。
    /// </summary>
    [DisallowMultipleComponent, AddComponentMenu("")]
    public sealed class DistanceFogCameraOptIn : MonoBehaviour
    {
    }
}
