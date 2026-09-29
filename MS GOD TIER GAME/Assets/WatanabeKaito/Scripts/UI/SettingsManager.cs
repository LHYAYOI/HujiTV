using UnityEngine;

public class SettingsManager : MonoBehaviour
{
    [Header("İ’è‰æ–Ê")]
    [SerializeField] private GameObject m_settingsPanel;

    [Header("İ’è‰æ–Ê‚ğŠJ‚­ƒ{ƒ^ƒ“")]
    [SerializeField] private GameObject m_openSettingsButton;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        m_settingsPanel.gameObject.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OpenSettingsPanel()
    {
        m_settingsPanel.gameObject.SetActive(true);

        m_openSettingsButton.gameObject.SetActive(false);
    }

    public void CloseSettingsPanel()
    {
        m_settingsPanel.gameObject.SetActive(false);

        m_openSettingsButton.gameObject.SetActive(true);
    }
}
