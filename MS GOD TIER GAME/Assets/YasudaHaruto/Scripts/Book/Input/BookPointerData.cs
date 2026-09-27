//-----------------------------------------------
// BookPointerData.cs
// 制作日：2026/09/20
// 制作者：安田晴人
// 概要： 本のページめくりのポインタ情報を保持する構造体
//-----------------------------------------------
using UnityEngine;

public struct BookPointerData
{
    public Vector2 Position { get; }

    public BookPointerData(Vector2 position)
    {
        Position = position;
    }
}