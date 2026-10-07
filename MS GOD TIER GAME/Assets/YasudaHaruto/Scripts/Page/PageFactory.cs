//-----------------------------------------------
// PageFactory.cs
// 制作日：2026/09/30
// 制作者：安田晴人
// 概要： ページインスタンスを生成するファクトリークラス
//-----------------------------------------------
public class PageFactory
{
    private readonly RuneTraceController m_runeTraceController;
    private readonly BookInputController m_inputController;
    private readonly BookNetworkService m_networkService;

    public PageFactory(
        RuneTraceController runeTraceController, BookInputController inputController, BookNetworkService networkService)
    {
        m_runeTraceController = runeTraceController;
        m_inputController = inputController;
        m_networkService = networkService;
    }

    public PageInstance Create(PageData data)
    {
        if (data == null)
        {
            return null;
        }

        IPageCommand command = CreateCommand(data.Action);

        IPageInteraction interaction = CreateInteraction(data.Interaction, command);

        return new PageInstance(data, interaction);
    }

    private IPageCommand CreateCommand(PageActionData data)
    {
        if (data == null)
        {
            return null;
        }

        switch (data.Type)
        {
            case PAGE_ACTION_TYPE.CAST_MAGIC:

                if (data.MagicData == null)
                {
                    return null;
                }

                return new CastMagicCommand(data.MagicData.MagicId, m_networkService);

            case PAGE_ACTION_TYPE.NONE:
            default:
                return null;
        }
    }

    private IPageInteraction CreateInteraction(PageInteractionData data, IPageCommand command)
    {
        if (data == null)
        {
            return null;
        }

        switch (data.Type)
        {
            case PAGE_INTERACTION_TYPE.RUNE_TRACE:

                if (data.RuneData == null)
                {
                    return null;
                }

                return new RuneTraceInteraction(data.RuneData, m_runeTraceController, m_inputController,
                    command);

            case PAGE_INTERACTION_TYPE.NONE:
            default:
                return null;
        }
    }
}