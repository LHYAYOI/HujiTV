using UnityEngine;

namespace LightShaftLab
{
    [DisallowMultipleComponent, RequireComponent(typeof(Light))]
    public sealed class LightShaftEmitter : MonoBehaviour
    {
        [Min(0)]
        [UnityEngine.Serialization.FormerlySerializedAs("scattering")]
        public float m_scattering = 1;
        [Tooltip("URPの影を霧の散乱に反映する。")]
        [UnityEngine.Serialization.FormerlySerializedAs("shadows")]
        public bool m_castShadowsFlag = true;
    }
}
