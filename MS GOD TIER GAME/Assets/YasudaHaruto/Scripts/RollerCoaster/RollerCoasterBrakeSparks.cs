using UnityEngine;

namespace YasudaHaruto.RollerCoaster
{
    [DefaultExecutionOrder(1050)]
    public sealed class RollerCoasterBrakeSparks : MonoBehaviour
    {
        [SerializeField] private Material m_material;
        private RollerCoasterTrialRide m_ride;
        private RollerCoasterSpeedProfile m_profile;
        private readonly ParticleSystem[] m_sides=new ParticleSystem[2];
        private float m_emissionRemainder;
        private float m_previousBrakeTime;
        public int LeftParticleCount => m_sides[0]!=null?m_sides[0].particleCount:0;
        public int RightParticleCount => m_sides[1]!=null?m_sides[1].particleCount:0;
        public float LastRelativeForwardSpeed { get; private set; }

        private void Start()
        {
            m_ride=GetComponent<RollerCoasterTrialRide>();m_profile=GetComponent<RollerCoasterSpeedProfile>();
            for(int side=0;side<2;side++)
            {
                var go=new GameObject(side==0?"Left Brake Sparks":"Right Brake Sparks");
                go.transform.SetParent(m_ride.Cart.transform,false);go.transform.localPosition=new Vector3(side==0?-.88f:.88f,.2f,.7f);
                var system=go.AddComponent<ParticleSystem>();system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=system.main;main.loop=true;main.playOnAwake=false;main.simulationSpace=ParticleSystemSimulationSpace.World;
                main.startLifetime=new ParticleSystem.MinMaxCurve(.14f,.28f);main.startSize=new ParticleSystem.MinMaxCurve(.016f,.035f);
                main.startSpeed=0f;main.gravityModifier=1f;main.maxParticles=180;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
                var emission=system.emission;emission.enabled=false;var shape=system.shape;shape.enabled=false;
                var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(new Color(1f,.95f,.55f),0),new GradientColorKey(new Color(1f,.32f,.025f),1)},
                    new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});
                var color=system.colorOverLifetime;color.enabled=true;color.color=gradient;
                var renderer=system.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=m_material;
                renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.lengthScale=1.2f;renderer.velocityScale=.006f;
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
                system.Play();m_sides[side]=system;
            }
        }

        private void LateUpdate()
        {
            if(m_ride==null||m_sides[0]==null)return;
            if(m_profile.DropBrakeElapsed<m_previousBrakeTime||!m_profile.DropBrakeStarted)
            {foreach(var side in m_sides)side.Clear();m_emissionRemainder=0f;}
            m_previousBrakeTime=m_profile.DropBrakeElapsed;
            if(m_ride.Paused)
            {foreach(var side in m_sides)if(!side.isPaused)side.Pause();return;}
            foreach(var side in m_sides)if(side.isPaused)side.Play();
            if(!m_profile.DropBrakeStarted||m_profile.DropStage!=RollerCoasterSpeedProfile.DROP_STAGE.APPROACH||m_ride.Cart.Speed<.15f)return;
            m_emissionRemainder+=Time.deltaTime*Mathf.Lerp(70f,180f,Mathf.Clamp01(m_ride.Cart.Speed/16f));
            int count=Mathf.FloorToInt(m_emissionRemainder);m_emissionRemainder-=count;
            for(int side=0;side<2;side++)for(int i=0;i<count;i++)
            {
                var source=m_sides[side].transform;
                var velocity=new Vector3((side==0?-1:1)*Random.Range(.3f,.9f),Random.Range(2.8f,4.5f),-Random.Range(3f,5f));
                Vector3 worldVelocity=source.TransformDirection(velocity)+m_ride.Cart.transform.forward*m_ride.Cart.Speed*.6f;
                LastRelativeForwardSpeed=Vector3.Dot(worldVelocity-m_ride.Cart.transform.forward*m_ride.Cart.Speed,m_ride.Cart.transform.forward);
                var particle=new ParticleSystem.EmitParams{position=source.position,velocity=worldVelocity};
                m_sides[side].Emit(particle,1);
            }
        }

        private void OnDestroy(){foreach(var side in m_sides)if(side!=null)Destroy(side.gameObject);}
        private void OnDisable(){foreach(var side in m_sides)if(side!=null)side.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
#if UNITY_EDITOR
        public void Configure(Material material){m_material=material;}
#endif
    }
}
