using UnityEngine;

namespace YasudaHaruto.RollerCoaster
{
    public sealed class RollerCoasterStageEntry : MonoBehaviour
    {
        [SerializeField] private RollerCoasterTrialRide m_ride;

        public void Enter(HitData hit)
        {
            var stageCart = hit.GetHitObjectComponent<CartController>();
            if (stageCart == null || m_ride == null || m_ride.gameObject.activeSelf)
                return;

            stageCart.SetSpeed(0f);
            m_ride.gameObject.SetActive(true);
        }
    }
}
