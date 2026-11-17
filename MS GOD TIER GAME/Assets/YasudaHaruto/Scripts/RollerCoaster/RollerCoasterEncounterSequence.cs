using UnityEngine;
using UnityEngine.Splines;
namespace YasudaHaruto.RollerCoaster
{
    public sealed class RollerCoasterEncounterSequence : MonoBehaviour
    {
        [SerializeField] private SplineContainer m_baseline;
        [SerializeField] private RollerCoasterEncounter[] m_encounters;
        public SplineContainer Baseline=>m_baseline;
        public RollerCoasterEncounter[] Encounters=>m_encounters;
#if UNITY_EDITOR
        public void Configure(SplineContainer baseline,RollerCoasterEncounter[] encounters){m_baseline=baseline;m_encounters=encounters;}
#endif
    }
}
