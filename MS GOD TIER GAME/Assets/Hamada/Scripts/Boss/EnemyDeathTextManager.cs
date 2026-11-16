using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public class EnemyDeathTextManager : MonoBehaviour
{
    [SerializeField] DeathTextController m_deathTextPrefab;
    [SerializeField] int m_uiPoolAmount = 3;

    List<DeathTextController> m_uiList = new List<DeathTextController>();

    private void Start()
    {
        CreateUIs();
    }

    private void Update()
    {
    }

    private void CreateUIs() 
    {
        for (int i = 0; i < m_uiPoolAmount; i++)
        {
            DeathTextController ui = Instantiate(m_deathTextPrefab, transform);
            ui.SetUsingFlag(false);
            m_uiList.Add(ui);
        }
    }

    public void PlayText(Vector3 position, Vector2 scale, DeathTextData data)
    {
        if (data == null)
        {
            return;
        }

        foreach (DeathTextController ui in m_uiList)
        {
            if (ui == null)
            {
                continue;
            }

            if (ui.IsUsing())
            {
                continue;
            }

            ui.SetUsingFlag(true);
            ui.Appear(position, scale, data);

            break;
        }
    }
}
