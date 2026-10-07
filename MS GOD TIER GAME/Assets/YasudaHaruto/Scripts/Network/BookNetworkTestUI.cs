using UnityEngine;

public class BookNetworkTestUI : MonoBehaviour
{
    private int m_pingValue;

    public void OnPingButtonPressed()
    {
        BookNetworkClient client =
            BookNetworkClient.Instance;

        if (client == null)
        {
            Debug.LogError("BookNetworkClientÇ™ë∂ç›ÇµÇ‹ÇπÇÒ");

            return;
        }

        m_pingValue++;

        client.SendTestPing(m_pingValue);
    }

    public void OnCastSkillButtonPressed()
    {
        BookNetworkClient client = BookNetworkClient.Instance;

        if (client == null)
        {
            Debug.LogError("BookNetworkClientÇ™ë∂ç›ÇµÇ‹ÇπÇÒ");
            return;
        }

        BookNetworkService networkService = new BookNetworkService(client);

        networkService.CastSkill(1);
    }
}