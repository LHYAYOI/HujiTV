//-----------------------------------------------
// BookController.cs
// 制作日：2026/09/11
// 制作者：安田晴人
// 概要：ページめくりの制御を行うスクリプト
//-----------------------------------------------
using System;
using UnityEngine;

public class BookController
{
    private readonly BookModel m_model;
    private readonly BookInputController m_inputController;

    public BookController(BookModel model, BookInputController inputController)
    {
        m_model = model;
        m_inputController = inputController;
    }

    public void Begin()
    {
        BeginCurrentPageInteraction();
    }

    public void RequestNavigation(PAGE_NAVIGATION_DIRECTION direction)
    {
        if (!m_inputController.CanNavigatePage)
        {
            return;
        }

        bool canMove = direction switch
        {
            PAGE_NAVIGATION_DIRECTION.NEXT => m_model.CanMoveNext,

            PAGE_NAVIGATION_DIRECTION.PREVIOUS => m_model.CanMovePrevious,

            _ => false
        };

        if (!canMove)
        {
            return;
        }

        EndCurrentPageInteraction();

        bool moved = direction switch
        {
            PAGE_NAVIGATION_DIRECTION.NEXT => m_model.MoveNext(),

            PAGE_NAVIGATION_DIRECTION.PREVIOUS => m_model.MovePrevious(),

            _ => false
        };

        if (!moved)
        {
            // Interactionを終了したままにしないため復帰させる。
            BeginCurrentPageInteraction();
            return;
        }

        BeginCurrentPageInteraction();
    }

    public void End()
    {
        EndCurrentPageInteraction();
    }

    private void BeginCurrentPageInteraction()
    {
        m_model.CurrentPage?.BeginInteraction();
    }

    private void EndCurrentPageInteraction()
    {
        m_model.CurrentPage?.EndInteraction();
    }
}