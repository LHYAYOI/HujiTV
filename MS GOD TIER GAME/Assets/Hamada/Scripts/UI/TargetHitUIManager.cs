using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Unity.VisualScripting;

public class TargetHitUIManager : MonoBehaviour
{
    [SerializeField] TargetHitUI m_uiPrefab;
    [SerializeField] int m_poolNum;

    List<TargetHitUI> m_uiList = new List<TargetHitUI>();

    private void Awake()
    {
        CreateUIs();
    }

    private void Update()
    {
        
    }

    private void CreateUIs() 
    {
        for (int i = 0; i < m_poolNum; i++) 
        {
            TargetHitUI ui = Instantiate(m_uiPrefab, transform);
            ui.Init();
            ui.gameObject.SetActive(false);
            m_uiList.Add(ui);
        }
    }

    public void ShowHitUI(GameObject target) 
    {
        if (target == null) 
        {
            return;
        }

        foreach (TargetHitUI ui in m_uiList) 
        {
            if (ui == null) 
            {
                continue;
            }

            if (ui.gameObject.activeSelf)
            {
                continue;
            }

            ui.gameObject.SetActive(true);
            ui.ShowHitUI(target);

            break;
        }
    }
}
