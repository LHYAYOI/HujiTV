using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using static UnityEngine.ParticleSystem;

public class MagicBulletController : MonoBehaviour
{
    [Serializable]
    private struct BezierControlPoint
    {
        public Vector2 m_curveHeight;
        public Vector2 m_curveWidth;
    }

    [SerializeField] MagicData m_magicData;

    [SerializeField] List<ParticleController> m_particles;

    [SerializeField] GameObject m_hitEffect;

    [SerializeField] GameObject m_kiranEffect;

    [SerializeField] float m_moveSpeed;

    [SerializeField] List<BezierControlPoint> m_bezierControlPoints;

    TargetHitUIManager m_hitUIManager;

    Vector3 m_controlPoint;

    public GameObject GetKiranEffect() 
    {
        return m_kiranEffect;
    }

    public MagicData GetMagicData()
    {
        return m_magicData;
    }

    public void SetHitUIManager(TargetHitUIManager manager) 
    {
        m_hitUIManager = manager;
    }

    public void FireBullet(TargetableObject target, MAGIC_CAST_DIRECTION direction)
    {
        if (target == null) 
        {
            return;
        }

        Vector3 centerPoint = Vector3.Lerp(transform.position, target.transform.position, 0.5f);

        CalculateControlPoint(centerPoint, direction);

        float distance = (target.transform.position - transform.position).magnitude;

        float duration = distance / m_moveSpeed;

        StartCoroutine(HomingRoutine(target, duration));
    }

    private void CalculateControlPoint(Vector3 centerPoint,MAGIC_CAST_DIRECTION direction) 
    {
        BezierControlPoint controlPoint = m_bezierControlPoints[(int)direction];

        float curveHeight = UnityEngine.Random.Range(controlPoint.m_curveHeight.x, controlPoint.m_curveHeight.y);
        float curveWidth = UnityEngine.Random.Range(controlPoint.m_curveWidth.x, controlPoint.m_curveWidth.y);

        m_controlPoint = centerPoint + Vector3.up * curveHeight + Vector3.right * curveWidth;
    }

    private IEnumerator HomingRoutine(TargetableObject target, float duration)
    {
        Vector3 startPosition = transform.position;

        float deltaTime = 0;

        while (deltaTime < duration)
        {
            if (target == null || !target.IsTargetable()) 
            {
                TargetUnExistProcess();
                yield break;
            }

            deltaTime += Time.deltaTime;

            float rate = deltaTime / duration;

            rate = Mathf.Clamp01(rate);

            transform.position = CalculateBezier(startPosition, m_controlPoint, target.transform.position, rate);

            yield return null;
        }
    }

    private Vector3 CalculateBezier(Vector3 start, Vector3 controlPoint, Vector3 end, float rate) 
    {
        float u = 1.0f - rate;

        return u * u * start + 2f * u * rate * controlPoint + rate * rate * end;
    }

    private void TargetUnExistProcess() 
    {
        foreach (ParticleController particle in m_particles) 
        {
            if (particle == null) 
            {
                continue;
            }

            particle.MakeParticlesDestroyable(1);
            particle.transform.parent = null;
        }
    }

    public void HitProcess(HitData hitData) 
    {
        if (hitData == null) 
        {
            return;
        }

        //ヒットUI
        if (m_hitUIManager) 
        {
            m_hitUIManager.ShowHitUI(hitData.GetHitObject, m_magicData.ImpactRuneTexture);
        }

        foreach (ParticleController particle in m_particles) 
        {
            if (particle.GetParticleType() == ParticleController.PARTICLE_TYPE.TRAIL) 
            {
                particle.gameObject.transform.parent = null;
            }
        }

        //ヒットエフェクト
        Instantiate(m_hitEffect, transform.position, quaternion.identity);

        Destroy(gameObject);
    }
}
