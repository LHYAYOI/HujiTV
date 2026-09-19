//-----------------------------------------------
// IPageInteraction.cs
// 制作日：2026/09/19
// 制作者：安田晴人
// 概要： ページのインタラクションを定義するインターフェース
//-----------------------------------------------
public interface IPageInteraction
{
    bool IsActive { get; }

    void Begin();

    void End();
}