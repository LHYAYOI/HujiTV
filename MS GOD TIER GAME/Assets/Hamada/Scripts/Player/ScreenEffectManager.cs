using Hatake.ScreenFx;
using System.Collections;
using UnityEngine;

public class ScreenEffectManager : MonoBehaviour
{
    [SerializeField] ScreenEdgeFx m_edgeFx;

    Coroutine m_Coroutine;

    public void DoDamageScreen() 
    {
        if (m_Coroutine != null) 
        {
            StopCoroutine(m_Coroutine);
        }

        m_Coroutine = StartCoroutine(ScreenDamageRoutine());
    }

    IEnumerator ScreenDamageRoutine() 
    {
        m_edgeFx.Grunge.weight = 1;

        float startWeight = m_edgeFx.Grunge.weight;

        float elapsedTime = 0;

        float duration = 1.5f;

        while (elapsedTime < duration) 
        {
            elapsedTime += Time.deltaTime;

            float rate = elapsedTime / duration;

            rate = Easing.EaseOutCubic(rate);

            m_edgeFx.Grunge.weight = Mathf.Lerp(startWeight, 0, rate);

            yield return null;
        }

        m_edgeFx.Grunge.weight = 0;
    }

}
