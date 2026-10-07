//-----------------------------------------------
// BookInputState.cs
// 制作日：2026/09/11
// 制作者：安田晴人
// 概要：本のページめくりの状態を保持するクラス
//-----------------------------------------------
using System;
using UnityEngine;

public enum BOOK_INPUT_STATE
{
    NORMAL,
    RUNE_TRACING,
    PAGE_TRANSITION,
    DISABLED
}