using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public class EnemyDeathTextManager : MonoBehaviour
{
    [SerializeField] DeathTextController m_deathTextPrefab;
    [SerializeField] int m_uiPoolAmount = 3;

    List<DeathTextController> m_uiList = new List<DeathTextController>();

    [SerializeField] DeathTextData m_testDeathTextData;

    private void Start()
    {
        CreateUIs();

        m_testDeathTextData.SetSpawnPosition(new Vector3(0, 0, 1));
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.M))
        {
            PlayText(m_testDeathTextData);
        }
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

    public void PlayText(DeathTextData data) 
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

            ui.gameObject.SetActive(true);
            ui.Appear(data);

            break;
        }
    }
}
