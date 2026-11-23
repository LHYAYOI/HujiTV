using System.Collections.Generic;
using UnityEngine;

public class ParticleController : MonoBehaviour
{
    public enum PARTICLE_TYPE
    {
        MAIN_PARTICLE,
        TRAIL
    }

    [SerializeField] List<ParticleSystem> m_particleList;

    [SerializeField] PARTICLE_TYPE m_type;

    public void MakeParticlesDestroyable(float destroyTime)
    {
        foreach (ParticleSystem particle in m_particleList)
        {
            if (particle == null) 
            {
                continue;
            }

            ParticleSystem.MainModule par = particle.main;
            par.loop = false;
            par.stopAction = ParticleSystemStopAction.Destroy;
        }
    }

    public PARTICLE_TYPE GetParticleType() 
    {
        return m_type;
    }
}
