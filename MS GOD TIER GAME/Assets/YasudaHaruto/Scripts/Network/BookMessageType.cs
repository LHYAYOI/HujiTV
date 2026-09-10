//-----------------------------------------------
// BookMessageType.cs
// 制作日：2026/09/10
// 制作者：安田晴人
// 概要： Unity Transportを使用したクライアントとサーバー間の通信で使用するメッセージタイプの定義
//-----------------------------------------------
public enum BOOK_MESSAGE_TYPE : byte
{
    NONE = 0,

    BUTTON_PRESSED = 1,
    ORIENTATION = 2,
}