//-----------------------------------------------
// IBookPointerInput.cs
// 制作日：2026/09/20
// 制作者：安田晴人
// 概要： 本のページめくりのポインタ情報を取得するインターフェース
//-----------------------------------------------
public interface IBookPointerInput
{
    bool TryGetPointerDown(out BookPointerData data);

    bool TryGetPointer(out BookPointerData data);

    bool TryGetPointerUp(out BookPointerData data);
}