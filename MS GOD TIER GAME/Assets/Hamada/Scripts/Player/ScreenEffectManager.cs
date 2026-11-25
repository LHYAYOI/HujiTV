using Hatake.ScreenFx;
using System.Collections;
using UnityEngine;

public class ScreenEffectManager : MonoBehaviour
{
    [SerializeField] ScreenEdgeFx m_edgeFx;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void DoDamageScreen() 
    {
        StartCoroutine(ScreenDamageRoutine());
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
