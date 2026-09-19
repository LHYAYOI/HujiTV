//-----------------------------------------------
// PageInstance.cs
// 制作日：2026/09/19
// 制作者：安田晴人
// 概要： ページインスタンスのクラス
//-----------------------------------------------
public class PageInstance
{
    private readonly PageData m_data;
    private readonly IPageInteraction m_interaction;

    public PageData Data => m_data;
    public IPageInteraction Interaction => m_interaction;

    public PageInstance(PageData data, IPageInteraction interaction)
    {
        m_data = data;
        m_interaction = interaction;
    }

    public void BeginInteraction()
    {
        m_interaction?.Begin();
    }

    public void EndInteraction()
    {
        m_interaction?.End();
    }
}