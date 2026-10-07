//-----------------------------------------------
// CastMagicCommand.cs
// 制作日：2026/09/29
// 制作者：安田晴人
// 概要： 魔法を発動するコマンドクラス
//-----------------------------------------------
public class CastMagicCommand : IPageCommand
{
    private readonly byte m_skillId;
    private readonly BookNetworkService m_networkService;

    public CastMagicCommand(byte skillId, BookNetworkService networkService)
    {
        m_skillId = skillId;
        m_networkService = networkService;
    }

    public void Execute()
    {
        if (m_networkService == null)
        {
            return;
        }

        m_networkService.CastSkill(m_skillId);
    }
}