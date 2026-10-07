//-----------------------------------------------
// BookNetworkService.cs
// 制作日：2026/09/29
// 制作者：安田晴人
// 概要： クライアント側のネットワークサービスを提供するクラス
//-----------------------------------------------
public class BookNetworkService
{
    private readonly BookNetworkClient m_client;

    public BookNetworkService(BookNetworkClient client)
    {
        m_client = client;
    }

    public bool CastSkill(byte skillId)
    {
        if (m_client == null)
        {
            return false;
        }

        if (skillId == 0)
        {
            return false;
        }

        return m_client.SendCastSkill(skillId);
    }
}