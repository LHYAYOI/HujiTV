using UnityEngine;

namespace LightShaftLab
{
    [AddComponentMenu("LightShaft/光柱（LightShaftEmitter）")]
    [DisallowMultipleComponent, RequireComponent(typeof(Light))]
    public sealed class LightShaftEmitter : MonoBehaviour
    {
        [Min(0), Tooltip("描画選別の優先倍率。1=通常、値を上げると見せ場を優先。0=選別対象外。")]
        public float m_priority = 1;
        [Min(0)]
        [UnityEngine.Serialization.FormerlySerializedAs("scattering")]
        public float m_scattering = 1;
        [Tooltip("URPの影を霧の散乱に反映する。")]
        [UnityEngine.Serialization.FormerlySerializedAs("shadows")]
        public bool m_castShadowsFlag = true;
    }
}
